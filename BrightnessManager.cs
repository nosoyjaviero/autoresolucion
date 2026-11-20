using System.Management;

namespace AutoResolution;

internal static class BrightnessManager
{
    private static IReadOnlyList<byte>? originalBrightness;

    public static void Dim(int brightness)
    {
        if (originalBrightness is null)
        {
            originalBrightness = GetCurrentBrightness();
        }

        SetBrightness(Enumerable.Repeat((byte)brightness, originalBrightness.Count).ToList());
    }

    public static void ApplyConfigured(int brightness)
    {
        var monitorCount = GetBrightnessMethods().Count;
        SetBrightness(Enumerable.Repeat((byte)brightness, monitorCount).ToList());
    }

    public static void Restore()
    {
        if (originalBrightness is null)
        {
            return;
        }

        SetBrightness(originalBrightness);
        originalBrightness = null;
    }

    private static IReadOnlyList<byte> GetCurrentBrightness()
    {
        using var searcher = new ManagementObjectSearcher(
            @"root\WMI",
            "SELECT CurrentBrightness FROM WmiMonitorBrightness");
        var values = searcher.Get()
            .Cast<ManagementObject>()
            .Select(item => Convert.ToByte(item["CurrentBrightness"]))
            .ToList();

        if (values.Count == 0)
        {
            throw new InvalidOperationException("Windows no encontró un monitor compatible con el control de brillo.");
        }

        return values;
    }

    private static void SetBrightness(IReadOnlyList<byte> brightnessValues)
    {
        var monitors = GetBrightnessMethods();
        if (monitors.Count == 0)
        {
            throw new InvalidOperationException("Windows no encontró un monitor compatible con el control de brillo.");
        }
        if (brightnessValues.Count == 0)
        {
            throw new InvalidOperationException("Windows no pudo determinar el brillo que debe aplicar.");
        }

        for (var index = 0; index < monitors.Count; index++)
        {
            var brightness = brightnessValues[Math.Min(index, brightnessValues.Count - 1)];
            monitors[index].InvokeMethod("WmiSetBrightness", new object[] { (byte)1, brightness });
        }
    }

    private static List<ManagementObject> GetBrightnessMethods()
    {
        using var searcher = new ManagementObjectSearcher(
            @"root\WMI",
            "SELECT * FROM WmiMonitorBrightnessMethods");
        return searcher.Get().Cast<ManagementObject>().ToList();
    }
}
