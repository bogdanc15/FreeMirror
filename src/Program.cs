// SPDX-License-Identifier: GPL-3.0-or-later
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace FreeMirror;

static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if (args.Contains("--self-test"))
        {
            try { Tests.Run(false); return 0; }
            catch (Exception ex) { File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "test-result.txt"), ex.ToString()); return 1; }
        }
        if (args.Length == 2 && args[0] == "--test-video-window")
        {
            try { Tests.VideoWindow(int.Parse(args[1])); return 0; }
            catch (Exception ex) { File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "live-window-test.txt"), ex.ToString()); return 1; }
        }
        using var mutex = new Mutex(true, "Local\\FreeMirror.Desktop", out bool first);
        if (!first) { MessageBox.Show("FreeMirror is already open."); return 1; }
        if (args.Contains("--integration-test"))
        {
            try { Tests.Run(args.Contains("--integration-test")); return 0; }
            catch (Exception ex) { File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "test-result.txt"), ex.ToString()); return 1; }
        }
        Application.Run(new MirrorWindow());
        return 0;
    }
}

sealed record StreamSettings(string Resolution = "1080p", int Fps = 60, bool LowDelay = true)
{
    public string Arguments()
    {
        string size = Resolution switch { "720p" => "1280x720", "1080p" => "1920x1080", "1440p" => "2560x1440", "4K" => "3840x2160", "8K" => "7680x4320", _ => throw new ArgumentException("Choose a resolution from 720p to 8K.") };
        if (Fps is not (30 or 60)) throw new ArgumentException("Choose 30 or 60 fps.");
        return $"-s {size}@{Fps} -fps {Fps}" + (Resolution is "1440p" or "4K" or "8K" ? " -h265" : "") + (LowDelay ? " -vsync no" : "");
    }
}

sealed class Receiver : IDisposable
{
    public const int Port = 55300;
    readonly string config = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "leapbtw", "uxplay-windows", "arguments.txt");
    byte[]? previous;
    bool ownsConfig;
    Process? process;
    readonly FullScreenWindow fullScreen = new();
    public bool IsFullScreen => fullScreen.Active;
    public bool HasVideo => Running && FullScreenWindow.FindVideoWindow(process!.Id) != 0;
    public void ToggleFullScreen()
    {
        if (!Running) throw new InvalidOperationException("Click Start receiving and connect your Apple device first.");
        fullScreen.Toggle(FullScreenWindow.FindVideoWindow(process!.Id));
    }
    public bool Running => process is { HasExited: false };
    public static string ValidateName(string name)
    {
        name = name.Trim().Replace(' ', '-');
        if (!Regex.IsMatch(name, @"\A[\p{L}\p{N}][\p{L}\p{N}_.-]{0,47}\z"))
            throw new ArgumentException("Use 1–48 letters, numbers, spaces, dots or hyphens. Start with a letter or number.");
        return name;
    }
    public void Start(string name, StreamSettings? settings = null)
    {
        if (Running) return;
        name = ValidateName(name);
        string streamArguments = (settings ?? new StreamSettings()).Arguments();
        var executable = Path.Combine(AppContext.BaseDirectory, "receiver", "uxplay-windows.exe");
        if (!File.Exists(executable)) throw new FileNotFoundException("The receiver is not installed. Close FreeMirror, run Setup-Receiver.ps1 beside FreeMirror.exe, then reopen the app. See README.txt for manual setup.");
        if (Process.GetProcessesByName("uxplay-windows").Length != 0)
            throw new InvalidOperationException("Quit the existing UxPlay receiver from its tray icon, then try again.");
        if (System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners().Any(p => p.Port >= Port && p.Port <= Port + 2))
            throw new InvalidOperationException("The receiver ports are busy. Close the other receiver and try again.");
        Directory.CreateDirectory(Path.GetDirectoryName(config)!);
        previous = File.Exists(config) ? File.ReadAllBytes(config) : null;
        ownsConfig = true;
        try
        {
            File.WriteAllText(config, $"-n {name} -nh -p {Port} -vs d3d11videosink -as autoaudiosink {streamArguments}", new UTF8Encoding(false));
            process = Process.Start(new ProcessStartInfo(executable) { WorkingDirectory = Path.GetDirectoryName(executable)!, UseShellExecute = false, CreateNoWindow = true });
            if (process == null) throw new InvalidOperationException("Windows could not start the receiver.");
        }
        catch { Stop(); throw; }
    }
    public static async Task<bool> Probe()
    {
        try
        {
            using var timeout = new CancellationTokenSource(1000);
            using var client = new TcpClient();
            await client.ConnectAsync("127.0.0.1", Port + 1, timeout.Token).ConfigureAwait(false);
            using var stream = client.GetStream();
            await stream.WriteAsync(Encoding.ASCII.GetBytes("GET /info RTSP/1.0\r\nCSeq: 1\r\n\r\n"), timeout.Token).ConfigureAwait(false);
            var data = new byte[4096];
            int count = await stream.ReadAsync(data, timeout.Token).ConfigureAwait(false);
            return Encoding.ASCII.GetString(data, 0, count).Contains("200 OK");
        }
        catch (Exception ex) when (ex is SocketException or IOException or OperationCanceledException) { return false; }
    }
    public void Stop()
    {
        if (Running) { process!.Kill(entireProcessTree: true); process.WaitForExit(5000); }
        process?.Dispose(); process = null;
        fullScreen.Reset();
        if (ownsConfig)
        {
            if (previous == null) File.Delete(config); else File.WriteAllBytes(config, previous);
            ownsConfig = false;
        }
    }
    public void Dispose() => Stop();
}

sealed class MirrorWindow : Form
{
    readonly Receiver receiver = new();
    readonly TextBox name = new() { Name = "receiverName", Text = "FreeMirror", Dock = DockStyle.Fill, AccessibleName = "Receiver name" };
    readonly Label status = new() { Text = "Stopped", AutoSize = true, ForeColor = Color.FromArgb(75, 85, 99) };
    readonly Button start = new() { Text = "Start receiving", AutoSize = true, Padding = new Padding(16, 8, 16, 8) };
    readonly Button stop = new() { Text = "Stop", AutoSize = true, Enabled = false, Padding = new Padding(16, 8, 16, 8) };
    readonly Button fullscreen = new() { Text = "Full screen", AutoSize = true, Enabled = false, Padding = new Padding(16, 8, 16, 8) };
    readonly ComboBox resolution = new() { Name = "resolution", DropDownStyle = ComboBoxStyle.DropDownList, DrawMode = DrawMode.OwnerDrawFixed, Width = 150, AccessibleName = "Requested resolution" };
    readonly ComboBox fps = new() { DropDownStyle = ComboBoxStyle.DropDownList, DrawMode = DrawMode.OwnerDrawFixed, Width = 120, AccessibleName = "Maximum frame rate" };
    readonly CheckBox lowDelay = new() { Text = "Low delay (prioritize picture response over audio sync)", Checked = true, AutoSize = true, Margin = new Padding(0, 12, 0, 4) };
    readonly System.Windows.Forms.Timer timer = new() { Interval = 1800 };
    bool checking;
    int checks;
    public MirrorWindow()
    {
        Text = "FreeMirror"; ClientSize = new Size(660, 710); MinimumSize = new Size(676, 749);
        AutoScaleMode = AutoScaleMode.Dpi; Font = new Font("Segoe UI", 11); BackColor = Color.FromArgb(247, 248, 250);
        StartPosition = FormStartPosition.CenterScreen;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(32), ColumnCount = 1, RowCount = 12, AutoScroll = true };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Controls.Add(layout);
        layout.Controls.Add(new Label { Text = "FreeMirror", Font = new Font("Segoe UI", 28, FontStyle.Bold), AutoSize = true });
        layout.Controls.Add(new Label { Text = "Your iPhone or iPad, on your PC.", AutoSize = true, Margin = new Padding(0, 0, 0, 24) });
        layout.Controls.Add(new Label { Text = "Receiver name", AutoSize = true }); layout.Controls.Add(name);
        resolution.Items.AddRange(["720p", "1080p", "1440p", "4K", "8K (experimental)"]); resolution.SelectedIndex = 1;
        resolution.Width = 180;
        fps.Items.AddRange(["60 fps", "30 fps"]); fps.SelectedIndex = 0;
        foreach (var choice in new[] { resolution, fps })
            choice.DrawItem += (_, e) =>
            {
                e.DrawBackground();
                if (e.Index >= 0) TextRenderer.DrawText(e.Graphics, choice.Items[e.Index]?.ToString() ?? "", choice.Font, e.Bounds, choice.Enabled ? e.ForeColor : SystemColors.GrayText, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                e.DrawFocusRectangle();
            };
        var quality = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 16, 0, 0) };
        foreach (var item in new[] { ("Resolution", resolution), ("Frame rate", fps) })
        {
            var column = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, Margin = new Padding(0, 0, 24, 0) };
            column.Controls.Add(new Label { Text = item.Item1, AutoSize = true }); column.Controls.Add(item.Item2); quality.Controls.Add(column);
        }
        layout.Controls.Add(quality); layout.Controls.Add(lowDelay);
        layout.Controls.Add(new Label { Text = "Your device chooses the actual quality; 8K is experimental.\nTry 720p/60 for the least delay. Higher resolutions may add lag.", AutoSize = true, ForeColor = Color.FromArgb(75, 85, 99), Margin = new Padding(0, 4, 0, 0) });
        var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 16, 0, 12) };
        buttons.Controls.AddRange([start, stop, fullscreen]); layout.Controls.Add(buttons); layout.Controls.Add(status);
        layout.Controls.Add(new Label { Text = "1. Connect your Apple device and PC to the same Wi-Fi.\n2. Click Start receiving.\n3. Open Control Center → Screen Mirroring.\n4. Choose the receiver name above.", AutoSize = true, Margin = new Padding(0, 24, 0, 16) });
        layout.Controls.Add(new Label { Text = "The mirrored picture opens in a separate window.\nIn full screen, use Alt+Tab to return to these controls.\nAllow the receiver on your private network if Windows asks.", AutoSize = true, ForeColor = Color.FromArgb(75, 85, 99) });
        var links = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 16, 0, 0) };
        var help = new LinkLabel { Text = "Help & credits", AutoSize = true }; links.Controls.Add(help); layout.Controls.Add(links);
        help.LinkClicked += (_, _) => Process.Start(new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "README.txt")) { UseShellExecute = true });
        start.Click += (_, _) =>
        {
            try { name.Text = Receiver.ValidateName(name.Text); receiver.Start(name.Text, SelectedSettings); checks = 0; SetState(true, "Starting receiver…"); timer.Start(); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Could not start", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        };
        stop.Click += (_, _) => { timer.Stop(); receiver.Stop(); SetState(false, "Stopped"); };
        fullscreen.Click += (_, _) =>
        {
            try { receiver.ToggleFullScreen(); fullscreen.Text = receiver.IsFullScreen ? "Exit full screen" : "Full screen"; }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Full screen", MessageBoxButtons.OK, MessageBoxIcon.Information); }
        };
        timer.Tick += async (_, _) =>
        {
            if (checking) return;
            checking = true;
            try
            {
                bool videoActive = receiver.HasVideo;
                bool ready = videoActive || (receiver.Running && await Receiver.Probe());
                fullscreen.Text = receiver.IsFullScreen ? "Exit full screen" : "Full screen";
                if (!receiver.Running) { timer.Stop(); receiver.Stop(); SetState(false, "Receiver exited. Click Start to try again."); }
                else if (stop.Enabled) status.Text = videoActive ? "Mirroring — " + name.Text : ready ? "Ready — choose " + name.Text + " on your device" : ++checks > 10 ? "Waiting for receiver. Check its tray icon for a message." : "Starting receiver…";
            }
            finally { checking = false; }
        };
        FormClosed += (_, _) => { timer.Stop(); receiver.Dispose(); timer.Dispose(); };
    }
    public StreamSettings SelectedSettings => new(resolution.SelectedIndex == 4 ? "8K" : (string)resolution.SelectedItem!, fps.SelectedIndex == 0 ? 60 : 30, lowDelay.Checked);
    void SetState(bool running, string message) { start.Enabled = !running; stop.Enabled = running; fullscreen.Enabled = running; fullscreen.Text = receiver.IsFullScreen ? "Exit full screen" : "Full screen"; name.Enabled = !running; resolution.Enabled = !running; fps.Enabled = !running; lowDelay.Enabled = !running; status.Text = message; }
}

static class Tests
{
    public static void VideoWindow(int processId)
    {
        using var target = Process.GetProcessById(processId);
        if (target.ProcessName != "uxplay-windows") throw new Exception("Live test requires the receiver process");
        nint handle = FullScreenWindow.FindVideoWindow(processId);
        if (handle == 0) throw new Exception("Live receiver video window was not detected");
        var before = FullScreenWindow.Bounds(handle);
        var fullScreen = new FullScreenWindow();
        try
        {
            fullScreen.Toggle(handle);
            if (FullScreenWindow.Bounds(handle) != Screen.FromHandle(handle).Bounds) throw new Exception("Live video did not fill the display");
        }
        finally { if (fullScreen.Active) fullScreen.Toggle(handle); }
        if (FullScreenWindow.Bounds(handle) != before) throw new Exception("Live video window was not restored");
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "live-window-test.txt"), "PASS: detected live receiver video; filled display; restored original window bounds");
    }
    public static void Run(bool integration)
    {
        if (!new StreamSettings("8K", 60, true).Arguments().Contains("-s 7680x4320@60 -fps 60 -h265")) throw new Exception("8K60 HEVC request failed");
        foreach (string resolution in new[] { "720p", "1080p", "1440p", "4K", "8K" })
        foreach (int fps in new[] { 30, 60 })
        {
            string args = new StreamSettings(resolution, fps, true).Arguments();
            if (!args.Contains($"-fps {fps}") || !args.Contains($"@{fps}")) throw new Exception("Frame rate request is missing");
            bool hevc = resolution is "1440p" or "4K" or "8K";
            if (args.Contains("-h265") != hevc) throw new Exception("High-resolution codec selection failed");
            if (!args.Contains("-vsync no")) throw new Exception("Low-delay mode was not applied");
        }
        if (!new StreamSettings("4K", 60, false).Arguments().Contains("-s 3840x2160@60")) throw new Exception("4K request failed");
        if (new StreamSettings("1080p", 60, false).Arguments().Contains("-vsync no")) throw new Exception("Balanced mode must retain audio/video synchronization");
        try { new StreamSettings("16K", 60, true).Arguments(); throw new Exception("Invalid resolution accepted"); } catch (ArgumentException) { }
        try { new StreamSettings("1080p", 120, true).Arguments(); throw new Exception("Invalid frame rate accepted"); } catch (ArgumentException) { }
        if (Receiver.ValidateName("  Living Room  ") != "Living-Room") throw new Exception("Name normalization failed");
        foreach (string invalid in new[] { "", "-n evil", "bad\nname", new string('a', 49), "name/other" })
        {
            try { Receiver.ValidateName(invalid); } catch (ArgumentException) { continue; }
            throw new Exception("Accepted invalid name: " + invalid);
        }
        using (var window = new MirrorWindow())
        {
            if (window.SelectedSettings != new StreamSettings("1080p", 60, true)) throw new Exception("UI defaults are incorrect");
            if (((TextBox)window.Controls.Find("receiverName", true).Single()).Text != "FreeMirror") throw new Exception("Default receiver name must not include a personal computer name");
            var resolutionChoice = (ComboBox)window.Controls.Find("resolution", true).Single();
            resolutionChoice.SelectedItem = "8K (experimental)";
            if (window.SelectedSettings.Resolution != "8K") throw new Exception("Experimental 8K selection did not reach receiver settings");
            resolutionChoice.SelectedItem = "1080p";
            window.Show(); Application.DoEvents();
            var originalBounds = window.Bounds;
            var fullscreenTest = new FullScreenWindow();
            fullscreenTest.Toggle(window.Handle);
            if (!fullscreenTest.Active || window.Bounds != Screen.FromHandle(window.Handle).Bounds) throw new Exception("Full-screen toggle did not fill the display");
            fullscreenTest.Toggle(window.Handle);
            if (fullscreenTest.Active || window.Bounds != originalBounds) throw new Exception("Full-screen toggle did not restore the window");
            using var bitmap = new Bitmap(window.Width, window.Height);
            window.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
            bitmap.Save(Path.Combine(AppContext.BaseDirectory, "preview.png"));
            window.Close();
        }
        if (integration)
        {
            string config = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "leapbtw", "uxplay-windows", "arguments.txt");
            byte[]? before = File.Exists(config) ? File.ReadAllBytes(config) : null;
            using var receiver = new Receiver();
            foreach (var profile in new[] { new StreamSettings("720p", 60, true), new StreamSettings("1080p", 60, true), new StreamSettings("1440p", 60, true), new StreamSettings("4K", 60, true), new StreamSettings("4K", 30, false), new StreamSettings("8K", 30, true), new StreamSettings("8K", 60, true) })
            {
            receiver.Start("FreeMirror-Test", profile);
            if (!File.ReadAllText(config).Contains(profile.Arguments())) throw new Exception("Requested stream settings were not applied");
            bool ready = false;
            for (int attempt = 0; attempt < 30; attempt++)
            {
                if (!receiver.Running) throw new Exception("Receiver exited during startup");
                if (Receiver.Probe().GetAwaiter().GetResult()) { ready = true; break; }
                Thread.Sleep(500);
            }
            if (!ready) throw new Exception("Receiver did not answer RTSP /info within 30 attempts");
            receiver.Stop();
            if (Receiver.Probe().GetAwaiter().GetResult()) throw new Exception("Receiver is still listening after Stop");
            if (before == null ? File.Exists(config) : !File.ReadAllBytes(config).SequenceEqual(before))
                throw new Exception("Existing receiver settings were not restored");
            }
        }
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "test-result.txt"), "PASS: name validation; resolution, codec, frame rate and delay settings; invalid setting rejection; native full-screen and window-size restoration; form rendering" + (integration ? "; real receiver startup for 720p60, 1080p60, 1440p60, 4K60, balanced 4K30, 8K30 and 8K60 requests; RTSP /info 200 OK for each; stop closes listener; previous settings restored; actual 8K video remains unverified" : ""));
    }
}
