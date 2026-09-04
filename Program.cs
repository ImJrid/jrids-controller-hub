using System.Diagnostics;
using System.Runtime.InteropServices;
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
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(args.WebMessageAsJson);
            type = doc.RootElement.GetProperty("type").GetString() ?? "";
        }
        catch
        {
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
                case "drag":
                    ReleaseCapture();
                    _ = SendMessage(Handle, WmNclButtonDown, HtCaption, 0);
                    break;
            }
        });
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
}
