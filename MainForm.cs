using Microsoft.Win32;
using System.Drawing;
using System.Runtime.InteropServices;

namespace AutoResolution;

internal sealed class MainForm : Form
{
    private readonly AppSettings settings = AppSettings.Load();
    private readonly ComboBox pluggedInCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 275 };
    private readonly ComboBox batteryCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 275 };
    private readonly TrackBar pluggedBrightness = CreateSlider();
    private readonly TrackBar batteryBrightness = CreateSlider();
    private readonly TrackBar pluggedProcessor = CreateSlider();
    private readonly TrackBar batteryProcessor = CreateSlider();
    private readonly Label pluggedBrightnessValue = new();
    private readonly Label batteryBrightnessValue = new();
    private readonly Label pluggedProcessorValue = new();
    private readonly Label batteryProcessorValue = new();
    private readonly CheckBox batterySaverCheck = new();
    private readonly CheckBox batteryDarkModeCheck = new();
    private readonly CheckBox startupCheck = new();
    private readonly Button themeButton = new() { AutoSize = true };
    private readonly Button languageButton = new() { AutoSize = true };
    private readonly Label titleLabel = new();
    private readonly GroupBox pluggedGroup = new();
    private readonly GroupBox batteryGroup = new();
    private readonly Label pluggedResolutionLabel = new();
    private readonly Label pluggedBrightnessLabel = new();
    private readonly Label pluggedProcessorLabel = new();
    private readonly Label batteryResolutionLabel = new();
    private readonly Label batteryBrightnessLabel = new();
    private readonly Label batteryProcessorLabel = new();
    private readonly Label footerLabel = new();
    private readonly GroupBox usageGroup = new();
    private readonly Label pluggedUsageLabel = new();
    private readonly Label batteryUsageLabel = new();
    private readonly Label usageHintLabel = new();
    private readonly Label currentSessionLabel = new();
    private readonly ListBox sessionHistory = new() { IntegralHeight = false };
    private readonly PowerUsageGraphPanel usageGraph = new();
    private readonly Button powerButton = new() { AutoSize = true };
    private readonly Button saveButton = new() { Text = "Guardar", AutoSize = true };
    private readonly Button closeButton = new() { Text = "Cerrar programa", AutoSize = true };
    private readonly NotifyIcon trayIcon;
    private readonly ContextMenuStrip trayMenu = new();
    private readonly System.Windows.Forms.Timer     powerStatusTimer = new() { Interval = 5000 };
    private readonly PowerUsageTracker usageTracker = new();
    private readonly Button usageButton = new() { AutoSize = true };
    private UsageForm? usageForm;
    private bool closing;
    private IReadOnlyList<DisplayResolution> resolutions = [];

    public MainForm()
    {
        Text = "AutoResolution";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(600, 930);
        ShowInTaskbar = false;

        trayIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "AutoResolution",
            Visible = true,
            ContextMenuStrip = trayMenu
        };
        trayIcon.DoubleClick += (_, _) => ShowWindow();
        trayMenu.Items.Add("Abrir", null, (_, _) => ShowWindow());
        trayMenu.Items.Add("Salir", null, (_, _) => ExitApplication());

        BuildUi();
        LoadResolutions();
        usageTracker.Start(IsPluggedIn(), GetBatteryPercentage(), CurrentProcessorLimit(), BatteryInfo.GetCapacityWh(), CpuUsageMonitor.GetPercent());
        UpdateUsageLabels();
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        SystemEvents.SessionSwitch += OnSessionSwitch;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        powerStatusTimer.Tick += (_, _) => ApplyPowerDependentFeatures();
        powerStatusTimer.Start();
        FormClosing += OnFormClosing;
        Resize += OnResize;
        ShowInTaskbar = true;
        ApplyPowerDependentFeatures();
    }

    private void BuildUi()
    {
        titleLabel.AutoSize = true;
        titleLabel.Location = new Point(24, 18);
        titleLabel.Font = new Font(Font, FontStyle.Bold);
        var actionPanel = new FlowLayoutPanel
        {
            Location = new Point(24, 48),
            Size = new Size(550, 42),
            WrapContents = false,
            AutoScroll = false,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };
        pluggedGroup.Text = "Con cargador conectado";
        pluggedGroup.Location = new Point(24, 105);
        pluggedGroup.Size = new Size(550, 250);
        pluggedResolutionLabel.Text = "Resolución:";
        pluggedResolutionLabel.AutoSize = true;
        pluggedResolutionLabel.Location = new Point(18, 30);
        pluggedInCombo.Location = new Point(120, 26);
        pluggedBrightnessLabel.Text = "Brillo con cargador (%):";
        pluggedBrightnessLabel.AutoSize = true;
        pluggedBrightnessLabel.Location = new Point(18, 75);
        ConfigureSlider(pluggedBrightness, settings.PluggedInBrightnessLevel, new Point(150, 65));
        ConfigureValueLabel(pluggedBrightnessValue, pluggedBrightness, new Point(430, 75));
        pluggedProcessorLabel.Text = "Límite del procesador (%):";
        pluggedProcessorLabel.AutoSize = true;
        pluggedProcessorLabel.Location = new Point(18, 120);
        ConfigureSlider(pluggedProcessor, settings.PluggedInProcessorLimit, new Point(150, 110));
        ConfigureValueLabel(pluggedProcessorValue, pluggedProcessor, new Point(430, 120));
        batterySaverCheck.Text = "Quitar ahorro de batería al conectar el cargador";
        batterySaverCheck.AutoSize = true;
        batterySaverCheck.Location = new Point(15, 115);
        batterySaverCheck.Checked = settings.AutomaticBatterySaver;
        pluggedGroup.Controls.AddRange([
            pluggedResolutionLabel, pluggedInCombo, pluggedBrightnessLabel, pluggedBrightness, pluggedBrightnessValue,
            pluggedProcessorLabel, pluggedProcessor, pluggedProcessorValue
        ]);

        batteryGroup.Text = "Con batería";
        batteryGroup.Location = new Point(24, 360);
        batteryGroup.Size = new Size(550, 220);
        batteryResolutionLabel.Text = "Resolución:";
        batteryResolutionLabel.AutoSize = true;
        batteryResolutionLabel.Location = new Point(18, 30);
        batteryCombo.Location = new Point(120, 26);
        batteryBrightnessLabel.Text = "Brillo con batería (%):";
        batteryBrightnessLabel.AutoSize = true;
        batteryBrightnessLabel.Location = new Point(18, 75);
        ConfigureSlider(batteryBrightness, settings.BatteryBrightnessLevel, new Point(150, 65));
        ConfigureValueLabel(batteryBrightnessValue, batteryBrightness, new Point(430, 75));
        batteryProcessorLabel.Text = "Límite del procesador (%):";
        batteryProcessorLabel.AutoSize = true;
        batteryProcessorLabel.Location = new Point(18, 120);
        ConfigureSlider(batteryProcessor, settings.BatteryProcessorLimit, new Point(150, 110));
        ConfigureValueLabel(batteryProcessorValue, batteryProcessor, new Point(430, 120));
        batterySaverCheck.Text = "Activar modo de ahorro de batería al desconectar";
        batterySaverCheck.AutoSize = true;
        batterySaverCheck.Location = new Point(18, 165);
        batterySaverCheck.Checked = settings.AutomaticBatterySaver;
        batteryDarkModeCheck.Text = "Activar modo oscuro con batería";
        batteryDarkModeCheck.AutoSize = true;
        batteryDarkModeCheck.Location = new Point(270, 165);
        batteryDarkModeCheck.Checked = settings.DarkModeOnBattery;
        batteryGroup.Controls.AddRange([
            batteryResolutionLabel, batteryCombo, batteryBrightnessLabel, batteryBrightness,
            batteryBrightnessValue, batteryProcessorLabel, batteryProcessor, batteryProcessorValue,
            batterySaverCheck, batteryDarkModeCheck
        ]);

        usageGroup.Text = "Tiempo de uso";
        usageGroup.Location = new Point(24, 605);
        usageGroup.Size = new Size(550, 330);
        pluggedUsageLabel.AutoSize = true;
        pluggedUsageLabel.Location = new Point(18, 30);
        batteryUsageLabel.AutoSize = true;
        batteryUsageLabel.Location = new Point(285, 30);
        usageHintLabel.AutoSize = true;
        usageHintLabel.Location = new Point(18, 70);
        currentSessionLabel.AutoSize = true;
        currentSessionLabel.Location = new Point(18, 95);
        sessionHistory.Location = new Point(18, 120);
        sessionHistory.Size = new Size(505, 35);
        usageGraph.Location = new Point(18, 160);
        usageGraph.Size = new Size(505, 155);
        usageGroup.Controls.AddRange([
            pluggedUsageLabel, batteryUsageLabel, usageHintLabel, currentSessionLabel, sessionHistory, usageGraph
        ]);
        usageGroup.Visible = false;

        startupCheck.Text = "Iniciar automáticamente con Windows";
        startupCheck.AutoSize = true;
        startupCheck.Location = new Point(24, 865);
        startupCheck.Checked = settings.StartWithWindows;
        powerButton.Click += (_, _) =>
        {
            settings.Enabled = !settings.Enabled;
            settings.Save();
            UpdatePowerButton();
            if (settings.Enabled)
            {
                ApplyResolutionForCurrentPowerState();
            }
            ApplyPowerDependentFeatures();
        };

        usageButton.Text = "Monitor de energía";
        usageButton.Click += (_, _) => OpenUsageForm();
        themeButton.Click += (_, _) =>
        {
            settings.DarkMode = !settings.DarkMode;
            settings.Save();
            ApplyTheme();
        };
        languageButton.Click += (_, _) =>
        {
            settings.English = !settings.English;
            settings.Save();
            ApplyLanguage();
            ApplyTheme();
        };
        saveButton.Click += (_, _) => SaveSettings(
            batterySaverCheck.Checked,
            startupCheck.Checked,
            batteryBrightness.Value,
            pluggedBrightness.Value,
            batteryProcessor.Value,
            pluggedProcessor.Value);
        closeButton.Click += (_, _) => ExitApplication();
        actionPanel.Controls.AddRange([usageButton, powerButton, saveButton, closeButton, themeButton, languageButton]);
        footerLabel.Text = "Los cambios se aplican automáticamente según la alimentación.";
        footerLabel.AutoSize = true;
        footerLabel.Location = new Point(24, 835);
        Controls.AddRange([titleLabel, actionPanel, pluggedGroup, batteryGroup, usageGroup, startupCheck, footerLabel]);
        ApplyLanguage();
        ApplyTheme();
        UpdatePowerButton();
    }

    private static TrackBar CreateSlider() => new()
    {
        Minimum = 1,
        Maximum = 100,
        TickFrequency = 10,
        Width = 300,
        Height = 40
    };

    private static void ConfigureSlider(TrackBar slider, int value, Point location)
    {
        slider.Value = Math.Clamp(value, slider.Minimum, slider.Maximum);
        slider.Location = location;
    }

    private static void ConfigureValueLabel(Label label, TrackBar slider, Point location)
    {
        label.AutoSize = true;
        label.Location = location;
        label.Text = $"{slider.Value}%";
        slider.ValueChanged += (_, _) => label.Text = $"{slider.Value}%";
    }

    private void LoadResolutions()
    {
        try
        {
            resolutions = DisplayManager.GetResolutions();
            pluggedInCombo.Items.AddRange(resolutions.Cast<object>().ToArray());
            batteryCombo.Items.AddRange(resolutions.Cast<object>().ToArray());
            SelectConfiguredResolution(pluggedInCombo, settings.PluggedInResolution);
            SelectConfiguredResolution(batteryCombo, settings.OnBatteryResolution);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "AutoResolution", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static void SelectConfiguredResolution(ComboBox combo, DisplayResolution? configured)
    {
        var index = configured is null
            ? 0
            : combo.Items.Cast<DisplayResolution>().ToList().FindIndex(item =>
                item.Width == configured.Width && item.Height == configured.Height && item.Frequency == configured.Frequency);
        combo.SelectedIndex = index >= 0 ? index : 0;
    }

    private void SaveSettings(
        bool automaticBatterySaver,
        bool startWithWindows,
        int batteryBrightnessLevel,
        int pluggedBrightnessLevel,
        int batteryProcessorLimit,
        int pluggedProcessorLimit)
    {
        settings.PluggedInResolution = pluggedInCombo.SelectedItem as DisplayResolution;
        settings.OnBatteryResolution = batteryCombo.SelectedItem as DisplayResolution;
        settings.AutomaticBatterySaver = automaticBatterySaver;
        settings.DarkModeOnBattery = batteryDarkModeCheck.Checked;
        settings.StartWithWindows = startWithWindows;
        settings.BatteryBrightnessLevel = batteryBrightnessLevel;
        settings.PluggedInBrightnessLevel = pluggedBrightnessLevel;
        settings.BatteryProcessorLimit = batteryProcessorLimit;
        settings.PluggedInProcessorLimit = pluggedProcessorLimit;
        settings.Save();
        StartupManager.SetEnabled(settings.StartWithWindows);
        if (settings.Enabled)
        {
            ApplyResolutionForCurrentPowerState();
        }
        ApplyPowerDependentFeatures();
        trayIcon.ShowBalloonTip(1500, "AutoResolution", "Configuración guardada.", ToolTipIcon.Info);
    }

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode is PowerModes.StatusChange or PowerModes.Resume)
        {
            ReapplyAfterPowerEvent();
        }
    }

    private void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        if (e.Reason is SessionSwitchReason.SessionUnlock or SessionSwitchReason.SessionLogon)
        {
            ReapplyAfterPowerEvent();
        }
    }

    private void ReapplyAfterPowerEvent()
    {
        if (IsDisposed)
        {
            return;
        }

        BeginInvoke(() =>
        {
            if (IsDisposed)
            {
                return;
            }

            if (settings.Enabled)
            {
                ApplyResolutionForCurrentPowerState();
            }

            ApplyPowerDependentFeatures();
        });
    }

    private void ApplyPowerDependentFeatures()
    {
        usageTracker.Update(IsPluggedIn(), GetBatteryPercentage(), CurrentProcessorLimit(), BatteryInfo.GetCapacityWh(), CpuUsageMonitor.GetPercent());
        UpdateUsageLabels();
        usageForm?.RefreshData();

        if (!settings.Enabled)
        {
            BrightnessManager.Restore();
            ApplyTheme();
            return;
        }

        if (settings.AutomaticBatterySaver)
        {
            try
            {
                PowerManager.SetBatterySaver(!IsPluggedIn());
            }
            catch (Exception ex)
            {
                trayIcon.ShowBalloonTip(4000, "AutoResolution", ex.Message, ToolTipIcon.Error);
            }
        }

        try
        {
            PowerManager.SetProcessorLimits(settings.PluggedInProcessorLimit, settings.BatteryProcessorLimit);
            if (IsPluggedIn())
            {
                BrightnessManager.Restore();
                BrightnessManager.ApplyConfigured(settings.PluggedInBrightnessLevel);
            }
            else
            {
                BrightnessManager.Dim(settings.BatteryBrightnessLevel);
            }
            ApplyTheme();
        }
        catch (Exception ex)
        {
            trayIcon.ShowBalloonTip(4000, "AutoResolution", ex.Message, ToolTipIcon.Error);
        }
    }

    private void ApplyResolutionForCurrentPowerState()
    {
        var resolution = IsPluggedIn() ? settings.PluggedInResolution : settings.OnBatteryResolution;
        if (resolution is null)
        {
            return;
        }

        try
        {
            DisplayManager.Apply(resolution);
        }
        catch (Exception ex)
        {
            trayIcon.ShowBalloonTip(3000, "AutoResolution", ex.Message, ToolTipIcon.Error);
        }
    }

    private static bool IsPluggedIn()
    {
        if (!GetSystemPowerStatus(out var status))
        {
            return SystemInformation.PowerStatus.PowerLineStatus == PowerLineStatus.Online;
        }

        return status.ACLineStatus == 1;
    }

    private static int GetBatteryPercentage()
    {
        if (GetSystemPowerStatus(out var status) && status.BatteryLifePercent <= 100)
        {
            return status.BatteryLifePercent;
        }

        var percentage = SystemInformation.PowerStatus.BatteryLifePercent;
        return percentage is >= 0 and <= 1 ? (int)Math.Round(percentage * 100) : 0;
    }

    [DllImport("kernel32.dll")]
    private static extern bool GetSystemPowerStatus(out SystemPowerStatus status);

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte Reserved;
        public int BatteryLifeTime;
        public int BatteryFullLifeTime;
    }
    private void UpdatePowerButton()
    {
        powerButton.Text = settings.Enabled ? "● Encendido" : "○ Apagado";
        powerButton.BackColor = settings.Enabled ? Color.LightGreen : SystemColors.Control;
    }

    private void ShowWindow()
    {
        ShowInTaskbar = true;
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    private void HideToTray()
    {
        WindowState = FormWindowState.Normal;
        ShowInTaskbar = false;
        Hide();
    }

    private void OnResize(object? sender, EventArgs e)
    {
        if (WindowState == FormWindowState.Minimized)
        {
            HideToTray();
        }
    }

    private void ExitApplication()
    {
        closing = true;
        Close();
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (!closing)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }

        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        powerStatusTimer.Stop();
        usageTracker.SaveSnapshot(GetBatteryPercentage());
        usageForm?.Close();
        BrightnessManager.Restore();
        trayIcon.Visible = false;
        trayIcon.Dispose();
        trayMenu.Dispose();
    }

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category == UserPreferenceCategory.General)
        {
            ApplyTheme();
        }
    }

    private void ApplyTheme()
    {
        var dark = settings.DarkMode || (!IsPluggedIn() && settings.DarkModeOnBattery);
        var backColor = dark ? Color.FromArgb(30, 33, 38) : Color.FromArgb(245, 247, 250);
        var foreColor = dark ? Color.FromArgb(235, 238, 242) : Color.FromArgb(35, 39, 47);
        ApplyTheme(this, backColor, foreColor);
        themeButton.Text = dark ? "☀ Claro" : "☾ Oscuro";
        languageButton.Text = settings.English ? "ES" : "EN";
        foreach (var button in new[] { usageButton, powerButton, saveButton, closeButton, themeButton, languageButton })
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = dark ? Color.FromArgb(55, 61, 70) : Color.FromArgb(225, 231, 239);
        }
        powerButton.BackColor = settings.Enabled
            ? (dark ? Color.FromArgb(44, 130, 92) : Color.LightGreen)
            : (dark ? Color.FromArgb(55, 61, 70) : SystemColors.Control);
        usageButton.Text = settings.English ? "Energy monitor" : "Monitor de energía";
    }

    private static void ApplyTheme(Control control, Color backColor, Color foreColor)
    {
        control.BackColor = backColor;
        control.ForeColor = foreColor;
        foreach (Control child in control.Controls)
        {
            ApplyTheme(child, backColor, foreColor);
        }
    }

    private void ApplyLanguage()
    {
        var english = settings.English;
        titleLabel.Text = english ? "Automatic power configuration" : "Configuración automática por energía";
        pluggedGroup.Text = english ? "Plugged in" : "Con cargador conectado";
        batteryGroup.Text = english ? "On battery" : "Con batería";
        pluggedResolutionLabel.Text = english ? "Resolution:" : "Resolución:";
        pluggedBrightnessLabel.Text = english ? "Brightness while plugged (%):" : "Brillo con cargador (%):";
        pluggedProcessorLabel.Text = english ? "Maximum processor (%):" : "Límite del procesador (%):";
        batteryResolutionLabel.Text = english ? "Resolution:" : "Resolución:";
        batteryBrightnessLabel.Text = english ? "Brightness on battery (%):" : "Brillo con batería (%):";
        batteryProcessorLabel.Text = english ? "Maximum processor (%):" : "Límite del procesador (%):";
        batterySaverCheck.Text = english ? "Enable battery saver mode" : "Activar modo de ahorro de batería";
        batteryDarkModeCheck.Text = english ? "Enable dark mode on battery" : "Activar modo oscuro con batería";
        usageGroup.Text = english ? "Usage time" : "Tiempo de uso";
        usageHintLabel.Text = english
            ? "Battery rate is estimated from percentage per hour."
            : "El consumo se estima según el porcentaje perdido por hora.";
        currentSessionLabel.Text = english ? "Recent sessions:" : "Sesiones recientes:";
        startupCheck.Text = english ? "Start automatically with Windows" : "Iniciar automáticamente con Windows";
        footerLabel.Text = english
            ? "Settings are applied automatically based on the power source."
            : "Los cambios se aplican automáticamente según la alimentación.";
        powerButton.Text = settings.Enabled
            ? (english ? "● Enabled" : "● Encendido")
            : (english ? "○ Disabled" : "○ Apagado");
        themeButton.Text = settings.DarkMode ? "☀ Light" : "☾ Dark";
        languageButton.Text = english ? "ES" : "EN";
        UpdateUsageLabels();
    }

    private void OpenUsageForm()
    {
        if (usageForm is null || usageForm.IsDisposed)
        {
            usageForm = new UsageForm(usageTracker, settings);
        }

        usageForm.RefreshData();
        usageForm.Show(this);
        usageForm.BringToFront();
    }

    private void UpdateUsageLabels()
    {
        var totals = usageTracker.GetCurrentTotals();
        pluggedUsageLabel.Text = $"{(settings.English ? "Plugged in" : "Con corriente")}: {FormatDuration(totals.plugged)}";
        batteryUsageLabel.Text = $"{(settings.English ? "On battery" : "Con batería")}: {FormatDuration(totals.battery)}";
        currentSessionLabel.Text = FormatCurrentSession();
        usageGraph.Session = usageTracker.GetGraphSession();
        usageGraph.ForeColor = ForeColor;
        usageGraph.Invalidate();
        var average = usageTracker.GetAverageBatteryDrainRate(settings.BatteryProcessorLimit);
        usageHintLabel.Text = settings.English
            ? $"Estimated drain at CPU {settings.BatteryProcessorLimit}%: {(average is null ? "not enough data" : $"{average:0.0}%/h")}"
            : $"Descarga estimada con CPU al {settings.BatteryProcessorLimit}%: {(average is null ? "faltan datos" : $"{average:0.0}%/h")}";
        sessionHistory.Items.Clear();
        foreach (var session in usageTracker.Sessions.Take(5))
        {
            sessionHistory.Items.Add(FormatSession(session));
        }
    }

    private string FormatCurrentSession()
    {
        var session = usageTracker.CurrentSession;
        if (session is null)
        {
            return settings.English ? "No active session." : "No hay una sesión activa.";
        }

        var currentPercentage = GetBatteryPercentage();
        var kind = session.IsCharging
            ? (settings.English ? "Charging" : "Cargando")
            : (settings.English ? "Discharging" : "Descargando");
        return $"{kind}: {session.StartPercentage}% -> {currentPercentage}% ({FormatDuration(DateTimeOffset.Now - session.StartedAt)})";
    }

    private string FormatSession(PowerSession session)
    {
        var kind = session.IsCharging
            ? (settings.English ? "Carga" : "Carga")
            : (settings.English ? "Descarga" : "Descarga");
        var rate = session.IsCharging || session.Duration.TotalHours < 0.1
            ? ""
            : $" | {Math.Abs(session.EndPercentage - session.StartPercentage) / session.Duration.TotalHours:0.0}%/h";
        return $"{kind}: {session.StartPercentage}% -> {session.EndPercentage}% | {FormatDuration(session.Duration)}{rate} | CPU {session.ProcessorLimit}% | {session.StartedAt:yyyy-MM-dd HH:mm}";
    }

    private int CurrentProcessorLimit() =>
        IsPluggedIn() ? settings.PluggedInProcessorLimit : settings.BatteryProcessorLimit;

    private static string FormatDuration(TimeSpan duration)
    {
        return duration.TotalDays >= 1
            ? $"{(int)duration.TotalDays}d {duration.Hours:00}h {duration.Minutes:00}m"
            : $"{(int)duration.TotalHours:00}h {duration.Minutes:00}m";
    }
}
