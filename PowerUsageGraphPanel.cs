using System.Drawing.Drawing2D;

namespace AutoResolution;

internal sealed class PowerUsageGraphPanel : Panel
{
    public PowerSession? Session { get; set; }
    public PowerGraphMetric Metric { get; set; } = PowerGraphMetric.Power;

    public PowerUsageGraphPanel()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Color.Transparent;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var area = new Rectangle(42, 12, Width - 58, Height - 38);
        using var gridPen = new Pen(Color.FromArgb(100, ForeColor), 1);
        using var linePen = new Pen(Metric == PowerGraphMetric.Power
            ? Color.FromArgb(235, 150, 55)
            : Color.FromArgb(130, 90, 210), 2);
        using var textBrush = new SolidBrush(ForeColor);

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.DrawLine(gridPen, area.Left, area.Bottom, area.Right, area.Bottom);
        e.Graphics.DrawLine(gridPen, area.Left, area.Top, area.Left, area.Bottom);

        var values = Session?.PercentageTicks
            .Select(item => (Percentage: int.TryParse(item.Key, out var value) ? value : -1,
                Duration: TimeSpan.FromTicks(item.Value).TotalSeconds))
            .Where(item => item.Percentage >= 0)
            .OrderBy(item => item.Percentage)
            .ToList() ?? [];
        if (values.Count == 0)
        {
            e.Graphics.DrawString("Sin datos suficientes", Font, textBrush, area.Left + 8, area.Top + 8);
            return;
        }

        var energyPerPercent = Session?.BatteryCapacityWh / 100d ?? 0;
        var powerValues = values.Select(item => energyPerPercent > 0 && item.Duration > 0
            ? energyPerPercent / (item.Duration / 3600d)
            : 0).ToList();
        var metricValues = Metric switch
        {
            PowerGraphMetric.Power => powerValues,
            PowerGraphMetric.Energy => values
                .Select(item => Math.Abs(item.Percentage - (Session?.StartPercentage ?? item.Percentage)) * energyPerPercent)
                .ToList(),
            _ => values.Select(item => item.Duration).ToList()
        };
        if (metricValues.All(value => value <= 0))
        {
            e.Graphics.DrawString(
                Metric == PowerGraphMetric.Power
                    ? "Potencia no disponible: se necesita variación de porcentaje."
                    : "Sin datos suficientes para esta gráfica.",
                Font,
                textBrush,
                area.Left + 8,
                area.Top + 8);
            return;
        }
        var maxValue = Math.Max(metricValues.DefaultIfEmpty(0).Max(), 0.001);
        var points = values.Select((item, index) => new PointF(
            area.Left + (item.Percentage / 100f * area.Width),
            area.Bottom - (float)(metricValues[index] / maxValue * area.Height))).ToArray();
        if (points.Length > 1)
        {
            e.Graphics.DrawLines(linePen, points);
        }

        foreach (var point in points)
        {
            e.Graphics.FillEllipse(linePen.Brush, point.X - 3, point.Y - 3, 6, 6);
        }

        foreach (var percentage in new[] { 0, 25, 50, 75, 100 })
        {
            var x = area.Left + percentage / 100f * area.Width;
            e.Graphics.DrawLine(gridPen, x, area.Bottom, x, area.Bottom - 5);
            var text = $"{percentage}%";
            var size = e.Graphics.MeasureString(text, Font);
            e.Graphics.DrawString(text, Font, textBrush, x - size.Width / 2, area.Bottom + 5);
        }
        var unit = Metric switch
        {
            PowerGraphMetric.Power => "Potencia (W)",
            PowerGraphMetric.Energy => "Energía acumulada (Wh)",
            _ => "Tiempo (s)"
        };
        e.Graphics.DrawString($"{unit} 0-{maxValue:0.00}", Font, textBrush, area.Left, 0);
        e.Graphics.DrawString($"{maxValue:0.00}", Font, textBrush, 2, area.Top - 5);
        e.Graphics.DrawString("0", Font, textBrush, 10, area.Bottom - 8);
        e.Graphics.DrawString("0", Font, textBrush, area.Right + 5, area.Bottom - 8);
    }
}

internal enum PowerGraphMetric
{
    Power,
    Energy,
    Time
}
