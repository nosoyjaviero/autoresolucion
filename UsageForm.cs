using System.Drawing;

namespace AutoResolution;

internal sealed class UsageForm : Form
{
    private readonly PowerUsageTracker tracker;
    private readonly AppSettings settings;
    private readonly Label summary = new();
    private readonly Label comparison = new();
    private readonly Label powerSummary = new();
    private readonly ListBox history = new() { IntegralHeight = false };
    private readonly PowerUsageGraphPanel graph = new();
    private readonly PowerUsageGraphPanel energyGraph = new() { Metric = PowerGraphMetric.Energy };
    private readonly PowerUsageGraphPanel timeGraph = new() { Metric = PowerGraphMetric.Time };
    private readonly Label powerGraphLabel = new();
    private readonly Label energyGraphLabel = new();
    private readonly Label timeGraphLabel = new();
    private readonly ComboBox sessionSelector = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly DataGridView percentageTable = new()
    {
        ReadOnly = true,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        RowHeadersVisible = false
    };
    private List<PowerSession> availableSessions = [];

    public UsageForm(PowerUsageTracker tracker, AppSettings settings)
    {
        this.tracker = tracker;
        this.settings = settings;
        Text = settings.English ? "Energy monitor" : "Monitor de energía";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(900, 980);
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimumSize = new Size(650, 500);

        summary.Location = new Point(24, 20);
        summary.AutoSize = true;
        summary.Font = new Font(Font, FontStyle.Bold);
        comparison.Location = new Point(24, 50);
        comparison.AutoSize = true;
        powerSummary.Location = new Point(24, 68);
        powerSummary.AutoSize = true;
        sessionSelector.Location = new Point(24, 98);
        sessionSelector.Size = new Size(820, 28);
        sessionSelector.SelectedIndexChanged += (_, _) => RefreshSelectedSession();
        powerGraphLabel.Text = settings.English ? "Average power by battery percentage (W)" : "Potencia media por porcentaje de batería (W)";
        powerGraphLabel.Location = new Point(24, 141);
        powerGraphLabel.AutoSize = true;
        graph.Location = new Point(24, 162);
        graph.Size = new Size(400, 220);
        energyGraphLabel.Text = settings.English ? "Accumulated estimated energy (Wh)" : "Energía estimada acumulada (Wh)";
        energyGraphLabel.Location = new Point(444, 141);
        energyGraphLabel.AutoSize = true;
        energyGraph.Location = new Point(444, 162);
        energyGraph.Size = new Size(400, 220);
        timeGraphLabel.Text = settings.English ? "Time at each percentage (seconds)" : "Tiempo en cada porcentaje (segundos)";
        timeGraphLabel.Location = new Point(24, 395);
        timeGraphLabel.AutoSize = true;
        timeGraph.Location = new Point(24, 416);
        timeGraph.Size = new Size(400, 220);
        percentageTable.Location = new Point(24, 655);
        percentageTable.Size = new Size(820, 190);
        percentageTable.Columns.Add("percentage", settings.English ? "Battery %" : "Porcentaje");
        percentageTable.Columns.Add("seconds", settings.English ? "Seconds at percentage" : "Segundos en el porcentaje");
        percentageTable.Columns.Add("change", settings.English ? "Change" : "Variación");
        percentageTable.Columns.Add("energy", settings.English ? "Energy per 1% (Wh)" : "Energía por 1% (Wh)");
        percentageTable.Columns.Add("power", settings.English ? "Average power (W)" : "Potencia media (W)");
        history.Location = new Point(24, 865);
        history.Size = new Size(820, 110);
        Controls.AddRange([summary, comparison, powerSummary, sessionSelector, powerGraphLabel, graph, energyGraphLabel, energyGraph, timeGraphLabel, timeGraph, percentageTable, history]);
        RefreshData();
    }

    public void RefreshData()
    {
        var totals = tracker.GetCurrentTotals();
        summary.Text = settings.English
            ? $"Plugged in: {Format(totals.plugged)}    Battery: {Format(totals.battery)}"
            : $"Con corriente: {Format(totals.plugged)}    Batería: {Format(totals.battery)}";
        var current = tracker.CurrentDrainRate;
        comparison.Text = settings.English
            ? $"Current drain: {(current is null ? "not enough data" : $"{current:0.0}%/h")}. Capacity: {FormatCapacity(tracker.GetGraphSession()?.BatteryCapacityWh)}. Active session: {FormatClock(tracker.CurrentSession?.Duration ?? TimeSpan.Zero)}."
            : $"Descarga actual: {(current is null ? "faltan datos" : $"{current:0.0}%/h")}. Capacidad: {FormatCapacity(tracker.GetGraphSession()?.BatteryCapacityWh)}. Sesión activa: {FormatClock(tracker.CurrentSession?.Duration ?? TimeSpan.Zero)}.";
        var selectedSession = tracker.GetGraphSession();
        powerSummary.Text = FormatPowerSummary(selectedSession);
        graph.Session = tracker.GetGraphSession();
        energyGraph.Session = tracker.GetGraphSession();
        timeGraph.Session = tracker.GetGraphSession();
        graph.ForeColor = ForeColor;
        graph.Invalidate();
        energyGraph.Invalidate();
        timeGraph.Invalidate();
        history.Items.Clear();
        availableSessions = (tracker.CurrentSession is null
            ? tracker.Sessions
            : new[] { tracker.CurrentSession }.Concat(tracker.Sessions)).Take(101).ToList();
        sessionSelector.Items.Clear();
        foreach (var session in availableSessions)
        {
            sessionSelector.Items.Add(FormatSession(session));
        }
        if (sessionSelector.Items.Count > 0) sessionSelector.SelectedIndex = 0;
        history.Items.AddRange(availableSessions.Take(10).Select(FormatSession).ToArray());
    }

    private void RefreshSelectedSession()
    {
        if (sessionSelector.SelectedIndex < 0 || sessionSelector.SelectedIndex >= availableSessions.Count) return;
        EnsurePercentageColumns();
        var session = availableSessions[sessionSelector.SelectedIndex];
        powerSummary.Text = FormatPowerSummary(session);
        graph.Session = session;
        energyGraph.Session = session;
        timeGraph.Session = session;
        graph.Invalidate();
        energyGraph.Invalidate();
        timeGraph.Invalidate();
        percentageTable.Rows.Clear();
        foreach (var item in session.PercentageTicks
                     .Select(item => new { Percentage = int.Parse(item.Key), Seconds = TimeSpan.FromTicks(item.Value).TotalSeconds })
                     .OrderByDescending(item => item.Percentage))
        {
            var energy = session.BatteryCapacityWh is null
                ? "N/D"
                : $"{session.BatteryCapacityWh.Value / 100:0.000}";
            var energyValue = session.BatteryCapacityWh / 100d;
            var averagePower = energyValue > 0 && item.Seconds > 0
                ? energyValue / (item.Seconds / 3600d)
                : 0;
            percentageTable.Rows.Add(
                $"{item.Percentage}%",
                $"{item.Seconds:0.0}",
                session.IsCharging ? "Carga" : "Descarga",
                energy,
                energyValue > 0 ? $"{averagePower:0.0}" : "N/D");
        }
    }

    private void EnsurePercentageColumns()
    {
        if (percentageTable.Columns.Count > 0)
        {
            return;
        }

        percentageTable.Columns.Add("percentage", settings.English ? "Battery %" : "Porcentaje");
        percentageTable.Columns.Add("seconds", settings.English ? "Seconds at percentage" : "Segundos en el porcentaje");
        percentageTable.Columns.Add("change", settings.English ? "Change" : "Variación");
        percentageTable.Columns.Add("energy", settings.English ? "Energy per 1% (Wh)" : "Energía por 1% (Wh)");
        percentageTable.Columns.Add("power", settings.English ? "Average power (W)" : "Potencia media (W)");
    }

    private string FormatSession(PowerSession session)
    {
        var kind = session.IsCharging
            ? (settings.English ? "Charging" : "Carga")
            : (settings.English ? "Discharging" : "Descarga");
        var rate = !session.IsCharging && session.Duration.TotalHours > 0
            ? $" | {(session.StartPercentage - session.EndPercentage) / session.Duration.TotalHours:0.0}%/h"
            : "";
        var energy = session.BatteryCapacityWh is null
            ? "N/D"
            : $"{Math.Abs(session.EndPercentage - session.StartPercentage) * session.BatteryCapacityWh.Value / 100:0.000} Wh";
        return $"{kind}: {session.StartPercentage}% -> {session.EndPercentage}% | {FormatSeconds(session.Duration)}{rate} | {energy} | CPU {session.ProcessorLimit}% | {session.StartedAt:yyyy-MM-dd HH:mm}";
    }

    private static string Format(TimeSpan time) =>
        time.TotalDays >= 1 ? $"{(int)time.TotalDays}d {time.Hours:00}h {time.Minutes:00}m" : $"{(int)time.TotalHours:00}h {time.Minutes:00}m";

    private static string FormatSeconds(TimeSpan time) => $"{(long)Math.Max(0, time.TotalSeconds):N0} s";

    private static string FormatClock(TimeSpan time) => $"{(int)time.TotalMinutes:00}:{time.Seconds:00}";

    private static string FormatCapacity(double? capacity) =>
        capacity is null ? "N/D" : $"{capacity.Value:0.0} Wh";

    private string FormatPowerSummary(PowerSession? session)
    {
        if (session is null || session.BatteryCapacityWh is null)
        {
            return settings.English
                ? "Power: unavailable (battery capacity not detected)."
                : "Potencia: no disponible (no se detectó la capacidad de la batería).";
        }

        var energy = Math.Abs(session.EndPercentage - session.StartPercentage) *
            session.BatteryCapacityWh.Value / 100d;
        var watts = session.Duration.TotalSeconds > 0 ? energy / session.Duration.TotalHours : 0;
        var type = session.IsCharging
            ? (settings.English ? "charging" : "cargando")
            : (settings.English ? "discharging" : "descargando");
        return $"{(settings.English ? "Estimated battery power" : "Potencia estimada de batería")} ({type}): {watts:0.00} W | {energy:0.000} Wh | CPU: {session.AverageCpuPercent:0.0}%";
    }
}
