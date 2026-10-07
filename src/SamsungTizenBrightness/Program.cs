// SPDX-FileCopyrightText: 2026 dttutty
// SPDX-License-Identifier: GPL-3.0-only

using System.Net;
using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Management;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace SamsungTizenBrightness;

internal static class Program
{
    private const string OpenSignalName = @"Local\M70BBrightness.Open";
    private static Mutex? _singleInstance;

    [STAThread]
    private static void Main(string[] args)
    {
        AppDiagnostics.Log(
            $"process entry; pid={Environment.ProcessId}; version={Application.ProductVersion}; " +
            $"args={string.Join(' ', args.Select(arg => $"[{arg}]"))}");
        ApplicationConfiguration.Initialize();
        L.Initialize(LocalState.TryLoadLanguage());

        if (args.Any(arg => string.Equals(
                arg,
                "--register-startup",
                StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                StartupRegistration.SetEnabled(true);
                AppDiagnostics.Log("startup registration requested; completed");
            }
            catch (Exception ex)
            {
                AppDiagnostics.Log(
                    $"startup registration requested; failed; {ex.GetType().Name}: {ex.Message}");
                Environment.ExitCode = 1;
            }
            return;
        }

        _singleInstance = new Mutex(
            true,
            @"Local\M70BBrightness.SingleInstance",
            out bool createdNew);
        if (!createdNew)
        {
            AppDiagnostics.Log("duplicate process entry; signaling existing instance");
            try
            {
                using EventWaitHandle existingSignal = EventWaitHandle.OpenExisting(OpenSignalName);
                existingSignal.Set();
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                // The first instance may still be starting. Its tray icon will
                // become available normally without opening a duplicate copy.
            }
            return;
        }

        try
        {
            StartupRegistration.MigrateLegacyIfNeeded();
            string? startupHost = ReadOption(args, "--host");
            string? host = startupHost ?? LocalState.TryLoadHost();
            if (startupHost is not null)
            {
                LocalState.SaveHost(startupHost);
                AppDiagnostics.Log("saved display IP loaded from startup task backup");
            }
            if (host is null)
            {
                host = ConnectionSetupPrompt.Show();
                if (host is null)
                    return;
                LocalState.SaveHost(host);
                StartupRegistration.RefreshHostBackup(host);
            }

            bool openOnLaunch = args.Any(arg =>
                string.Equals(arg, "--open", StringComparison.OrdinalIgnoreCase));
            bool startedWithWindows = args.Any(arg =>
                string.Equals(arg, "--startup", StringComparison.OrdinalIgnoreCase));
            using var openSignal = new EventWaitHandle(
                false,
                EventResetMode.AutoReset,
                OpenSignalName);
            Application.Run(new TrayContext(
                host,
                openOnLaunch,
                startedWithWindows,
                openSignal));
            AppDiagnostics.Log("process exit; normal");
        }
        catch (Exception ex)
        {
            AppDiagnostics.Log($"process exit; startup fatal; {ex.GetType().Name}: {ex.Message}");
            Environment.ExitCode = 1;
            MessageBox.Show(
                L.T("StartupFatal", ex.Message),
                L.T("AppTitle"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private static string? ReadOption(string[] args, string option)
    {
        for (int index = 0; index < args.Length - 1; index++)
        {
            if (string.Equals(args[index], option, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(args[index + 1]))
                return args[index + 1].Trim();
        }
        return null;
    }
}

internal static class ConnectionSetupPrompt
{
    public static string? Show(string? currentHost = null)
    {
        using Form dialog = CreateDialog(currentHost, currentHost is null || StartupRegistration.IsEnabled());
        var input = (TextBox)dialog.Controls["DisplayHost"]!;
        var startWithWindows = (CheckBox)dialog.Controls["StartWithWindows"]!;
        while (dialog.ShowDialog() == DialogResult.OK)
        {
            string host = input.Text.Trim();
            if (IPAddress.TryParse(host, out _) || Uri.CheckHostName(host) != UriHostNameType.Unknown)
            {
                try { StartupRegistration.SetEnabled(startWithWindows.Checked); }
                catch (Exception error) when (error is UnauthorizedAccessException or IOException)
                {
                    MessageBox.Show(L.T("StartupSaveWarning", error.Message), dialog.Text,
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                return host;
            }
            MessageBox.Show(L.T("InvalidHost"), L.T("AppTitle"),
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        return null;
    }

    // Pure UI construction, also used by offline layout regression tests.
    internal static Form CreateDialog(string? currentHost, bool startupEnabled)
    {
        var dialog = new Form
        {
            Text = L.T("SetupTitle"),
            ClientSize = new Size(620, 460),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false, MinimizeBox = false,
            StartPosition = FormStartPosition.CenterScreen,
            Font = new Font("Microsoft YaHei UI", 9F)
        };
        var title = new Label
        {
            Text = L.T("SetupHeading"), AutoSize = true,
            Font = new Font(dialog.Font.FontFamily, 14F, FontStyle.Bold),
            Location = new Point(22, 18)
        };
        var intro = new Label
        {
            Text = L.T("SetupIntro"), Size = new Size(574, 42), Location = new Point(24, 54)
        };
        var hostLabel = new Label
        {
            Text = L.T("SetupIpLabel"), AutoSize = false, Size = new Size(574, 44),
            Font = new Font(dialog.Font, FontStyle.Bold), Location = new Point(24, 102)
        };
        var input = new TextBox
        {
            Name = "DisplayHost",
            PlaceholderText = L.T("SetupIpExample"), Text = currentHost ?? string.Empty,
            Location = new Point(25, 150), Size = new Size(570, 27)
        };
        var instructions = new GroupBox
        {
            Text = L.T("SetupIpRemoteHeading"),
            Location = new Point(20, 194), Size = new Size(580, 176)
        };
        instructions.Controls.Add(new Label
        {
            Name = "IpRemoteGuide",
            Text = L.T("SetupIpRemoteBody"), Size = new Size(540, 140),
            Location = new Point(18, 25)
        });
        var startWithWindows = new CheckBox
        {
            Name = "StartWithWindows",
            Text = L.T("SetupStartup"), AutoSize = true,
            Checked = startupEnabled,
            Location = new Point(24, 384)
        };
        var ok = new Button
        {
            Text = L.T("SaveContinue"), DialogResult = DialogResult.OK,
            Location = new Point(388, 420), Size = new Size(104, 32)
        };
        var cancel = new Button
        {
            Text = L.T("Cancel"), DialogResult = DialogResult.Cancel,
            Location = new Point(500, 420), Size = new Size(96, 32)
        };
        dialog.Controls.AddRange([title, intro, hostLabel, input, instructions, startWithWindows, ok, cancel]);
        dialog.AcceptButton = ok;
        dialog.CancelButton = cancel;
        return dialog;
    }
}

internal static class StartupRegistration
{
    private const string ValueName = "SamsungTizenBrightness";
    private const string TaskName = @"\Samsung Tizen Brightness";
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ApprovalKeyPath =
        @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

    public static bool IsEnabled()
    {
        if (TryReadTaskXml(out string taskXml) && StartupPolicy.ReadTask(taskXml) is { } task)
            return task.Enabled;

        return IsLegacyEnabled();
    }

    public static void MigrateLegacyIfNeeded()
    {
        try
        {
            string taskXml = "";
            StartupTaskState? task = TryReadTaskXml(out taskXml) ? StartupPolicy.ReadTask(taskXml) : null;
            if (!StartupPolicy.ShouldMigrate(task, IsLegacyEnabled(), Application.ExecutablePath))
                return;

            if (task is not null)
            {
                // Retarget only the executable; preserve host backup, user, retry
                // policy and every other setting from the existing registration.
                WriteTask(StartupPolicy.RetargetTask(taskXml, Application.ExecutablePath));
                AppDiagnostics.Log("startup task retargeted to current installation");
            }
            else
            {
                // Never overwrite a same-name task belonging to something else.
                if (!string.IsNullOrWhiteSpace(taskXml)) return;
                SetEnabled(true);
                AppDiagnostics.Log("startup registration migrated from Run key to scheduled task");
            }
        }
        catch (Exception error)
        {
            // Keep the working Run entry when migration is unavailable. A later
            // launch can retry without breaking the user's existing autostart.
            AppDiagnostics.Log(
                $"startup registration migration failed; {error.GetType().Name}: {error.Message}");
        }
    }

    private static bool IsLegacyEnabled()
    {
        using RegistryKey? runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        string? command = runKey?.GetValue(ValueName) as string;
        if (!StartupPolicy.IsLegacyCommand(command))
            return false;

        using RegistryKey? approvalKey = Registry.CurrentUser.OpenSubKey(ApprovalKeyPath);
        if (approvalKey?.GetValue(ValueName) is byte[] approval &&
            approval.Length > 0 && approval[0] == 3)
            return false;
        return true;
    }

    public static void SetEnabled(bool enabled)
    {
        if (enabled)
        {
            CreateOrUpdateTask();
            using RegistryKey runKey = Registry.CurrentUser.CreateSubKey(
                RunKeyPath,
                writable: true);
            runKey.DeleteValue(ValueName, throwOnMissingValue: false);
            using RegistryKey approvalKey = Registry.CurrentUser.CreateSubKey(
                ApprovalKeyPath,
                writable: true);
            approvalKey.DeleteValue(ValueName, throwOnMissingValue: false);
        }
        else
        {
            DeleteTask();
            using RegistryKey runKey = Registry.CurrentUser.CreateSubKey(
                RunKeyPath,
                writable: true);
            runKey.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }

    public static void RefreshHostBackup(string host)
    {
        if (TryReadTaskXml(out string xml) && StartupPolicy.ReadTask(xml) is { Enabled: true })
            CreateOrUpdateTask(host);
    }

    private static void CreateOrUpdateTask(string? host = null)
    {
        string userSid = WindowsIdentity.GetCurrent().User?.Value
            ?? throw new IOException("无法取得当前 Windows 用户标识。");
        string executable = System.Security.SecurityElement.Escape(Application.ExecutablePath)
            ?? Application.ExecutablePath;
        string sid = System.Security.SecurityElement.Escape(userSid) ?? userSid;
        host ??= LocalState.TryLoadHost();
        string arguments = "--startup";
        if (!string.IsNullOrWhiteSpace(host))
            arguments += $" --host \"{host.Trim()}\"";
        string escapedArguments = System.Security.SecurityElement.Escape(arguments) ?? arguments;
        string xml = $"""
            <?xml version="1.0" encoding="UTF-16"?>
            <Task version="1.4" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
              <RegistrationInfo>
                <Description>Samsung Tizen 显示器亮度托盘程序</Description>
              </RegistrationInfo>
              <Triggers>
                <LogonTrigger>
                  <Enabled>true</Enabled>
                  <UserId>{sid}</UserId>
                  <Delay>PT10S</Delay>
                </LogonTrigger>
              </Triggers>
              <Principals>
                <Principal id="Author">
                  <UserId>{sid}</UserId>
                  <LogonType>InteractiveToken</LogonType>
                  <RunLevel>LeastPrivilege</RunLevel>
                </Principal>
              </Principals>
              <Settings>
                <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
                <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
                <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
                <AllowHardTerminate>true</AllowHardTerminate>
                <StartWhenAvailable>true</StartWhenAvailable>
                <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
                <IdleSettings>
                  <StopOnIdleEnd>false</StopOnIdleEnd>
                  <RestartOnIdle>false</RestartOnIdle>
                </IdleSettings>
                <AllowStartOnDemand>true</AllowStartOnDemand>
                <Enabled>true</Enabled>
                <Hidden>false</Hidden>
                <RunOnlyIfIdle>false</RunOnlyIfIdle>
                <WakeToRun>false</WakeToRun>
                <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
                <Priority>7</Priority>
                <RestartOnFailure>
                  <Interval>PT1M</Interval>
                  <Count>3</Count>
                </RestartOnFailure>
              </Settings>
              <Actions Context="Author">
                <Exec>
                  <Command>{executable}</Command>
                  <Arguments>{escapedArguments}</Arguments>
                </Exec>
              </Actions>
            </Task>
            """;

        WriteTask(xml);
    }

    private static void WriteTask(string xml)
    {
        string temporaryXml = Path.Combine(
            Path.GetTempPath(),
            $"SamsungTizenBrightness-{Guid.NewGuid():N}.xml");
        try
        {
            File.WriteAllText(temporaryXml, xml, Encoding.Unicode);
            ProcessResult result = RunSchtasks(["/Create", "/TN", TaskName, "/XML", temporaryXml, "/F"]);
            if (result.ExitCode != 0)
                throw new IOException(DescribeSchtasksFailure("创建登录计划任务", result));
        }
        finally
        {
            try { File.Delete(temporaryXml); } catch { }
        }
    }

    private static void DeleteTask()
    {
        if (!TryReadTaskXml(out _))
            return;

        ProcessResult result = RunSchtasks(["/Delete", "/TN", TaskName, "/F"]);
        if (result.ExitCode != 0)
            throw new IOException(DescribeSchtasksFailure("删除登录计划任务", result));
    }

    private static bool TryReadTaskXml(out string xml)
    {
        ProcessResult result = RunSchtasks(["/Query", "/TN", TaskName, "/XML"]);
        xml = result.StandardOutput;
        return result.ExitCode == 0 && !string.IsNullOrWhiteSpace(xml);
    }

    private static ProcessResult RunSchtasks(IEnumerable<string> arguments)
    {
        var start = new System.Diagnostics.ProcessStartInfo
        {
            FileName = Path.Combine(Environment.SystemDirectory, "schtasks.exe"),
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (string argument in arguments)
            start.ArgumentList.Add(argument);

        return BoundedProcess.RunAsync(start, TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
    }

    private static string DescribeSchtasksFailure(string action, ProcessResult result)
    {
        string detail = string.IsNullOrWhiteSpace(result.StandardError)
            ? result.StandardOutput.Trim()
            : result.StandardError.Trim();
        return string.IsNullOrWhiteSpace(detail)
            ? $"{action}失败（代码 {result.ExitCode}）。"
            : $"{action}失败：{detail}";
    }

}

internal sealed class TrayContext : ApplicationContext
{
    private readonly string _host;
    private readonly SamsungBrightnessSession _session;
    private readonly MonitorControlPopup _popup;
    private readonly NotifyIcon _trayIcon;
    private readonly ToolStripMenuItem _openItem;
    private readonly ToolStripMenuItem _connectionItem;
    private readonly ToolStripMenuItem _startWithWindowsItem;
    private readonly ToolStripMenuItem _reconnectItem;
    private readonly ToolStripMenuItem _pairItem;
    private readonly ToolStripMenuItem _languageItem;
    private readonly Bitmap _languageIcon;
    private readonly ToolStripMenuItem _exitItem;
    private readonly Icon _appIcon;
    private readonly Icon _offlineIcon;
    private readonly MonitorConnectivity _connectivity;
    private readonly HdmiMonitorPresence _hdmiPresence;
    private readonly EventWaitHandle _openSignal;
    private readonly System.Windows.Forms.Timer _openSignalTimer = new();
    private readonly System.Windows.Forms.Timer _trayRegistrationTimer = new();
    private bool _exiting;
    private bool _controlConnected;
    private bool _hdmiConnected;
    private bool _pairing;
    private volatile bool _suspended;

    public TrayContext(
        string host,
        bool openOnLaunch,
        bool startedWithWindows,
        EventWaitHandle openSignal)
    {
        _host = host;
        _openSignal = openSignal;
        _session = new SamsungBrightnessSession(host, LocalState.LoadBrightness());
        _connectivity = new MonitorConnectivity(_session, () => _hdmiConnected && !_suspended);
        _hdmiPresence = new HdmiMonitorPresence();
        _popup = new MonitorControlPopup(_session, _connectivity, host);
        _appIcon = (Icon)(Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application).Clone();
        _offlineIcon = IconVisuals.CreateGrayscale(_appIcon);

        var menu = new ContextMenuStrip();
        _openItem = new ToolStripMenuItem(L.T("OpenBrightness"), null, (_, _) => _popup.OpenNearCursor());
        _connectionItem = new ToolStripMenuItem(L.T("ConnectionSettings"), null, (_, _) => ShowConnectionSetup());
        _startWithWindowsItem = new ToolStripMenuItem(L.T("StartWithWindows"))
        {
            Checked = StartupRegistration.IsEnabled()
        };
        _startWithWindowsItem.Click += (_, _) => ToggleStartWithWindows();
        _reconnectItem = new ToolStripMenuItem(L.T("ReconnectDisplay"), null, async (_, _) => await ReconnectDisplayAsync());
        _pairItem = new ToolStripMenuItem(L.T("PairRemote"), null, async (_, _) => await PairRemoteAsync());
        _languageIcon = IconVisuals.CreateGlobe();
        _languageItem = new ToolStripMenuItem(L.T("Language"), _languageIcon);
        foreach (UiLanguage language in Enum.GetValues<UiLanguage>())
        {
            var languageOption = new ToolStripMenuItem(L.LanguageName(language))
            {
                Tag = language,
                Checked = language == L.Current
            };
            languageOption.Click += (_, _) => ChangeLanguage(language);
            _languageItem.DropDownItems.Add(languageOption);
        }
        _exitItem = new ToolStripMenuItem(L.T("Exit"), null, async (_, _) => await ExitAsync());
        menu.Items.Add(_openItem);
        menu.Items.Add(_connectionItem);
        menu.Items.Add(_startWithWindowsItem);
        menu.Items.Add(_reconnectItem);
        menu.Items.Add(_pairItem);
        menu.Items.Add(_languageItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_exitItem);

        _trayIcon = new NotifyIcon
        {
            Text = L.T("AppTitle"),
            Icon = _offlineIcon,
            ContextMenuStrip = menu,
            Visible = true
        };
        _trayIcon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
                _popup.OpenNearCursor();
        };

        // NotifyIcon is initially constructed before Application.Run enters
        // the WinForms message loop. Explorer occasionally drops that first
        // registration (notably just after an in-place update). Re-register it
        // once the UI loop is active so a healthy background process can never
        // remain running without a visible tray entry.
        _trayRegistrationTimer.Interval = 900;
        _trayRegistrationTimer.Tick += (_, _) =>
        {
            _trayRegistrationTimer.Stop();
            if (_exiting)
                return;
            _trayIcon.Visible = false;
            _trayIcon.Visible = true;
            AppDiagnostics.Log("tray icon registration refreshed after startup");
        };
        _trayRegistrationTimer.Start();

        if (!startedWithWindows)
        {
            _trayIcon.BalloonTipTitle = L.T("StartedTitle");
            _trayIcon.BalloonTipText = L.T("StartedBody");
            _trayIcon.ShowBalloonTip(3000);
        }
        _ = _popup.Handle;
        _openSignalTimer.Interval = 100;
        _openSignalTimer.Tick += (_, _) =>
        {
            if (_openSignal.WaitOne(0))
            {
                AppDiagnostics.Log("open signal received");
                _popup.OpenNearCursor();
            }
        };
        _openSignalTimer.Start();
        _connectivity.StatusChanged += TrayConnectivity_StatusChanged;
        _hdmiPresence.StatusChanged += HdmiPresence_StatusChanged;
        SystemEvents.PowerModeChanged += SystemEvents_PowerModeChanged;
        _connectivity.Start();
        _hdmiPresence.Start();
        if (openOnLaunch)
        {
            EventHandler? openWhenReady = null;
            openWhenReady = (_, _) =>
            {
                Application.Idle -= openWhenReady;
                _popup.OpenNearCursor();
            };
            Application.Idle += openWhenReady;
        }
    }

    private void TrayConnectivity_StatusChanged(bool connected)
    {
        _controlConnected = connected;
        UpdateTrayConnectionState();
    }

    private void HdmiPresence_StatusChanged(bool connected)
    {
        if (_popup.IsHandleCreated && _popup.InvokeRequired)
        {
            try { _popup.BeginInvoke(() => HdmiPresence_StatusChanged(connected)); }
            catch (InvalidOperationException) { }
            return;
        }
        _hdmiConnected = connected;
        _session.IsDisplayAttached = connected;
        UpdateTrayConnectionState();
        if (_exiting) return;
        if (connected && !_suspended)
            _ = _session.ProbeAsync();
        else
            _session.MarkUnavailable();
        // Cable events only update connectivity. Never change the display's power.
    }

    private void SystemEvents_PowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (_exiting) return;
        if (e.Mode == PowerModes.Suspend)
        {
            _suspended = true;
            _session.MarkUnavailable();
        }
        else if (e.Mode == PowerModes.Resume)
        {
            _suspended = false;
            if (_hdmiConnected)
                _ = _session.ProbeAsync(); // Read-only reconnect; no wake packet or power command.
        }
    }

    private void UpdateTrayConnectionState()
    {
        void Update()
        {
            if (_exiting)
                return;

            bool connected = _controlConnected && _hdmiConnected;
            _trayIcon.Icon = connected ? _appIcon : _offlineIcon;
            _trayIcon.Text = connected
                ? L.T("TrayConnected")
                : !_hdmiConnected
                    ? L.T("TrayHdmiDisconnected")
                    : L.T("TrayControlDisconnected");
        }

        if (_popup.IsHandleCreated && _popup.InvokeRequired)
        {
            try { _popup.BeginInvoke(Update); } catch (InvalidOperationException) { }
        }
        else
        {
            Update();
        }
    }

    private void ChangeLanguage(UiLanguage language)
    {
        if (language == L.Current)
            return;

        L.Set(language);
        _openItem.Text = L.T("OpenBrightness");
        _connectionItem.Text = L.T("ConnectionSettings");
        _startWithWindowsItem.Text = L.T("StartWithWindows");
        _reconnectItem.Text = L.T("ReconnectDisplay");
        _pairItem.Text = L.T("PairRemote");
        _languageItem.Text = L.T("Language");
        _exitItem.Text = L.T("Exit");
        foreach (ToolStripMenuItem item in _languageItem.DropDownItems.OfType<ToolStripMenuItem>())
            item.Checked = item.Tag is UiLanguage itemLanguage && itemLanguage == language;
        _popup.ApplyLanguage();
        UpdateTrayConnectionState();
    }

    private async Task ReconnectDisplayAsync()
    {
        try
        {
            await _session.ProbeAsync();
            if (!_session.IsAvailable) throw new IOException(L.T("DisplayUnavailable"));
            _popup.OpenNearCursor();
        }
        catch (Exception error)
        {
            MessageBox.Show(L.T("ReconnectFailed", error.Message), L.T("AppTitle"),
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async Task PairRemoteAsync()
    {
        if (_pairing || _exiting)
            return;

        _pairing = true;
        _trayIcon.BalloonTipTitle = L.T("PairingTitle");
        _trayIcon.BalloonTipText = L.T("PairingBody");
        _trayIcon.ShowBalloonTip(5000);
        try
        {
            await _session.PairRemoteAsync();
            _trayIcon.BalloonTipTitle = L.T("PairingComplete");
            _trayIcon.BalloonTipText = L.T("PairingCompleteBody");
            _trayIcon.ShowBalloonTip(4000);
            _popup.OpenNearCursor();
        }
        catch (Exception error)
        {
            MessageBox.Show(
                L.T("PairFailed", error.Message),
                L.T("AppTitle"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
        finally
        {
            _pairing = false;
        }
    }

    private void ShowConnectionSetup()
    {
        string? updatedHost = ConnectionSetupPrompt.Show(_host);
        _startWithWindowsItem.Checked = StartupRegistration.IsEnabled();
        if (updatedHost is null ||
            updatedHost.Equals(_host, StringComparison.OrdinalIgnoreCase))
            return;

        LocalState.SaveHost(updatedHost);
        StartupRegistration.RefreshHostBackup(updatedHost);
        MessageBox.Show(
            L.T("HostSavedRestart"),
            L.T("AppTitle"),
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void ToggleStartWithWindows()
    {
        bool enable = !_startWithWindowsItem.Checked;
        try
        {
            StartupRegistration.SetEnabled(enable);
            _startWithWindowsItem.Checked = enable;
        }
        catch (Exception error) when (error is UnauthorizedAccessException or IOException)
        {
            MessageBox.Show(
                L.T("StartupFailed", error.Message),
                L.T("AppTitle"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private async Task ExitAsync()
    {
        if (_exiting)
            return;

        _exiting = true;
        SystemEvents.PowerModeChanged -= SystemEvents_PowerModeChanged;
        _openSignalTimer.Stop();
        _connectivity.StatusChanged -= TrayConnectivity_StatusChanged;
        _hdmiPresence.StatusChanged -= HdmiPresence_StatusChanged;
        _hdmiPresence.Dispose();
        await _connectivity.DisposeAsync();
        await _popup.ShutdownAsync();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _languageIcon.Dispose();
        _offlineIcon.Dispose();
        _appIcon.Dispose();
        _trayRegistrationTimer.Dispose();
        _openSignalTimer.Dispose();
        _popup.Dispose();
        ExitThread();
    }
}

internal static class IconVisuals
{
    // Code-drawn globe: stays legible in the menu image column and does not
    // depend on emoji-font availability or the selected interface language.
    internal static Bitmap CreateGlobe()
    {
        var bitmap = new Bitmap(16, 16);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(SystemColors.MenuText, 1.2F);
        graphics.DrawEllipse(pen, 1.5F, 1.5F, 13F, 13F);
        graphics.DrawEllipse(pen, 4.5F, 1.5F, 7F, 13F);
        graphics.DrawLine(pen, 1.5F, 8F, 14.5F, 8F);
        graphics.DrawArc(pen, 1.5F, 4.3F, 13F, 3.7F, 0, 180);
        graphics.DrawArc(pen, 1.5F, 8F, 13F, 3.7F, 180, 180);
        return bitmap;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr iconHandle);

    public static Icon CreateGrayscale(Icon source)
    {
        using Bitmap bitmap = source.ToBitmap();
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                Color pixel = bitmap.GetPixel(x, y);
                int gray = (int)Math.Round(
                    pixel.R * 0.2126D +
                    pixel.G * 0.7152D +
                    pixel.B * 0.0722D);
                bitmap.SetPixel(x, y, Color.FromArgb(pixel.A, gray, gray, gray));
            }
        }

        IntPtr handle = bitmap.GetHicon();
        try
        {
            using Icon temporary = Icon.FromHandle(handle);
            return (Icon)temporary.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }
}

internal sealed record MonitorSettingCapability(
    string Key,
    string Kind,
    int? Minimum,
    int? Maximum,
    IReadOnlyList<string> Options,
    bool Writable,
    bool Experimental = false,
    bool RequiresConfirmation = false);

internal sealed class MonitorSettingsSnapshot
{
    public MonitorSettingsSnapshot(
        IReadOnlyDictionary<string, MonitorSettingCapability> capabilities,
        IReadOnlyDictionary<string, object?> values)
    {
        Capabilities = capabilities;
        Values = values;
    }

    public IReadOnlyDictionary<string, MonitorSettingCapability> Capabilities { get; }
    public IReadOnlyDictionary<string, object?> Values { get; }
}

internal sealed class SamsungBrightnessSession : IAsyncDisposable
{
    private readonly string _host;
    private readonly SamsungIpControlClient _ip;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly CancellationTokenSource _shutdown = new();
    private int _initialized;
    private int _available;
    private Exception? _lastFailure;
    private bool _disposed;

    public SamsungBrightnessSession(string host, int currentBrightness)
    {
        _host = host;
        _ip = new SamsungIpControlClient(host, LocalState.TryLoadIpControlAuthorization(host));
        CurrentBrightness = Math.Clamp(currentBrightness, 0, 50);
        AppDiagnostics.Log("control mode=IP Remote brightness only; automatic power disabled");
    }

    public int CurrentBrightness { get; private set; }
    public bool IsDisplayAttached { get; set; }
    public bool IsAvailable => Volatile.Read(ref _available) == 1;
    public Exception? LastFailure => Volatile.Read(ref _lastFailure);
    public bool IsOpen => Volatile.Read(ref _initialized) == 1 && IsAvailable;
    public event Action<string, string>? ProgressChanged;
    public event Action<bool>? AvailabilityChanged;

    public async Task ProbeAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed) return;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _shutdown.Token);
        await _gate.WaitAsync(linked.Token);
        try
        {
            await ReadBrightnessAsync(linked.Token);
        }
        catch (Exception error) when (error is not OperationCanceledException || !linked.IsCancellationRequested)
        {
            MarkUnavailable(error);
        }
        finally { _gate.Release(); }
    }

    public void MarkUnavailable(Exception? error = null)
    {
        Volatile.Write(ref _lastFailure, error);
        Volatile.Write(ref _initialized, 0);
        if (Interlocked.Exchange(ref _available, 0) == 1)
            AvailabilityChanged?.Invoke(false);
    }

    private void MarkAvailable()
    {
        Volatile.Write(ref _lastFailure, null);
        if (Interlocked.Exchange(ref _available, 1) == 0)
            AvailabilityChanged?.Invoke(true);
    }

    private async Task<int> ReadBrightnessAsync(CancellationToken cancellationToken)
    {
        int value = await _ip.GetBacklightAsync(cancellationToken);
        CurrentBrightness = value;
        LocalState.SaveBrightness(value);
        MarkAvailable();
        return value;
    }

    public async Task PairRemoteAsync()
    {
        await _gate.WaitAsync(_shutdown.Token);
        try
        {
            Report(L.T("PairingBody"), "IP_REMOTE — explicit pairing");
            IpControlAuthorization authorization = await _ip.PairAsync(_shutdown.Token);
            LocalState.SaveIpControlAuthorization(authorization);
            await ReadBrightnessAsync(_shutdown.Token);
            AppDiagnostics.Log("IP Remote paired; authorization encrypted and certificate pinned");
        }
        catch (Exception error) { MarkUnavailable(error); throw; }
        finally { _gate.Release(); }
    }

    public async Task OpenAsync()
    {
        await _gate.WaitAsync(_shutdown.Token);
        try
        {
            Report(L.T("ReadingBrightness"), "IP_REMOTE — backlightControl get");
            await ReadBrightnessAsync(_shutdown.Token);
            Volatile.Write(ref _initialized, 1);
        }
        catch (Exception error) { MarkUnavailable(error); throw; }
        finally { _gate.Release(); }
    }

    public async Task MoveToAsync(int target)
    {
        await _gate.WaitAsync(_shutdown.Token);
        try
        {
            if (!IsOpen) throw new InvalidOperationException(L.T("ControlDisconnected"));
            Report(L.T("Brightness"), $"IP_REMOTE — backlightControl {target}");
            CurrentBrightness = await _ip.SetBacklightAsync(Math.Clamp(target, 0, 50), _shutdown.Token);
            LocalState.SaveBrightness(CurrentBrightness);
            AppDiagnostics.Log($"IP Remote backlight set; value={CurrentBrightness}");
        }
        catch (Exception error) { MarkUnavailable(error); throw; }
        finally { _gate.Release(); }
    }

    public async Task<MonitorSettingsSnapshot> GetSettingsSnapshotAsync()
    {
        await OpenAsync();
        return new MonitorSettingsSnapshot(
            new Dictionary<string, MonitorSettingCapability>
            {
                ["backlight"] = new("backlight", "range", 0, 50, Array.Empty<string>(), true)
            },
            new Dictionary<string, object?> { ["backlight"] = CurrentBrightness });
    }

    public async Task<object?> SetMonitorSettingAsync(string key, object value, bool confirmed = false)
    {
        if (!key.Equals("backlight", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException("Only backlight is supported.");
        await MoveToAsync(Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture));
        return CurrentBrightness;
    }

    public Task ResetMinimumAsync() => MoveToAsync(0);
    public Task ResetMaximumAsync() => MoveToAsync(50);
    public Task CloseAsync() => Task.CompletedTask;
    public Task AbortAsync()
    {
        Volatile.Write(ref _initialized, 0);
        return Task.CompletedTask;
    }

    private void Report(string status, string command) => ProgressChanged?.Invoke(status, command);

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        _shutdown.Cancel();
        await _gate.WaitAsync();
        try { _ip.Dispose(); }
        finally { _gate.Release(); }
        _shutdown.Dispose();
    }
}

internal sealed class MonitorConnectivity : IAsyncDisposable
{
    private readonly SamsungBrightnessSession _session;
    private readonly Func<bool> _shouldProbe;
    private readonly CancellationTokenSource _shutdown = new();
    private Task? _loop;
    private bool? _lastResult;

    public MonitorConnectivity(SamsungBrightnessSession session, Func<bool> shouldProbe)
    {
        _session = session;
        _shouldProbe = shouldProbe;
        _session.AvailabilityChanged += Session_AvailabilityChanged;
    }

    public event Action<bool>? StatusChanged;
    public void Start()
    {
        PublishIfChanged(_session.IsAvailable);
        _loop = PollAsync();
    }

    private async Task PollAsync()
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
            while (await timer.WaitForNextTickAsync(_shutdown.Token))
            {
                if (_shouldProbe())
                    await _session.ProbeAsync(_shutdown.Token);
                else
                    _session.MarkUnavailable();
            }
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested) { }
        catch (Exception error)
        {
            AppDiagnostics.Log($"IP Remote polling failed; {error.GetType().Name}: {error.Message}");
        }
    }

    private void Session_AvailabilityChanged(bool connected) => PublishIfChanged(connected);
    private void PublishIfChanged(bool connected)
    {
        if (_lastResult == connected) return;
        _lastResult = connected;
        StatusChanged?.Invoke(connected);
    }

    public async ValueTask DisposeAsync()
    {
        _session.AvailabilityChanged -= Session_AvailabilityChanged;
        _shutdown.Cancel();
        if (_loop is not null) await _loop;
        _shutdown.Dispose();
    }
}

internal sealed class HdmiMonitorPresence : IDisposable
{
    private System.Threading.Timer? _timer;
    private int _checking;
    private bool? _lastResult;

    public event Action<bool>? StatusChanged;

    public void Start()
    {
        _timer ??= new System.Threading.Timer(
            _ => CheckNow(),
            null,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(2));
    }

    private void CheckNow()
    {
        if (Interlocked.Exchange(ref _checking, 1) != 0)
            return;

        try
        {
            bool connected = IsSamsungMonitorActive();
            if (_lastResult == connected)
                return;
            _lastResult = connected;
            AppDiagnostics.Log($"display cable presence changed; connected={connected}; automatic power disabled");
            StatusChanged?.Invoke(connected);
        }
        catch (ManagementException)
        {
            // Keep the previous state if Windows monitor instrumentation is busy.
        }
        catch (COMException)
        {
            // A display topology change can briefly invalidate the WMI query.
        }
        finally
        {
            Volatile.Write(ref _checking, 0);
        }
    }

    private static bool IsSamsungMonitorActive()
    {
        using var searcher = new ManagementObjectSearcher(
            @"root\wmi",
            "SELECT Active, ManufacturerName FROM WmiMonitorID WHERE Active = TRUE");
        using ManagementObjectCollection monitors = searcher.Get();
        foreach (ManagementObject monitor in monitors)
        {
            using (monitor)
            {
                string manufacturer = DecodeWmiText(monitor["ManufacturerName"] as Array);
                if (manufacturer.Equals("SAM", StringComparison.OrdinalIgnoreCase) ||
                    manufacturer.Contains("SAMSUNG", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        return false;
    }

    private static string DecodeWmiText(Array? values)
    {
        if (values is null)
            return string.Empty;

        var text = new StringBuilder(values.Length);
        foreach (object? value in values)
        {
            char character = (char)Convert.ToUInt16(value);
            if (character == '\0')
                break;
            text.Append(character);
        }
        return text.ToString();
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _timer = null;
    }
}

internal static class LocalState
{
    private static readonly string DirectoryPath = UserDataDirectory.Resolve();
    private static readonly string IpControlPath = Path.Combine(DirectoryPath, "ipcontrol.json");
    private static readonly byte[] IpControlEntropy = Encoding.UTF8.GetBytes("Samsung-IP-Control-v1");
    private static readonly string BrightnessPath = Path.Combine(DirectoryPath, "brightness.txt");
    private static readonly string HostPath = Path.Combine(DirectoryPath, "host.txt");
    private static readonly string LanguagePath = Path.Combine(DirectoryPath, "language.txt");

    public static string? TryLoadLanguage()
    {
        try
        {
            return File.Exists(LanguagePath) ? File.ReadAllText(LanguagePath).Trim() : null;
        }
        catch
        {
            return null;
        }
    }

    public static void SaveLanguage(string language)
    {
        Directory.CreateDirectory(DirectoryPath);
        File.WriteAllText(LanguagePath, language);
    }

    public static string? TryLoadHost()
    {
        if (!File.Exists(HostPath))
        {
            AppDiagnostics.Log($"saved display IP not found; path={HostPath}");
            return null;
        }
        string host = File.ReadAllText(HostPath).Trim();
        AppDiagnostics.Log($"saved display IP loaded; path={HostPath}; present={host.Length > 0}");
        return host.Length > 0 ? host : null;
    }

    public static void SaveHost(string host)
    {
        Directory.CreateDirectory(DirectoryPath);
        File.WriteAllText(HostPath, host.Trim());
    }

    public static IpControlAuthorization? TryLoadIpControlAuthorization(string host)
    {
        try
        {
            if (!File.Exists(IpControlPath)) return null;
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(IpControlPath));
            JsonElement root = document.RootElement;
            if (!string.Equals(root.GetProperty("Host").GetString(), host, StringComparison.OrdinalIgnoreCase))
                return null;
            byte[] encrypted = Convert.FromBase64String(root.GetProperty("ProtectedToken").GetString()!);
            string token = Encoding.UTF8.GetString(ProtectedData.Unprotect(
                encrypted, IpControlEntropy, DataProtectionScope.CurrentUser));
            string pin = root.GetProperty("CertificateSha256").GetString() ?? string.Empty;
            return string.IsNullOrWhiteSpace(token) || pin.Length != 64
                ? null : new IpControlAuthorization(host, token, pin);
        }
        catch { return null; }
    }

    public static void SaveIpControlAuthorization(IpControlAuthorization authorization)
    {
        Directory.CreateDirectory(DirectoryPath);
        byte[] encrypted = ProtectedData.Protect(Encoding.UTF8.GetBytes(authorization.Token),
            IpControlEntropy, DataProtectionScope.CurrentUser);
        string payload = JsonSerializer.Serialize(new
        {
            authorization.Host,
            ProtectedToken = Convert.ToBase64String(encrypted),
            authorization.CertificateSha256
        });
        string temporary = IpControlPath + ".tmp";
        File.WriteAllText(temporary, payload);
        File.Move(temporary, IpControlPath, overwrite: true);
    }

    public static int LoadBrightness()
    {
        if (File.Exists(BrightnessPath) &&
            int.TryParse(File.ReadAllText(BrightnessPath), out int value) &&
            value is >= 0 and <= 50)
            return value;

        // The last verified brightness before this version was built.
        return 15;
    }

    public static void SaveBrightness(int value)
    {
        Directory.CreateDirectory(DirectoryPath);
        File.WriteAllText(BrightnessPath, Math.Clamp(value, 0, 50).ToString());
    }
}

internal static class UserDataDirectory
{
    public static string Resolve()
    {
        string localAppData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);
        string normalPath = Path.Combine(localAppData, "M70BBrightness");

        try
        {
            string? sid = System.Security.Principal.WindowsIdentity.GetCurrent().User?.Value;
            if (sid is not null)
            {
                using RegistryKey? profileKey = Registry.LocalMachine.OpenSubKey(
                    $@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList\{sid}");
                string? profile = profileKey?.GetValue("ProfileImagePath") as string;
                if (!string.IsNullOrWhiteSpace(profile))
                {
                    string profilePath = Path.Combine(
                        Environment.ExpandEnvironmentVariables(profile),
                        "AppData", "Local", "M70BBrightness");
                    // Task Scheduler can occasionally start before the user
                    // shell has populated the standard folder lookup. Reuse
                    // the current account's existing settings in that case.
                    if (File.Exists(Path.Combine(profilePath, "host.txt")) ||
                        string.IsNullOrWhiteSpace(localAppData))
                        return profilePath;
                }
            }
        }
        catch
        {
            // The normal Windows known-folder path still works for fresh installs.
        }

        return normalPath;
    }
}

internal static class AppDiagnostics
{
    private static readonly object Gate = new();
    private static readonly string DirectoryPath = UserDataDirectory.Resolve();
    private static readonly string LogPath = Path.Combine(DirectoryPath, "diagnostic.log");

    public static void Log(string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(DirectoryPath);
                if (File.Exists(LogPath) && new FileInfo(LogPath).Length > 64 * 1024)
                    File.WriteAllText(LogPath, "diagnostic log rotated" + Environment.NewLine);
                File.AppendAllText(
                    LogPath,
                    $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // Diagnostics must never interfere with tray control.
        }
    }
}
