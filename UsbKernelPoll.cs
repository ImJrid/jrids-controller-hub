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
            var result = Capture(Math.Clamp(seconds, 3, 15));
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
            if (times.Count < 20)
            {
                return Fail("Not enough USB interrupt reports. Move the sticks during the capture.");
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

            if (intervalsUs.Count < 16)
            {
                return Fail("USB interrupt timing was too sparse to measure.");
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
        var pending = new Dictionary<ulong, (double Start, ulong Pipe, bool Interrupt)>();
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
            try
            {
                urb = Convert.ToUInt64(data.PayloadByName("fid_URB_Ptr"));
            }
            catch
            {
                return;
            }

            if (urb == 0)
            {
                return;
            }

            var interrupt = eventName.Contains("BULK_OR_INTERRUPT", StringComparison.OrdinalIgnoreCase)
                || eventName.Contains("INTERRUPT", StringComparison.OrdinalIgnoreCase);

            if (isStart)
            {
                ulong pipe = 0;
                try
                {
                    pipe = Convert.ToUInt64(data.PayloadByName("fid_PipeHandle"));
                }
                catch
                {
                    // USBPORT traces may omit the pipe field.
                }

                pending[urb] = (data.TimeStampRelativeMSec, pipe, interrupt);
                return;
            }

            if (!pending.Remove(urb, out var tx))
            {
                return;
            }

            if (!tx.Interrupt && !interrupt)
            {
                return;
            }

            if (!byPipe.TryGetValue(tx.Pipe, out var list))
            {
                list = [];
                byPipe[tx.Pipe] = list;
            }

            list.Add(data.TimeStampRelativeMSec);
        };
        source.Process();

        return byPipe.Count == 0
            ? []
            : byPipe.Values.OrderByDescending(list => list.Count).First().OrderBy(v => v).ToList();
    }

    private static object[] Buckets(List<double> intervalsUs)
    {
        (string Label, double MinUs, double MaxUs)[] edges =
        [
            ("8000+ Hz", 0, 125),
            ("7700-8000", 125, 130),
            ("7200-7700", 130, 139),
            ("5000-7200", 139, 200),
            ("<5000 Hz", 200, double.MaxValue)
        ];
        var total = Math.Max(intervalsUs.Count, 1);
        return edges
            .Select(edge =>
            {
                var count = intervalsUs.Count(us => us >= edge.MinUs && us < edge.MaxUs);
                return new
                {
                    label = edge.Label,
                    count,
                    pct = Math.Round(100.0 * count / total, 1)
                };
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
