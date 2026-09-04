using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace JridsControllerHub;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new HubForm());
    }
}

internal sealed class HubForm : Form
{
    private const int WmNclButtonDown = 0xA1;
    private const int HtCaption = 0x2;
    private const string AppHost = "jrids.hub";

    private readonly WebView2 _webView = new()
    {
        Dock = DockStyle.Fill,
        DefaultBackgroundColor = Color.FromArgb(11, 12, 14)
    };

    public HubForm()
    {
        Text = "Jrids Controller Hub";
        Width = 1360;
        Height = 860;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.None;
        BackColor = Color.FromArgb(11, 12, 14);
        MinimumSize = new Size(980, 640);
        DoubleBuffered = true;

        var logoPath = Path.Combine(WebRoot, "assets", "jrids-logo.png");
        if (File.Exists(logoPath))
        {
            using var bitmap = new Bitmap(logoPath);
            Icon = Icon.FromHandle(bitmap.GetHicon());
        }

        Controls.Add(_webView);
        Load += async (_, _) =>
        {
            try
            {
                await InitializeWebViewAsync();
            }
            catch (WebView2RuntimeNotFoundException)
            {
                MessageBox.Show(
                    "Microsoft Edge WebView2 Runtime is required to run Jrids Controller Hub.\n\nInstall it from Microsoft, then open the app again.",
                    "Jrids Controller Hub",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                Close();
            }
        };
    }

    protected override void WndProc(ref Message m)
    {
        const int wmNcHitTest = 0x84;
        const int htLeft = 10;
        const int htRight = 11;
        const int htTop = 12;
        const int htTopLeft = 13;
        const int htTopRight = 14;
        const int htBottom = 15;
        const int htBottomLeft = 16;
        const int htBottomRight = 17;
        const int grip = 8;

        if (m.Msg == wmNcHitTest && WindowState == FormWindowState.Normal)
        {
            var cursor = PointToClient(Cursor.Position);
            bool left = cursor.X <= grip;
            bool right = cursor.X >= ClientSize.Width - grip;
            bool top = cursor.Y <= grip;
            bool bottom = cursor.Y >= ClientSize.Height - grip;

            if (top && left) { m.Result = htTopLeft; return; }
            if (top && right) { m.Result = htTopRight; return; }
            if (bottom && left) { m.Result = htBottomLeft; return; }
            if (bottom && right) { m.Result = htBottomRight; return; }
            if (left) { m.Result = htLeft; return; }
            if (right) { m.Result = htRight; return; }
            if (top) { m.Result = htTop; return; }
            if (bottom) { m.Result = htBottom; return; }
        }

        base.WndProc(ref m);
    }

    private static string WebRoot
    {
        get
        {
            var nextToExe = Path.Combine(AppContext.BaseDirectory, "wwwroot");
            if (Directory.Exists(nextToExe))
            {
                return nextToExe;
            }

            return Path.Combine(AppContext.BaseDirectory, "..", "..", "..");
        }
    }

    private async Task InitializeWebViewAsync()
    {
        var userData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "JridsControllerHub",
            "WebView2");
        Directory.CreateDirectory(userData);

        var env = await CoreWebView2Environment.CreateAsync(null, userData);
        await _webView.EnsureCoreWebView2Async(env);

        _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
        _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
        _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
        _webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
            AppHost,
            Path.GetFullPath(WebRoot),
            CoreWebView2HostResourceAccessKind.Allow);

        _webView.CoreWebView2.WebMessageReceived += OnWebMessage;
        _webView.CoreWebView2.NewWindowRequested += (_, args) =>
        {
            args.Handled = true;
            OpenExternal(args.Uri);
        };
        _webView.CoreWebView2.NavigationStarting += (_, args) =>
        {
            if (args.Uri.StartsWith($"https://{AppHost}/", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            args.Cancel = true;
            OpenExternal(args.Uri);
        };

        _webView.CoreWebView2.Navigate($"https://{AppHost}/index.html");
    }

    private void OnWebMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        string type;
        int pollIndex = 0;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(args.WebMessageAsJson);
            type = doc.RootElement.GetProperty("type").GetString() ?? "";
            if (doc.RootElement.TryGetProperty("index", out var indexEl) && indexEl.TryGetInt32(out var parsed))
            {
                pollIndex = parsed;
            }
        }
        catch
        {
            return;
        }

        if (type == "install-update")
        {
            _ = CheckForUpdatesAsync(apply: true);
            return;
        }

        if (type == "poll-measure")
        {
            _ = MeasurePollAsync(pollIndex);
            return;
        }

        BeginInvoke(() =>
        {
            switch (type)
            {
                case "min":
                    WindowState = FormWindowState.Minimized;
                    break;
                case "max":
                    WindowState = WindowState == FormWindowState.Maximized
                        ? FormWindowState.Normal
                        : FormWindowState.Maximized;
                    break;
                case "close":
                    Close();
                    break;
                case "uninstall":
                    StartUninstall();
                    break;
                case "drag":
                    ReleaseCapture();
                    _ = SendMessage(Handle, WmNclButtonDown, HtCaption, 0);
                    break;
            }
        });
    }

    private bool _updateInProgress;
    private CancellationTokenSource? _pollCts;

    private async Task MeasurePollAsync(int index)
    {
        _pollCts?.Cancel();
        _pollCts = new CancellationTokenSource();
        var token = _pollCts.Token;
        index = Math.Clamp(index, 0, 3);
        try
        {
            await Task.Run(() => SampleXInputPoll(index, token));
        }
        catch (OperationCanceledException)
        {
            // Replaced by a newer measure, or the window closed.
        }
    }

    private void SampleXInputPoll(int index, CancellationToken token)
    {
        var intervals = new List<double>(32768);
        var clock = Stopwatch.StartNew();
        var lastPacket = uint.MaxValue;
        var lastTicks = 0L;
        var connected = false;
        var nextUi = TimeSpan.Zero;

        while (clock.Elapsed < TimeSpan.FromSeconds(5) && !token.IsCancellationRequested)
        {
            uint status;
            XInputState state;
            try
            {
                status = XInputGetState((uint)index, out state);
            }
            catch (DllNotFoundException)
            {
                PostWebJson(new { type = "poll-result", error = "xinput-miss", samples = 0, hz = 0, buckets = Array.Empty<object>() });
                return;
            }

            if (status == 0)
            {
                connected = true;
                if (state.dwPacketNumber != lastPacket)
                {
                    if (lastPacket != uint.MaxValue)
                    {
                        var dtMs = (clock.ElapsedTicks - lastTicks) * 1000.0 / Stopwatch.Frequency;
                        if (dtMs is > 0.04 and < 40)
                        {
                            intervals.Add(dtMs);
                        }
                    }

                    lastPacket = state.dwPacketNumber;
                    lastTicks = clock.ElapsedTicks;
                }
            }

            if (clock.Elapsed >= nextUi)
            {
                nextUi = clock.Elapsed + TimeSpan.FromMilliseconds(200);
                PostWebJson(PollPayload("poll-progress", intervals, done: false, connected ? null : "waiting"));
            }
        }

        if (token.IsCancellationRequested)
        {
            return;
        }

        if (!connected || intervals.Count < 8)
        {
            PostWebJson(new { type = "poll-result", error = "xinput-miss", samples = intervals.Count, hz = 0, buckets = Array.Empty<object>() });
            return;
        }

        PostWebJson(PollPayload("poll-result", intervals, done: true, error: null));
    }

    private static object PollPayload(string type, List<double> intervalsMs, bool done, string? error)
    {
        var rates = intervalsMs.ConvertAll(ms => 1000.0 / ms);
        var hz = rates.Count == 0 ? 0 : Median(rates);
        return new
        {
            type,
            hz = Math.Round(hz),
            samples = rates.Count,
            done,
            error,
            buckets = PollBuckets(rates)
        };
    }

    private static object[] PollBuckets(List<double> rates)
    {
        (string Label, double Min, double Max)[] edges =
        [
            ("8000+ Hz", 8000, double.PositiveInfinity),
            ("4000-8000", 4000, 8000),
            ("2000-4000", 2000, 4000),
            ("1000-2000", 1000, 2000),
            ("500-1000", 500, 1000),
            ("250-500", 250, 500),
            ("125-250", 125, 250),
            ("<125 Hz", 0, 125)
        ];
        var total = Math.Max(rates.Count, 1);
        return edges
            .Select(edge =>
            {
                var count = rates.Count(hz => hz >= edge.Min && hz < edge.Max);
                return new
                {
                    label = edge.Label,
                    count,
                    pct = Math.Round(100.0 * count / total, 1)
                };
            })
            .ToArray();
    }

    private static double Median(List<double> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        var mid = sorted.Count / 2;
        return sorted.Count % 2 == 0 ? (sorted[mid - 1] + sorted[mid]) / 2 : sorted[mid];
    }

    private async Task CheckForUpdatesAsync(bool apply)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "JridsControllerHub");
            http.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/vnd.github+json");
            var json = await http.GetStringAsync("https://api.github.com/repos/ImJrid/jrids-controller-hub/releases/latest");
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var tag = doc.RootElement.GetProperty("tag_name").GetString() ?? "";
            var html = doc.RootElement.GetProperty("html_url").GetString() ?? "";
            var setupUrl = FindSetupAssetUrl(doc.RootElement)
                ?? $"https://github.com/ImJrid/jrids-controller-hub/releases/download/{tag}/JridsControllerHubSetup.exe";
            var newer = IsNewerThanCurrent(tag);
            var installing = apply && newer && CanInstallUpdate() && !_updateInProgress;
            PostWebJson(new { type = "update-result", tag, html, installing });

            if (!installing)
            {
                return;
            }

            _updateInProgress = true;
            var setupPath = Path.Combine(Path.GetTempPath(), "JridsControllerHubSetup.exe");
            await using (var output = File.Create(setupPath))
            await using (var input = await http.GetStreamAsync(setupUrl))
            {
                await input.CopyToAsync(output);
            }

            BeginInvoke(() =>
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = setupPath,
                    Arguments = "/VERYSILENT /NORESTART /SUPPRESSMSGBOXES /CLOSEAPPLICATIONS",
                    UseShellExecute = true
                });
                Close();
            });
        }
        catch
        {
            _updateInProgress = false;
            const string payload = """{"type":"update-result","error":"offline"}""";
            BeginInvoke(() => _webView.CoreWebView2.PostWebMessageAsJson(payload));
        }
    }

    private void PostWebJson(object payload)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(payload);
        BeginInvoke(() => _webView.CoreWebView2.PostWebMessageAsJson(json));
    }

    private bool CanInstallUpdate() => FindUninstaller() is not null && !Debugger.IsAttached;

    private static string? FindSetupAssetUrl(System.Text.Json.JsonElement release)
    {
        if (!release.TryGetProperty("assets", out var assets) || assets.ValueKind != System.Text.Json.JsonValueKind.Array)
        {
            return null;
        }

        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
            if (!string.Equals(name, "JridsControllerHubSetup.exe", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return asset.TryGetProperty("browser_download_url", out var urlEl) ? urlEl.GetString() : null;
        }

        return null;
    }

    private static bool IsNewerThanCurrent(string tag)
    {
        var current = typeof(HubForm).Assembly.GetName().Version ?? new Version(0, 0, 0, 0);
        var parts = tag.TrimStart('v', 'V')
            .Split('.', '-', '+')
            .Select(part => int.TryParse(part, out var number) ? number : (int?)null)
            .Where(number => number.HasValue)
            .Select(number => number!.Value)
            .ToList();
        while (parts.Count < 4)
        {
            parts.Add(0);
        }

        return new Version(parts[0], parts[1], parts[2], parts[3]) > current;
    }

    private void StartUninstall()
    {
        var uninstaller = FindUninstaller();
        if (uninstaller is null)
        {
            MessageBox.Show(
                this,
                "This copy wasn't installed with Setup. Run JridsControllerHubSetup.exe and choose Uninstall, or delete the app folder.",
                "Uninstall",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        Process.Start(new ProcessStartInfo(uninstaller) { UseShellExecute = true });
        Close();
    }

    private static string? FindUninstaller()
    {
        try
        {
            var local = Directory.GetFiles(AppContext.BaseDirectory, "unins*.exe")
                .FirstOrDefault(file => Path.GetFileName(file).StartsWith("unins", StringComparison.OrdinalIgnoreCase));
            if (local is not null)
            {
                return local;
            }
        }
        catch
        {
            // Portable copies have no Inno uninstaller beside the exe.
        }

        const string keyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\{8F3C2A91-4B6E-4D1A-9C7F-2E5A8B0D1C44}_is1";
        foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
        {
            try
            {
                using var key = hive.OpenSubKey(keyPath);
                var parsed = ParseUninstallPath(key?.GetValue("UninstallString") as string);
                if (parsed is not null && File.Exists(parsed))
                {
                    return parsed;
                }
            }
            catch
            {
                // Try the other hive.
            }
        }

        return null;
    }

    private static string? ParseUninstallPath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        value = value.Trim();
        if (value.StartsWith('"'))
        {
            var end = value.IndexOf('"', 1);
            if (end > 1)
            {
                return value[1..end];
            }
        }

        var exe = value.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        return exe >= 0 ? value[..(exe + 4)].Trim('"') : null;
    }

    private static void OpenExternal(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
    private static extern uint XInputGetState14(uint dwUserIndex, out XInputState pState);

    [DllImport("xinput9_1_0.dll", EntryPoint = "XInputGetState")]
    private static extern uint XInputGetState910(uint dwUserIndex, out XInputState pState);

    private static bool _xinputLegacy;

    private static uint XInputGetState(uint index, out XInputState state)
    {
        if (_xinputLegacy)
        {
            return XInputGetState910(index, out state);
        }

        try
        {
            return XInputGetState14(index, out state);
        }
        catch (DllNotFoundException)
        {
            _xinputLegacy = true;
            return XInputGetState910(index, out state);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputGamepad
    {
        public ushort wButtons;
        public byte bLeftTrigger;
        public byte bRightTrigger;
        public short sThumbLX;
        public short sThumbLY;
        public short sThumbRX;
        public short sThumbRY;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputState
    {
        public uint dwPacketNumber;
        public XInputGamepad Gamepad;
    }
}
