// SPDX-FileCopyrightText: 2026 dttutty
// SPDX-License-Identifier: GPL-3.0-only

using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace SamsungTizenBrightness;

/// <summary>
/// Windows 11 style brightness flyout. The IP Remote connection stays
/// alive when the flyout is hidden; opening the flyout only refreshes the
/// settings snapshot.
/// </summary>
internal sealed class MonitorControlPopup : Form
{
    private const int FlyoutWidth = 408;
    private const int FlyoutHeight = 150;
    private const string BrightnessSettingKey = "backlight";

    private readonly SamsungBrightnessSession _session;
    private readonly MonitorConnectivity _connectivity;
    private readonly string _host;
    private readonly Label _title = new();
    private readonly Label _subtitle = new();
    private readonly Button _themeButton = new();
    private readonly ToolTip _themeToolTip = new();
    private readonly FlowLayoutPanel _settingsPanel = new();
    private readonly Panel _overlay = new();
    private readonly Label _overlayTitle = new();
    private readonly Label _overlayCommand = new();
    private readonly ProgressBar _overlayProgress = new();
    private readonly System.Windows.Forms.Timer _closeTimer = new();
    private readonly System.Windows.Forms.Timer _outsideClickTimer = new();
    private readonly Dictionary<string, SettingCommandState> _commands =
        new(StringComparer.OrdinalIgnoreCase);

    private bool? _connected;
    private bool _opening;
    private bool _waking;
    private bool _closing;
    private bool _snapshotLoaded;
    private bool _mouseButtonsWereDown;
    private DateTime _outsideClickArmedAt;

    public MonitorControlPopup(
        SamsungBrightnessSession session,
        MonitorConnectivity connectivity,
        string host)
    {
        _session = session;
        _connectivity = connectivity;
        _host = host;

        Text = L.T("AppTitle");
        ClientSize = new Size(FlyoutWidth, FlyoutHeight);
        FormBorderStyle = FormBorderStyle.None;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = FlyoutColors.Surface;
        ForeColor = Color.White;
        Font = new Font("Microsoft YaHei UI", 9F);
        DoubleBuffered = true;

        _title.Text = L.T("PopupTitle");
        _title.AutoSize = true;
        _title.BackColor = Color.Transparent;
        _title.ForeColor = Color.White;
        _title.Font = new Font(Font.FontFamily, 12.5F, FontStyle.Bold);
        _title.Location = new Point(20, 15);

        _subtitle.Text = L.T("DetectingConnection");
        _subtitle.AutoSize = false;
        _subtitle.AutoEllipsis = true;
        _subtitle.Size = new Size(FlyoutWidth - 42, 22);
        _subtitle.BackColor = Color.Transparent;
        _subtitle.ForeColor = FlyoutColors.SecondaryText;
        _subtitle.Font = new Font(Font.FontFamily, 8.5F);
        _subtitle.Location = new Point(21, 43);

        _themeButton.Location = new Point(FlyoutWidth - 112, 17);
        _themeButton.Size = new Size(92, 30);
        _themeButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _themeButton.FlatStyle = FlatStyle.Flat;
        _themeButton.FlatAppearance.BorderSize = 0;
        _themeButton.FlatAppearance.MouseOverBackColor = FlyoutColors.Hover;
        _themeButton.FlatAppearance.MouseDownBackColor = FlyoutColors.Pressed;
        _themeButton.BackColor = FlyoutColors.Surface;
        _themeButton.ForeColor = FlyoutColors.PrimaryText;
        _themeButton.Font = new Font(Font.FontFamily, 8.5F);
        _themeButton.Cursor = Cursors.Hand;
        _themeButton.Click += ThemeButton_Click;
        RefreshThemeButton();

        var separator = new Panel
        {
            BackColor = FlyoutColors.Border,
            Location = new Point(16, 67),
            Size = new Size(FlyoutWidth - 32, 1),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        _settingsPanel.Location = new Point(16, 74);
        _settingsPanel.Size = new Size(FlyoutWidth - 25, FlyoutHeight - 86);
        _settingsPanel.Anchor = AnchorStyles.Top | AnchorStyles.Bottom |
                                AnchorStyles.Left | AnchorStyles.Right;
        _settingsPanel.AutoScroll = true;
        _settingsPanel.WrapContents = false;
        _settingsPanel.FlowDirection = FlowDirection.TopDown;
        _settingsPanel.BackColor = Color.Transparent;
        _settingsPanel.Padding = new Padding(0, 0, 5, 8);

        ConfigureOverlay();

        Controls.AddRange([_title, _subtitle, _themeButton, separator, _settingsPanel, _overlay]);
        _overlay.BringToFront();
        BuildCachedBrightnessShell();
        _overlay.Visible = false;

        _closeTimer.Interval = 320;
        _closeTimer.Tick += (_, _) => FinishClose();

        _outsideClickTimer.Interval = 30;
        _outsideClickTimer.Tick += (_, _) =>
        {
            bool mouseDown = Control.MouseButtons != MouseButtons.None;
            bool newClick = mouseDown && !_mouseButtonsWereDown;
            _mouseButtonsWereDown = mouseDown;
            if (newClick &&
                DateTime.UtcNow >= _outsideClickArmedAt &&
                !Bounds.Contains(Cursor.Position))
            {
                BeginClose();
            }
        };

        _connectivity.StatusChanged += Connectivity_StatusChanged;
        _session.ProgressChanged += Session_ProgressChanged;
    }

    public async void OpenNearCursor()
    {
        AppDiagnostics.Log($"open requested; visible={Visible}; connected={_connected}; ipRemote={_session.IsAvailable}");
        if (_closing)
            CancelClose();

        RefreshThemeButton();
        if (!_snapshotLoaded)
            ShowLoadingShell(L.T("DetectingDisplay"));
        PositionNearTray();
        if (!Visible)
            Show();
        AppDiagnostics.Log($"show completed; visible={Visible}");
        Activate();
        Update();
        _mouseButtonsWereDown = Control.MouseButtons != MouseButtons.None;
        _outsideClickArmedAt = DateTime.UtcNow.AddMilliseconds(250);
        _outsideClickTimer.Start();

        // Paint the complete cached flyout before network connection starts. Those operations can spend seconds timing out when
        // the PC is connected to a network without the display.
        await Task.Yield();
        if (!Visible || _closing)
            return;
        if (!_session.IsDisplayAttached)
        {
            ShowOffline();
            return;
        }

        if (_connected is true || _session.IsAvailable)
        {
            _connected = true;
            await EnsureSessionAndSnapshotAsync();
        }
        else
        {
            await ConnectDisplayAsync();
        }
    }

    public async Task ShutdownAsync()
    {
        _closeTimer.Stop();
        _outsideClickTimer.Stop();
        Hide();
        _connectivity.StatusChanged -= Connectivity_StatusChanged;
        _session.ProgressChanged -= Session_ProgressChanged;
        await _session.CloseAsync();
        await _session.DisposeAsync();
    }

    public void ApplyLanguage()
    {
        Text = L.T("AppTitle");
        _title.Text = L.T("PopupTitle");
        RefreshThemeButton();
        _snapshotLoaded = false;
        if (_connected is true)
        {
            _subtitle.Text = L.T("HdmiConnected");
            if (Visible)
                _ = EnsureSessionAndSnapshotAsync();
        }
        else
        {
            ShowOffline(_session.LastFailure);
        }
    }

    private void RefreshThemeButton()
    {
        bool light = WindowsColorMode.IsLight;
        _themeButton.Text = light ? L.T("ThemeLight") : L.T("ThemeDark");
        _themeButton.AccessibleName = light
            ? L.T("ThemeLightAccessible")
            : L.T("ThemeDarkAccessible");
        _themeToolTip.SetToolTip(_themeButton, light
            ? L.T("SwitchToDark")
            : L.T("SwitchToLight"));
    }

    private void ThemeButton_Click(object? sender, EventArgs e)
    {
        try
        {
            WindowsColorMode.SetLight(!WindowsColorMode.IsLight);
            RefreshThemeButton();
        }
        catch (Exception error)
        {
            AppDiagnostics.Log($"Windows color mode switch failed; {error.GetType().Name}: {error.Message}");
            MessageBox.Show(
                L.T("ThemeSwitchFailed", error.Message),
                Text,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private void ConfigureOverlay()
    {
        _overlay.Location = new Point(1, 69);
        _overlay.Size = new Size(FlyoutWidth - 2, FlyoutHeight - 70);
        _overlay.Anchor = AnchorStyles.Top | AnchorStyles.Bottom |
                          AnchorStyles.Left | AnchorStyles.Right;
        _overlay.BackColor = FlyoutColors.Surface;
        _overlay.Visible = true;

        _overlayTitle.Location = new Point(20, 5);
        _overlayTitle.Size = new Size(FlyoutWidth - 42, 27);
        _overlayTitle.ForeColor = Color.White;
        _overlayTitle.BackColor = Color.Transparent;
        _overlayTitle.Font = new Font(Font.FontFamily, 10.5F, FontStyle.Bold);
        _overlayTitle.TextAlign = ContentAlignment.MiddleLeft;

        _overlayCommand.Location = new Point(20, 31);
        _overlayCommand.Size = new Size(FlyoutWidth - 42, 35);
        _overlayCommand.ForeColor = FlyoutColors.SecondaryText;
        _overlayCommand.BackColor = Color.Transparent;
        _overlayCommand.Font = new Font("Cascadia Mono", 8.25F);

        _overlayProgress.Location = new Point(20, 73);
        _overlayProgress.Size = new Size(FlyoutWidth - 42, 4);
        _overlayProgress.Style = ProgressBarStyle.Marquee;
        _overlayProgress.MarqueeAnimationSpeed = 26;

        _overlay.Controls.AddRange([_overlayTitle, _overlayCommand, _overlayProgress]);
    }

    private async Task ConnectDisplayAsync()
    {
        if (_waking || _closing || !Visible)
            return;

        _waking = true;
        ShowLoadingShell(L.T("ConnectingDisplay"));
        try
        {
            await _session.OpenAsync();
            if (!_session.IsAvailable)
                throw new TimeoutException("无法建立显示器控制连接。");

            _connected = true;
            await EnsureSessionAndSnapshotAsync();
        }
        catch (Exception ex)
        {
            if (Visible && !_closing)
                ShowOffline(ex);
        }
        finally
        {
            _waking = false;
        }
    }

    private async Task EnsureSessionAndSnapshotAsync()
    {
        if (_opening || _closing || !Visible || _connected is not true)
        {
            AppDiagnostics.Log($"snapshot skipped; opening={_opening}; closing={_closing}; visible={Visible}; connected={_connected}");
            return;
        }

        _opening = true;
        AppDiagnostics.Log("snapshot started");
        ShowLoadingShell(L.T("ReadingBrightness"));
        try
        {
            if (!_session.IsOpen)
                await _session.OpenAsync();

            MonitorSettingsSnapshot snapshot = await _session.GetSettingsSnapshotAsync();
            if (!Visible || _closing || _connected is not true)
                return;

            BuildSettings(snapshot);
            AppDiagnostics.Log($"snapshot completed; capabilities={snapshot.Capabilities.Count}; values={snapshot.Values.Count}");
            _snapshotLoaded = true;
            _subtitle.Text = L.T("HdmiConnected");
            _themeToolTip.SetToolTip(_subtitle, null);
            _subtitle.AccessibleDescription = null;
            _subtitle.ForeColor = FlyoutColors.Connected;
            HideOverlay();
        }
        catch (Exception ex)
        {
            AppDiagnostics.Log($"snapshot failed; {ex.GetType().Name}: {ex.Message}");
            _snapshotLoaded = false;
            if (Visible && !_closing)
                ShowOffline(ex);
        }
        finally
        {
            _opening = false;
        }
    }

    private void BuildSettings(MonitorSettingsSnapshot snapshot)
    {
        _settingsPanel.SuspendLayout();
        try
        {
            Control[] previousControls = _settingsPanel.Controls.Cast<Control>().ToArray();
            _settingsPanel.Controls.Clear();
            foreach (Control control in previousControls)
                control.Dispose();
            _commands.Clear();

            var grouped = snapshot.Capabilities.Values
                .Where(capability => capability.Key.Equals(
                    BrightnessSettingKey,
                    StringComparison.OrdinalIgnoreCase))
                .Where(capability => snapshot.Values.ContainsKey(capability.Key))
                .OrderBy(capability => SettingPresentation.SortOrder(capability.Key))
                .ThenBy(capability => capability.Key, StringComparer.OrdinalIgnoreCase)
                .GroupBy(capability => SettingPresentation.Section(
                    capability.Key,
                    capability.Experimental))
                .OrderBy(group => SettingPresentation.SectionOrder(group.Key));

            foreach (IGrouping<string, MonitorSettingCapability> group in grouped)
            {
                foreach (MonitorSettingCapability capability in group)
                {
                    object? value = snapshot.Values[capability.Key];
                    MonitorSettingRow? row = CreateSettingRow(capability, value, snapshot);
                    if (row is null)
                        continue;

                    row.Width = _settingsPanel.ClientSize.Width - 13;
                    row.Margin = new Padding(0, 0, 0, 2);
                    row.ValueRequested += SettingRow_ValueRequested;
                    if (row is MonitorSliderSettingRow sliderRow)
                        sliderRow.MuteRequested += SliderRow_MuteRequested;
                    _settingsPanel.Controls.Add(row);
                    _commands[capability.Key] = new SettingCommandState(row);
                }
            }

            if (_commands.Count == 0)
            {
                _settingsPanel.Controls.Add(new Label
                {
                    Text = L.T("BrightnessUnavailable"),
                    ForeColor = FlyoutColors.SecondaryText,
                    BackColor = Color.Transparent,
                    AutoSize = false,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Size = new Size(_settingsPanel.ClientSize.Width - 13, 90),
                    Margin = new Padding(0, 24, 0, 0)
                });
            }
            AppDiagnostics.Log($"settings UI built; rows={_commands.Count}");
        }
        finally
        {
            _settingsPanel.ResumeLayout(true);
        }
    }

    private void BuildCachedBrightnessShell()
    {
        if (_settingsPanel.Controls.Count > 0)
            return;

        var row = new MonitorSliderSettingRow(
            BrightnessSettingKey,
            L.T("Brightness"),
            minimum: 0,
            maximum: 50,
            value: _session.CurrentBrightness,
            writable: false,
            requiresConfirmation: false,
            showSunEndpoints: true,
            showMuteButton: false,
            muted: false,
            muteWritable: false)
        {
            Width = _settingsPanel.ClientSize.Width - 13,
            Margin = new Padding(0, 0, 0, 2)
        };
        _settingsPanel.Controls.Add(row);
    }

    private void ShowLoadingShell(string status)
    {
        BuildCachedBrightnessShell();
        foreach (Control control in _settingsPanel.Controls)
            control.Enabled = false;
        _subtitle.Text = status;
        _themeToolTip.SetToolTip(_subtitle, null);
        _subtitle.AccessibleDescription = null;
        _subtitle.ForeColor = FlyoutColors.SecondaryText;
        _overlay.Visible = false;
    }

    private void AddSectionHeader(string section)
    {
        _settingsPanel.Controls.Add(new Label
        {
            Text = section,
            ForeColor = FlyoutColors.SecondaryText,
            BackColor = Color.Transparent,
            Font = new Font(Font.FontFamily, 8.5F, FontStyle.Bold),
            AutoSize = false,
            TextAlign = ContentAlignment.BottomLeft,
            Size = new Size(_settingsPanel.ClientSize.Width - 13, 29),
            Margin = new Padding(4, 5, 0, 2)
        });
    }

    private MonitorSettingRow? CreateSettingRow(
        MonitorSettingCapability capability,
        object? value,
        MonitorSettingsSnapshot snapshot)
    {
        string displayName = SettingPresentation.DisplayName(capability.Key);
        string kind = capability.Kind.ToLowerInvariant();

        if ((kind is "range" or "number" ||
             capability.Minimum.HasValue || capability.Maximum.HasValue) &&
            TryConvertInt(value, out int numericValue))
        {
            int minimum = capability.Minimum ?? 0;
            int maximum = capability.Maximum ?? Math.Max(100, numericValue);
            if (maximum <= minimum)
                maximum = minimum + 1;
            bool isVolume = capability.Key.Equals("volume", StringComparison.OrdinalIgnoreCase);
            bool muted = isVolume &&
                snapshot.Values.TryGetValue("mute", out object? muteValue) &&
                TryConvertBool(muteValue, out bool currentMute) && currentMute;
            bool muteWritable = isVolume &&
                snapshot.Capabilities.TryGetValue("mute", out MonitorSettingCapability? muteCapability) &&
                muteCapability.Writable;
            return new MonitorSliderSettingRow(
                capability.Key,
                displayName,
                minimum,
                maximum,
                numericValue,
                capability.Writable,
                capability.RequiresConfirmation ||
                    SettingPresentation.RequiresConfirmation(capability.Key),
                showSunEndpoints: capability.Key.Equals("backlight", StringComparison.OrdinalIgnoreCase),
                showMuteButton: isVolume,
                muted,
                muteWritable);
        }

        if (kind is "toggle" or "bool" or "boolean" || value is bool)
        {
            return new MonitorToggleSettingRow(
                capability.Key,
                displayName,
                TryConvertBool(value, out bool enabled) && enabled,
                capability.Writable,
                capability.RequiresConfirmation ||
                    SettingPresentation.RequiresConfirmation(capability.Key));
        }

        if (kind is "choice" or "enum" || capability.Options.Count > 0)
        {
            string current = Convert.ToString(value) ?? string.Empty;
            IReadOnlyList<string> options = capability.Options.Count > 0
                ? capability.Options
                : new[] { current };
            return new MonitorChoiceSettingRow(
                capability.Key,
                displayName,
                options,
                current,
                capability.Writable,
                capability.RequiresConfirmation ||
                    SettingPresentation.RequiresConfirmation(capability.Key));
        }

        return null;
    }

    private async void SliderRow_MuteRequested(MonitorSliderSettingRow row, bool muted)
    {
        if (_connected is not true || !_session.IsOpen)
            return;

        row.SetPending(true);
        row.SetError(null);
        try
        {
            object? actual = await _session.SetMonitorSettingAsync("mute", muted);
            row.ApplyMuteActual(actual);
        }
        catch (Exception ex)
        {
            row.ApplyMuteActual(!muted);
            row.SetError(ex.Message);
        }
        finally
        {
            row.SetPending(false);
        }
    }

    private void SettingRow_ValueRequested(MonitorSettingRow row, object value)
    {
        if (_connected is not true || !_session.IsOpen || !row.Writable)
            return;
        if (!_commands.TryGetValue(row.SettingKey, out SettingCommandState? state))
            return;

        state.DesiredValue = value;
        state.Revision++;
        if (!state.Running)
            _ = RunSettingCommandsAsync(state);
    }

    private async Task RunSettingCommandsAsync(SettingCommandState state)
    {
        state.Running = true;
        state.Row.SetPending(true);
        try
        {
            while (_connected is true && _session.IsOpen)
            {
                int revision = state.Revision;
                object desired = state.DesiredValue ?? throw new InvalidOperationException();
                object? actual;
                try
                {
                    actual = await _session.SetMonitorSettingAsync(
                        state.Row.SettingKey,
                        desired,
                        state.Row.RequiresConfirmation);
                }
                catch (Exception ex)
                {
                    if (revision == state.Revision)
                        state.Row.SetError(ex.Message);
                    return;
                }

                if (revision == state.Revision)
                {
                    state.Row.ApplyActualValue(actual);
                    state.Row.SetError(null);
                    return;
                }
                // A newer target arrived while this write was in flight.  Send
                // only the newest value on the next pass.
            }
        }
        finally
        {
            state.Running = false;
            state.Row.SetPending(false);
        }
    }

    private void Connectivity_StatusChanged(bool connected)
    {
        void Update()
        {
            if (IsDisposed)
                return;

            _connected = connected;
            _subtitle.Text = connected ? L.T("HdmiConnected") : L.T("ControlDisconnected");
            _subtitle.ForeColor = connected ? FlyoutColors.Connected : FlyoutColors.Error;

            if (_closing)
                return;
            if (!connected)
            {
                _snapshotLoaded = false;
                foreach (SettingCommandState state in _commands.Values)
                    state.Row.Enabled = false;
                if (Visible)
                    ShowOffline(_session.LastFailure);
                _ = _session.AbortAsync();
            }
            else if (Visible)
            {
                _ = EnsureSessionAndSnapshotAsync();
            }
        }

        if (IsHandleCreated && InvokeRequired)
        {
            try { BeginInvoke(Update); } catch (InvalidOperationException) { }
        }
        else
        {
            Update();
        }
    }

    private void Session_ProgressChanged(string status, string command)
    {
        void Update()
        {
            if ((_opening || _waking) && !_closing)
            {
                _subtitle.Text = _opening
                    ? L.T("ReadingBrightness")
                    : L.T("ConnectingDisplay");
                _subtitle.ForeColor = FlyoutColors.SecondaryText;
            }
        }

        if (IsHandleCreated && InvokeRequired)
        {
            try { BeginInvoke(Update); } catch (InvalidOperationException) { }
        }
        else
        {
            Update();
        }
    }

    private void ShowOverlay(string title, string command, bool busy)
    {
        _overlayTitle.Text = title;
        _overlayCommand.Text = command;
        _overlayProgress.Visible = busy;
        _overlay.Visible = true;
        _overlay.BringToFront();
    }

    private void ShowOffline(Exception? error = null)
    {
        AppDiagnostics.Log($"connection unavailable; kind={ConnectionFailure.Classify(error)}; {error?.Message}");
        BuildCachedBrightnessShell();
        foreach (Control control in _settingsPanel.Controls)
            control.Enabled = false;
        _subtitle.Text = ConnectionFailure.Summary(error);
        _themeToolTip.SetToolTip(_subtitle, ConnectionFailure.Guidance(error));
        _subtitle.AccessibleDescription = ConnectionFailure.Guidance(error);
        _subtitle.ForeColor = FlyoutColors.Error;
        _overlay.Visible = false;
    }

    private void HideOverlay()
    {
        _overlay.Visible = false;
        foreach (SettingCommandState state in _commands.Values)
            state.Row.Enabled = state.Row.Writable;
    }

    private void BeginClose()
    {
        if (_closing || !Visible)
            return;

        AppDiagnostics.Log("close started");
        _closing = true;
        _outsideClickTimer.Stop();
        ShowOverlay(
            L.T("ClosingControl"),
            "HIDE — 保持显示器控制通道在线",
            busy: true);
        _closeTimer.Start();
    }

    private void CancelClose()
    {
        _closeTimer.Stop();
        _closing = false;
        if (_snapshotLoaded && _connected is true && _session.IsOpen)
            HideOverlay();
    }

    private void FinishClose()
    {
        _closeTimer.Stop();
        _outsideClickTimer.Stop();
        Hide();
        _closing = false;
    }

    private void PositionNearTray()
    {
        Screen screen = Screen.FromPoint(Cursor.Position);
        Rectangle area = screen.WorkingArea;
        Location = new Point(area.Right - Width - 4, area.Bottom - Height - 4);
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        if (Visible && !_closing && DateTime.UtcNow >= _outsideClickArmedAt)
            BeginClose();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        try
        {
            int enabled = 1;
            int rounded = 2;
            int transientBackdrop = 3;
            DwmSetWindowAttribute(Handle, 20, ref enabled, sizeof(int));
            DwmSetWindowAttribute(Handle, 33, ref rounded, sizeof(int));
            DwmSetWindowAttribute(Handle, 38, ref transientBackdrop, sizeof(int));
        }
        catch
        {
            // The custom dark surface is the fallback on older Windows builds.
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var border = new Pen(FlyoutColors.Border);
        using GraphicsPath path = MonitorFlyoutDrawing.RoundedRectangle(
            new Rectangle(0, 0, ClientSize.Width - 1, ClientSize.Height - 1), 11);
        e.Graphics.DrawPath(border, path);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _closeTimer.Dispose();
            _outsideClickTimer.Dispose();
            _themeToolTip.Dispose();
        }
        base.Dispose(disposing);
    }

    private static bool TryConvertInt(object? value, out int result)
    {
        try
        {
            if (value is not null)
            {
                result = Convert.ToInt32(value);
                return true;
            }
        }
        catch (Exception) when (value is string or double or float or long)
        {
        }
        result = 0;
        return false;
    }

    private static bool TryConvertBool(object? value, out bool result)
    {
        if (value is bool boolean)
        {
            result = boolean;
            return true;
        }
        string text = Convert.ToString(value) ?? string.Empty;
        if (text.Equals("ON", StringComparison.OrdinalIgnoreCase) || text == "1")
        {
            result = true;
            return true;
        }
        if (text.Equals("OFF", StringComparison.OrdinalIgnoreCase) || text == "0")
        {
            result = false;
            return true;
        }
        return bool.TryParse(text, out result);
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd,
        int attribute,
        ref int value,
        int valueSize);

    private sealed class SettingCommandState
    {
        public SettingCommandState(MonitorSettingRow row) => Row = row;
        public MonitorSettingRow Row { get; }
        public object? DesiredValue { get; set; }
        public int Revision { get; set; }
        public bool Running { get; set; }
    }
}

internal abstract class MonitorSettingRow : Control
{
    private bool _pending;
    private string? _error;
    private bool _confirmationPending;
    private object? _confirmationValue;
    private readonly System.Windows.Forms.Timer _confirmationTimer = new();

    protected MonitorSettingRow(
        string key,
        string displayName,
        bool writable,
        bool requiresConfirmation)
    {
        SettingKey = key;
        DisplayName = displayName;
        Writable = writable;
        RequiresConfirmation = requiresConfirmation;
        Height = 66;
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor,
            true);
        BackColor = Color.Transparent;
        DoubleBuffered = true;
        Enabled = writable;
        AccessibleName = displayName;

        _confirmationTimer.Interval = 3000;
        _confirmationTimer.Tick += (_, _) =>
        {
            _confirmationTimer.Stop();
            _confirmationPending = false;
            _confirmationValue = null;
            Invalidate();
        };
    }

    public string SettingKey { get; }
    public string DisplayName { get; }
    public bool Writable { get; }
    public bool RequiresConfirmation { get; }
    public event Action<MonitorSettingRow, object>? ValueRequested;

    public abstract void ApplyActualValue(object? value);

    protected bool ConfirmationPending => _confirmationPending;

    public void SetPending(bool pending)
    {
        _pending = pending;
        Invalidate();
    }

    public void SetError(string? error)
    {
        _error = error;
        Invalidate();
    }

    protected void RequestValue(object value)
    {
        if (!RequiresConfirmation)
        {
            ValueRequested?.Invoke(this, value);
            return;
        }

        if (!_confirmationPending)
        {
            _confirmationPending = true;
            _confirmationValue = value;
            _confirmationTimer.Stop();
            _confirmationTimer.Start();
            Invalidate();
            return;
        }

        _confirmationTimer.Stop();
        _confirmationPending = false;
        object confirmedValue = _confirmationValue ?? value;
        _confirmationValue = null;
        Invalidate();
        ValueRequested?.Invoke(this, confirmedValue);
    }

    protected void DrawHeader(Graphics graphics)
    {
        string labelText = _confirmationPending ? L.T("ClickAgainConfirm") : DisplayName;
        Color labelColor = _confirmationPending
            ? FlyoutColors.Warning
            : _error is null
                ? (Enabled ? FlyoutColors.PrimaryText : FlyoutColors.DisabledText)
                : FlyoutColors.Error;
        TextRenderer.DrawText(
            graphics,
            labelText,
            Font,
            new Rectangle(4, 1, Width - 34, 21),
            labelColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);

        if (_pending)
        {
            using var pendingBrush = new SolidBrush(FlyoutColors.Accent);
            graphics.FillEllipse(pendingBrush, Width - 18, 9, 6, 6);
        }
        else if (_error is not null)
        {
            using var errorBrush = new SolidBrush(FlyoutColors.Error);
            graphics.FillEllipse(errorBrush, Width - 19, 8, 8, 8);
        }
    }

    protected void DrawCompactStatus(Graphics graphics)
    {
        if (_pending)
        {
            using var pendingBrush = new SolidBrush(FlyoutColors.Accent);
            graphics.FillEllipse(pendingBrush, Width - 9, 4, 5, 5);
        }
        else if (_error is not null)
        {
            using var errorBrush = new SolidBrush(FlyoutColors.Error);
            graphics.FillEllipse(errorBrush, Width - 10, 3, 7, 7);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _confirmationTimer.Dispose();
        base.Dispose(disposing);
    }
}

internal sealed class MonitorSliderSettingRow : MonitorSettingRow
{
    private readonly MonitorValueSlider _slider = new();
    private readonly MonitorSunGlyphButton? _minimumButton;
    private readonly MonitorSunGlyphButton? _maximumButton;
    private readonly MonitorSettingGlyph? _leadingGlyph;
    private readonly MonitorSpeakerGlyphButton? _muteButton;
    private readonly ToolTip _toolTip = new();
    private readonly System.Windows.Forms.Timer _sendTimer = new();

    public MonitorSliderSettingRow(
        string key,
        string displayName,
        int minimum,
        int maximum,
        int value,
        bool writable,
        bool requiresConfirmation,
        bool showSunEndpoints,
        bool showMuteButton,
        bool muted,
        bool muteWritable)
        : base(key, displayName, writable, requiresConfirmation)
    {
        Height = 54;
        _slider.Minimum = minimum;
        _slider.Maximum = maximum;
        _slider.Value = value;
        _slider.ValueChanged += (_, _) =>
        {
            _sendTimer.Stop();
            _sendTimer.Start();
        };
        _slider.ValueCommitted += (_, _) =>
        {
            _sendTimer.Stop();
            RequestValue(_slider.Value);
        };
        Controls.Add(_slider);
        _toolTip.SetToolTip(_slider, displayName);

        if (showSunEndpoints)
        {
            _minimumButton = new MonitorSunGlyphButton(11F)
            {
                AccessibleName = L.T("MinimumBrightness")
            };
            _maximumButton = new MonitorSunGlyphButton(17F)
            {
                AccessibleName = L.T("MaximumBrightness")
            };
            _minimumButton.Click += (_, _) =>
            {
                _slider.ShowTargetValue(minimum);
                RequestValue(minimum);
            };
            _maximumButton.Click += (_, _) =>
            {
                _slider.ShowTargetValue(maximum);
                RequestValue(maximum);
            };
            Controls.AddRange([_minimumButton, _maximumButton]);
            _toolTip.SetToolTip(_minimumButton, L.T("SetMinimumBrightness"));
            _toolTip.SetToolTip(_maximumButton, L.T("SetMaximumBrightness"));
        }
        else if (showMuteButton)
        {
            _muteButton = new MonitorSpeakerGlyphButton(muted)
            {
                AccessibleName = muted ? L.T("Unmute") : L.T("Mute"),
                Enabled = muteWritable
            };
            _muteButton.Click += (_, _) =>
            {
                bool target = !_muteButton.Muted;
                _muteButton.Muted = target;
                _muteButton.AccessibleName = target ? L.T("Unmute") : L.T("Mute");
                _toolTip.SetToolTip(_muteButton, target ? L.T("Unmute") : L.T("Mute"));
                MuteRequested?.Invoke(this, target);
            };
            Controls.Add(_muteButton);
            _toolTip.SetToolTip(_muteButton, muted ? L.T("Unmute") : L.T("Mute"));
        }
        else
        {
            _leadingGlyph = new MonitorSettingGlyph(key)
            {
                AccessibleName = displayName
            };
            Controls.Add(_leadingGlyph);
            _toolTip.SetToolTip(_leadingGlyph, displayName);
        }

        _sendTimer.Interval = 120;
        _sendTimer.Tick += (_, _) =>
        {
            _sendTimer.Stop();
            RequestValue(_slider.Value);
        };
    }

    public event Action<MonitorSliderSettingRow, bool>? MuteRequested;

    public override void ApplyActualValue(object? value)
    {
        if (!TryInt(value, out int actual) || _slider.IsInteracting)
            return;
        _slider.Value = actual;
    }

    public void ApplyMuteActual(object? value)
    {
        if (_muteButton is null)
            return;
        bool muted = value is bool boolean
            ? boolean
            : string.Equals(Convert.ToString(value), "ON", StringComparison.OrdinalIgnoreCase) ||
              Convert.ToString(value) == "1";
        _muteButton.Muted = muted;
        _muteButton.AccessibleName = muted ? L.T("Unmute") : L.T("Mute");
        _toolTip.SetToolTip(_muteButton, muted ? L.T("Unmute") : L.T("Mute"));
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (_minimumButton is not null && _maximumButton is not null)
        {
            _minimumButton.Bounds = new Rectangle(0, 14, 38, 38);
            _maximumButton.Bounds = new Rectangle(Math.Max(0, Width - 38), 14, 38, 38);
            _slider.Bounds = new Rectangle(42, 2, Math.Max(20, Width - 84), 44);
        }
        else
        {
            Control? leading = (Control?)_muteButton ?? _leadingGlyph;
            if (leading is not null)
                leading.Bounds = new Rectangle(0, 14, 38, 38);
            _slider.Bounds = new Rectangle(42, 2, Math.Max(20, Width - 46), 44);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        DrawCompactStatus(e.Graphics);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _sendTimer.Dispose();
            _toolTip.Dispose();
        }
        base.Dispose(disposing);
    }

    private static bool TryInt(object? value, out int result)
    {
        try
        {
            result = Convert.ToInt32(value);
            return value is not null;
        }
        catch
        {
            result = 0;
            return false;
        }
    }
}

internal sealed class MonitorToggleSettingRow : MonitorSettingRow
{
    private bool _value;
    private bool _hovered;

    public MonitorToggleSettingRow(
        string key,
        string displayName,
        bool value,
        bool writable,
        bool requiresConfirmation)
        : base(key, displayName, writable, requiresConfirmation)
    {
        _value = value;
        Height = 48;
        Cursor = writable ? Cursors.Hand : Cursors.Default;
    }

    public override void ApplyActualValue(object? value)
    {
        if (value is bool boolean)
            _value = boolean;
        else
        {
            string text = Convert.ToString(value) ?? string.Empty;
            _value = text.Equals("ON", StringComparison.OrdinalIgnoreCase) || text == "1";
        }
        Invalidate();
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hovered = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hovered = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (!Enabled || e.Button != MouseButtons.Left)
            return;
        if (ConfirmationPending)
        {
            RequestValue(_value);
            return;
        }
        _value = !_value;
        Invalidate();
        RequestValue(_value);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        if (_hovered && Enabled)
        {
            using GraphicsPath hover = MonitorFlyoutDrawing.RoundedRectangle(
                new Rectangle(0, 2, Width - 1, Height - 4), 7);
            using var hoverBrush = new SolidBrush(FlyoutColors.Hover);
            e.Graphics.FillPath(hoverBrush, hover);
        }
        DrawHeader(e.Graphics);

        Rectangle toggle = new(Width - 52, 12, 44, 24);
        using GraphicsPath togglePath = MonitorFlyoutDrawing.RoundedRectangle(toggle, 12);
        using var toggleBrush = new SolidBrush(
            _value && Enabled ? FlyoutColors.Accent : FlyoutColors.InactiveTrack);
        e.Graphics.FillPath(toggleBrush, togglePath);
        int knobX = _value ? toggle.Right - 20 : toggle.Left + 4;
        using var knobBrush = new SolidBrush(Enabled ? Color.White : FlyoutColors.DisabledText);
        e.Graphics.FillEllipse(knobBrush, knobX, toggle.Top + 4, 16, 16);
    }
}

internal sealed class MonitorChoiceSettingRow : MonitorSettingRow
{
    private readonly IReadOnlyList<string> _options;
    private string _value;
    private bool _hovered;

    public MonitorChoiceSettingRow(
        string key,
        string displayName,
        IReadOnlyList<string> options,
        string value,
        bool writable,
        bool requiresConfirmation)
        : base(key, displayName, writable, requiresConfirmation)
    {
        _options = options;
        _value = value;
        Height = 52;
        Cursor = writable ? Cursors.Hand : Cursors.Default;
    }

    public override void ApplyActualValue(object? value)
    {
        _value = Convert.ToString(value) ?? string.Empty;
        Invalidate();
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hovered = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hovered = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (!Enabled || e.Button != MouseButtons.Left || _options.Count == 0)
            return;
        if (ConfirmationPending)
        {
            RequestValue(_value);
            return;
        }
        int current = IndexOf(_value);
        _value = _options[(current + 1) % _options.Count];
        Invalidate();
        RequestValue(_value);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        Rectangle button = new(Math.Max(150, Width - 178), 8, Math.Min(170, Width - 158), 36);
        if (_hovered && Enabled)
        {
            using GraphicsPath path = MonitorFlyoutDrawing.RoundedRectangle(button, 7);
            using var brush = new SolidBrush(FlyoutColors.Hover);
            e.Graphics.FillPath(brush, path);
        }

        DrawHeader(e.Graphics);
        TextRenderer.DrawText(
            e.Graphics,
            SettingPresentation.DisplayOption(_value),
            Font,
            new Rectangle(button.Left + 8, button.Top, button.Width - 29, button.Height),
            Enabled ? FlyoutColors.PrimaryText : FlyoutColors.DisabledText,
            TextFormatFlags.Right | TextFormatFlags.VerticalCenter |
            TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
        using var chevronFont = new Font("Segoe UI Symbol", 13F);
        TextRenderer.DrawText(
            e.Graphics,
            "›",
            chevronFont,
            new Rectangle(button.Right - 23, button.Top, 18, button.Height),
            Enabled ? FlyoutColors.PrimaryText : FlyoutColors.DisabledText,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    private int IndexOf(string value)
    {
        for (int i = 0; i < _options.Count; i++)
        {
            if (string.Equals(_options[i], value, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }
}

internal sealed class MonitorValueSlider : Control
{
    private int _minimum;
    private int _maximum = 100;
    private int _value;
    private bool _dragging;
    private bool _showValueBubble;
    private readonly System.Windows.Forms.Timer _bubbleTimer = new();

    public MonitorValueSlider()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor,
            true);
        DoubleBuffered = true;
        BackColor = Color.Transparent;
        Cursor = Cursors.Hand;
        _bubbleTimer.Interval = 750;
        _bubbleTimer.Tick += (_, _) =>
        {
            _bubbleTimer.Stop();
            _showValueBubble = false;
            Invalidate();
        };
    }

    [DefaultValue(0)]
    public int Minimum
    {
        get => _minimum;
        set { _minimum = value; Value = _value; Invalidate(); }
    }

    [DefaultValue(100)]
    public int Maximum
    {
        get => _maximum;
        set { _maximum = Math.Max(value, _minimum + 1); Value = _value; Invalidate(); }
    }

    [DefaultValue(0)]
    public int Value
    {
        get => _value;
        set
        {
            int next = Math.Clamp(value, _minimum, _maximum);
            if (_value == next)
                return;
            _value = next;
            Invalidate();
        }
    }

    public bool IsInteracting => _dragging;
    public event EventHandler? ValueChanged;
    public event EventHandler? ValueCommitted;

    public void ShowTargetValue(int value)
    {
        Value = value;
        _bubbleTimer.Stop();
        _showValueBubble = true;
        _bubbleTimer.Start();
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        const int railPadding = 10;
        int railLeft = railPadding;
        int railRight = Width - railPadding;
        int railWidth = Math.Max(1, railRight - railLeft);
        int centerY = Height - 13;
        float ratio = (float)(_value - _minimum) / (_maximum - _minimum);
        int thumbX = railLeft + (int)Math.Round(railWidth * ratio);

        using var inactive = new Pen(FlyoutColors.InactiveTrack, 4)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        using var active = new Pen(
            Enabled ? FlyoutColors.Accent : FlyoutColors.DisabledAccent, 4)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        graphics.DrawLine(inactive, railLeft, centerY, railRight, centerY);
        graphics.DrawLine(active, railLeft, centerY, thumbX, centerY);

        using var outer = new SolidBrush(FlyoutColors.ThumbOuter);
        using var inner = new SolidBrush(
            Enabled ? FlyoutColors.Accent : FlyoutColors.DisabledAccent);
        graphics.FillEllipse(outer, thumbX - 9, centerY - 9, 18, 18);
        graphics.FillEllipse(inner, thumbX - 5, centerY - 5, 10, 10);

        if (_showValueBubble)
            DrawBubble(graphics, thumbX);
    }

    private void DrawBubble(Graphics graphics, int thumbX)
    {
        string text = _value.ToString();
        using var font = new Font("Segoe UI Variable Text", 8.5F, FontStyle.Bold);
        Size measured = TextRenderer.MeasureText(text, font);
        int width = Math.Max(32, measured.Width + 12);
        int x = Math.Clamp(thumbX - width / 2, 0, Width - width);
        Rectangle bounds = new(x, 0, width, 23);
        using GraphicsPath path = MonitorFlyoutDrawing.RoundedRectangle(bounds, 6);
        using var brush = new SolidBrush(FlyoutColors.Bubble);
        using var border = new Pen(FlyoutColors.Border);
        graphics.FillPath(brush, path);
        graphics.DrawPath(border, path);
        TextRenderer.DrawText(
            graphics,
            text,
            font,
            bounds,
            Color.White,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
            TextFormatFlags.SingleLine);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (!Enabled || e.Button != MouseButtons.Left)
            return;
        _bubbleTimer.Stop();
        _showValueBubble = true;
        _dragging = true;
        Capture = true;
        UpdateFromMouse(e.X);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragging)
            UpdateFromMouse(e.X);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (_dragging)
        {
            UpdateFromMouse(e.X);
            _dragging = false;
            Capture = false;
            _bubbleTimer.Stop();
            _bubbleTimer.Start();
            ValueCommitted?.Invoke(this, EventArgs.Empty);
        }
        base.OnMouseUp(e);
    }

    private void UpdateFromMouse(int x)
    {
        const int left = 10;
        int right = Width - 10;
        float ratio = Math.Clamp((float)(x - left) / Math.Max(1, right - left), 0F, 1F);
        int next = _minimum + (int)Math.Round((_maximum - _minimum) * ratio);
        if (next == _value)
            return;
        _value = next;
        Invalidate();
        ValueChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _bubbleTimer.Dispose();
        base.Dispose(disposing);
    }
}

internal sealed class MonitorSettingGlyph : Control
{
    private readonly string _key;

    public MonitorSettingGlyph(string key)
    {
        _key = key;
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.SupportsTransparentBackColor,
            true);
        BackColor = Color.Transparent;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        Color color = Enabled ? FlyoutColors.PrimaryText : FlyoutColors.DisabledText;
        using var pen = new Pen(color, 1.5F)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        string key = _key.ToLowerInvariant();

        if (key.Contains("contrast") || key.Equals("brightness", StringComparison.Ordinal))
        {
            Rectangle circle = new(10, 10, 18, 18);
            graphics.DrawEllipse(pen, circle);
            using var fill = new SolidBrush(Color.FromArgb(120, color));
            graphics.FillPie(fill, circle, 90, 180);
        }
        else if (key.Contains("colorstrength"))
        {
            using var dim = new SolidBrush(Color.FromArgb(110, color));
            graphics.FillEllipse(dim, 8, 14, 12, 12);
            graphics.FillEllipse(dim, 18, 14, 12, 12);
            graphics.DrawEllipse(pen, 13, 8, 12, 12);
        }
        else if (key.Contains("colortint"))
        {
            graphics.DrawEllipse(pen, 8, 11, 16, 16);
            graphics.DrawEllipse(pen, 15, 11, 16, 16);
        }
        else if (key.Contains("sharp"))
        {
            graphics.DrawLines(pen,
            [
                new Point(7, 25),
                new Point(14, 17),
                new Point(19, 22),
                new Point(30, 10)
            ]);
        }
        else
        {
            graphics.DrawLine(pen, 7, 11, 31, 11);
            graphics.DrawLine(pen, 7, 19, 31, 19);
            graphics.DrawLine(pen, 7, 27, 31, 27);
            using var brush = new SolidBrush(color);
            graphics.FillEllipse(brush, 13, 8, 6, 6);
            graphics.FillEllipse(brush, 23, 16, 6, 6);
            graphics.FillEllipse(brush, 10, 24, 6, 6);
        }
    }
}

internal sealed class MonitorSpeakerGlyphButton : Control
{
    private bool _muted;
    private bool _hovered;
    private bool _pressed;

    public MonitorSpeakerGlyphButton(bool muted)
    {
        _muted = muted;
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.SupportsTransparentBackColor,
            true);
        BackColor = Color.Transparent;
        Cursor = Cursors.Hand;
        TabStop = true;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Muted
    {
        get => _muted;
        set
        {
            if (_muted == value)
                return;
            _muted = value;
            Invalidate();
        }
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hovered = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hovered = false;
        _pressed = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && Enabled)
            _pressed = true;
        Invalidate();
        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        _pressed = false;
        Invalidate();
        base.OnMouseUp(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Space or Keys.Enter)
        {
            OnClick(EventArgs.Empty);
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        if ((_hovered || _pressed) && Enabled)
        {
            using GraphicsPath background = MonitorFlyoutDrawing.RoundedRectangle(
                new Rectangle(1, 1, Width - 3, Height - 3), 6);
            using var brush = new SolidBrush(_pressed ? FlyoutColors.Pressed : FlyoutColors.Hover);
            graphics.FillPath(brush, background);
        }

        Color color = Enabled ? FlyoutColors.PrimaryText : FlyoutColors.DisabledText;
        using var pen = new Pen(color, 1.55F)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };
        Point[] speaker =
        [
            new(8, 16), new(13, 16), new(20, 11),
            new(20, 27), new(13, 22), new(8, 22)
        ];
        graphics.DrawPolygon(pen, speaker);

        if (_muted)
        {
            graphics.DrawLine(pen, 24, 15, 31, 23);
            graphics.DrawLine(pen, 31, 15, 24, 23);
        }
        else
        {
            graphics.DrawArc(pen, 19, 14, 10, 10, -55, 110);
            graphics.DrawArc(pen, 18, 10, 17, 18, -50, 100);
        }
    }
}

internal sealed class MonitorSunGlyphButton : Control
{
    private readonly float _glyphSize;
    private bool _hovered;
    private bool _pressed;

    public MonitorSunGlyphButton(float glyphSize)
    {
        _glyphSize = glyphSize;
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.SupportsTransparentBackColor,
            true);
        BackColor = Color.Transparent;
        Cursor = Cursors.Hand;
        TabStop = true;
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hovered = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hovered = false;
        _pressed = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && Enabled)
            _pressed = true;
        Invalidate();
        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        _pressed = false;
        Invalidate();
        base.OnMouseUp(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Space or Keys.Enter)
        {
            OnClick(EventArgs.Empty);
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        if ((_hovered || _pressed) && Enabled)
        {
            using GraphicsPath background = MonitorFlyoutDrawing.RoundedRectangle(
                new Rectangle(1, 1, Width - 3, Height - 3), 6);
            using var brush = new SolidBrush(_pressed ? FlyoutColors.Pressed : FlyoutColors.Hover);
            e.Graphics.FillPath(brush, background);
        }

        Color color = Enabled ? FlyoutColors.PrimaryText : FlyoutColors.DisabledText;
        float cx = Width / 2F;
        float cy = Height / 2F;
        float core = _glyphSize * .18F;
        float rayStart = _glyphSize * .30F;
        float rayEnd = _glyphSize * .48F;
        using var pen = new Pen(color, 1.45F)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        e.Graphics.DrawEllipse(pen, cx - core, cy - core, core * 2, core * 2);
        for (int i = 0; i < 8; i++)
        {
            double angle = i * Math.PI / 4D;
            e.Graphics.DrawLine(
                pen,
                cx + (float)Math.Cos(angle) * rayStart,
                cy + (float)Math.Sin(angle) * rayStart,
                cx + (float)Math.Cos(angle) * rayEnd,
                cy + (float)Math.Sin(angle) * rayEnd);
        }
    }
}

internal static class SettingPresentation
{
    private static readonly HashSet<string> HiddenSettings = new(StringComparer.OrdinalIgnoreCase)
    {
        "ecoSensor",
        "energySaving",
        "inputSource"
    };

    private static readonly Dictionary<string, string> Names = new(StringComparer.OrdinalIgnoreCase)
    {
        ["backlight"] = "亮度",
        ["brightness"] = "黑色级别",
        ["contrast"] = "对比度",
        ["colorStrength"] = "色彩",
        ["colorTint"] = "色调",
        ["sharpness"] = "锐度",
        ["pictureMode"] = "图像模式",
        ["colorEffect"] = "色彩效果",
        ["volume"] = "音量",
        ["mute"] = "静音",
        ["soundMode"] = "声音模式",
        ["ecoSensor"] = "环境光感应",
        ["energySaving"] = "节能模式",
        ["brightnessOptimization"] = "亮度优化",
        ["motionLighting"] = "运动照明",
        ["inputSource"] = "输入源"
    };

    private static readonly string[] PreferredOrder =
    [
        "backlight", "contrast", "colorStrength", "colorTint", "sharpness",
        "brightness", "pictureMode", "colorEffect",
        "volume", "mute", "soundMode",
        "ecoSensor", "energySaving", "brightnessOptimization", "motionLighting",
        "inputSource"
    ];

    public static string DisplayName(string key)
        => key.Equals("backlight", StringComparison.OrdinalIgnoreCase)
            ? L.T("Brightness")
            : Names.TryGetValue(key, out string? name) ? name : SplitIdentifier(key);

    public static bool IsHidden(string key) => HiddenSettings.Contains(key);

    public static string Section(string key, bool experimental = false)
    {
        if (experimental)
            return "高级画面";

        string lower = key.ToLowerInvariant();
        if (lower.Contains("source") || lower.Contains("input"))
            return "输入源";
        if (lower.Contains("volume") || lower.Contains("mute") ||
            lower.Contains("sound") || lower.Contains("audio"))
            return "声音";
        if (lower.Contains("eco") || lower.Contains("energy") ||
            lower.Contains("ambient") || lower.Contains("optimization") ||
            lower.Contains("motionlighting"))
            return "节能";
        return "画面";
    }

    public static int SectionOrder(string section) => section switch
    {
        "画面" => 0,
        "高级画面" => 1,
        "声音" => 2,
        "节能" => 3,
        "输入源" => 4,
        _ => 4
    };

    public static int SortOrder(string key)
    {
        for (int i = 0; i < PreferredOrder.Length; i++)
        {
            if (string.Equals(PreferredOrder[i], key, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return 1000;
    }

    public static string DisplayOption(string option) => option.ToUpperInvariant() switch
    {
        "ON" => "开启",
        "OFF" => "关闭",
        "LOW" => "低",
        "MEDIUM" => "中",
        "HIGH" => "高",
        "AUTO" => "自动",
        "MINIMUM" => "最低",
        "MAXIMUM" => "最高",
        "CUSTOM" => "自定义",
        "ADAPTIVE" => "自适应",
        "STANDARD" => "标准",
        "DYNAMIC" => "动态",
        "MOVIE" => "电影",
        "FILMMAKER" => "电影制作人",
        "GAME" => "游戏",
        _ => option
    };

    /// <summary>
    /// Compatibility hook for destructive capabilities.  When the protocol
    /// grows a RequiresConfirmation flag, this key-based fallback can be OR'ed
    /// with that field without changing any of the row controls.
    /// </summary>
    public static bool RequiresConfirmation(string key)
    {
        string normalized = key.Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .ToLowerInvariant();
        return normalized.Contains("factoryreset", StringComparison.Ordinal) ||
               normalized.Contains("resetall", StringComparison.Ordinal) ||
               normalized.Contains("resetpicture", StringComparison.Ordinal) ||
               normalized.Contains("resetsound", StringComparison.Ordinal) ||
               normalized is "poweroff" or "paneloff" or "servicemode" or "inputsource";
    }

    private static string SplitIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "设置";
        var result = new System.Text.StringBuilder(value.Length + 8);
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (i > 0 && char.IsUpper(c) && !char.IsWhiteSpace(value[i - 1]))
                result.Append(' ');
            result.Append(c);
        }
        return result.ToString();
    }
}

internal static class FlyoutColors
{
    public static readonly Color Surface = Color.FromArgb(44, 44, 44);
    public static readonly Color Border = Color.FromArgb(74, 74, 74);
    public static readonly Color PrimaryText = Color.FromArgb(230, 230, 230);
    public static readonly Color SecondaryText = Color.FromArgb(166, 166, 166);
    public static readonly Color DisabledText = Color.FromArgb(105, 105, 105);
    public static readonly Color Connected = Color.FromArgb(113, 210, 143);
    public static readonly Color Error = Color.FromArgb(255, 128, 128);
    public static readonly Color Warning = Color.FromArgb(255, 196, 92);
    public static readonly Color Accent = Color.FromArgb(96, 205, 255);
    public static readonly Color DisabledAccent = Color.FromArgb(72, 112, 130);
    public static readonly Color InactiveTrack = Color.FromArgb(103, 103, 103);
    public static readonly Color ThumbOuter = Color.FromArgb(79, 79, 79);
    public static readonly Color Bubble = Color.FromArgb(66, 66, 66);
    public static readonly Color Hover = Color.FromArgb(64, 64, 64);
    public static readonly Color Pressed = Color.FromArgb(78, 78, 78);
}

internal static class MonitorFlyoutDrawing
{
    public static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
    {
        int diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        if (diameter <= 0)
            return new GraphicsPath();
        var path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
