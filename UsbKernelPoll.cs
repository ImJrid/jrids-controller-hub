using System.Diagnostics;
using System.Security.Principal;
using System.Text.Json;
using Microsoft.Diagnostics.Tracing;

namespace JridsControllerHub;

internal static class UsbKernelPoll
{
    public static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static void CaptureToFile(string outputPath, int seconds)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        try
        {
            var result = Capture(Math.Clamp(seconds, 5, 20));
            File.WriteAllText(outputPath, JsonSerializer.Serialize(result));
        }
        catch (Exception ex)
        {
            File.WriteAllText(outputPath, JsonSerializer.Serialize(new
            {
                error = ex.Message,
                hz = 0,
                samples = 0,
                buckets = Array.Empty<object>()
            }));
        }
    }

    private static object Capture(int seconds)
    {
        if (!IsAdministrator())
        {
            return Fail("Administrator rights are required for kernel USB tracing.");
        }

        var etlPath = Path.Combine(Path.GetTempPath(), $"jrids-usb-{Guid.NewGuid():N}.etl");
        var session = "jridsusb";
        try
        {
            RunLogman($"stop {session} -ets", ignoreErrors: true);
            var start = RunLogman($"start {session} -p Microsoft-Windows-USB-UCX -o \"{etlPath}\" -ets");
            if (start != 0)
            {
                return Fail("Could not start USB ETW capture. Windows blocked kernel tracing.");
            }

            RunLogman($"update {session} -p Microsoft-Windows-USB-USBPORT -ets", ignoreErrors: true);
            Thread.Sleep(TimeSpan.FromSeconds(seconds));
            RunLogman($"stop {session} -ets");

            if (!File.Exists(etlPath) || new FileInfo(etlPath).Length < 64)
            {
                return Fail("Capture produced no USB trace. Plug the pad in over USB and try again.");
            }

            var times = ParseInterruptCompletions(etlPath);
            if (times.Count < 30)
            {
                return Fail("Not enough USB interrupt reports. Keep the pad plugged in over USB.");
            }

            var intervalsUs = new List<double>(times.Count);
            for (var i = 1; i < times.Count; i++)
            {
                var us = (times[i] - times[i - 1]) * 1000.0;
                if (us is > 0 and < 50_000)
                {
                    intervalsUs.Add(us);
                }
            }

            if (intervalsUs.Count < 24)
            {
                return Fail("USB interrupt timing was too sparse. Keep the pad plugged in over USB.");
            }

            var hz = 1_000_000.0 / intervalsUs.Average();
            return new
            {
                error = (string?)null,
                hz = Math.Round(hz),
                samples = intervalsUs.Count,
                buckets = Buckets(intervalsUs)
            };
        }
        finally
        {
            RunLogman($"stop {session} -ets", ignoreErrors: true);
            try
            {
                File.Delete(etlPath);
            }
            catch
            {
                // Temp trace can stay until reboot.
            }
        }
    }

    private static List<double> ParseInterruptCompletions(string etlPath)
    {
        var pending = new Dictionary<ulong, ulong>();
        var byPipe = new Dictionary<ulong, List<double>>();

        using var source = new ETWTraceEventSource(etlPath);
        source.Dynamic.All += data =>
        {
            var provider = data.ProviderName ?? "";
            if (!provider.Contains("USB-UCX", StringComparison.OrdinalIgnoreCase)
                && !provider.Contains("USB-USBPORT", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var eventName = data.EventName ?? "";
            var isStart = eventName.Contains("/Start", StringComparison.OrdinalIgnoreCase);
            var isStop = eventName.Contains("/Stop", StringComparison.OrdinalIgnoreCase)
                || eventName.Contains("Complete", StringComparison.OrdinalIgnoreCase);
            if (!isStart && !isStop)
            {
                return;
            }

            ulong urb = 0;
            ulong pipe = 0;
            try
            {
                urb = Convert.ToUInt64(data.PayloadByName("fid_URB_Ptr"));
            }
            catch
            {
                return;
            }

            try
            {
                pipe = Convert.ToUInt64(data.PayloadByName("fid_PipeHandle"));
            }
            catch
            {
                // USBPORT traces may omit the pipe field.
            }

            if (urb == 0)
            {
                return;
            }

            if (isStart)
            {
                pending[urb] = pipe;
                return;
            }

            if (pending.Remove(urb, out var startPipe) && startPipe != 0)
            {
                pipe = startPipe;
            }

            if (!byPipe.TryGetValue(pipe, out var list))
            {
                list = [];
                byPipe[pipe] = list;
            }

            list.Add(data.TimeStampRelativeMSec);
        };
        source.Process();

        return PickPollPipe(byPipe);
    }

    private static List<double> PickPollPipe(Dictionary<ulong, List<double>> byPipe)
    {
        List<double>? best = null;
        var bestScore = double.MinValue;
        foreach (var times in byPipe.Values)
        {
            if (times.Count < 30)
            {
                continue;
            }

            var ordered = times.OrderBy(v => v).ToList();
            var intervals = new List<double>();
            for (var i = 1; i < ordered.Count; i++)
            {
                var us = (ordered[i] - ordered[i - 1]) * 1000.0;
                if (us is > 0 and < 50_000)
                {
                    intervals.Add(us);
                }
            }

            if (intervals.Count < 24)
            {
                continue;
            }

            var median = intervals.OrderBy(v => v).ElementAt(intervals.Count / 2);
            var looksLikePoll = median is >= 80 and <= 250;
            var score = ordered.Count + (looksLikePoll ? 1_000_000 : 0);
            if (score > bestScore)
            {
                bestScore = score;
                best = ordered;
            }
        }

        return best ?? [];
    }

    private static object[] Buckets(List<double> intervalsUs)
    {
        (string Label, double MinHz, double MaxHz)[] edges =
        [
            (">8749 Hz", 8749, double.PositiveInfinity),
            ("8248-8749", 8248, 8749),
            ("8016-8248", 8016, 8248),
            ("7798-8016", 7798, 8016),
            ("7500-7798", 7500, 7798),
            ("7000-7500", 7000, 7500),
            ("5000-7000", 5000, 7000),
            ("<5000 Hz", 0, 5000)
        ];
        var rates = intervalsUs.ConvertAll(us => 1_000_000.0 / us);
        var total = Math.Max(rates.Count, 1);
        var counts = edges.Select(edge => rates.Count(hz => hz >= edge.MinHz && hz < edge.MaxHz)).ToArray();
        var maxCount = Math.Max(counts.DefaultIfEmpty(0).Max(), 1);
        return edges
            .Select((edge, i) => new
            {
                label = edge.Label,
                count = counts[i],
                pct = Math.Round(100.0 * counts[i] / total, 1),
                bar = (int)Math.Round(56.0 * counts[i] / maxCount)
            })
            .ToArray();
    }

    private static object Fail(string error) => new
    {
        error,
        hz = 0,
        samples = 0,
        buckets = Array.Empty<object>()
    };

    private static int RunLogman(string arguments, bool ignoreErrors = false)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "logman.exe",
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        });
        if (process is null)
        {
            return ignoreErrors ? 0 : 1;
        }

        process.WaitForExit(20_000);
        return ignoreErrors ? 0 : process.ExitCode;
    }
}
