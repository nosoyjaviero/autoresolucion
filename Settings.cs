using System.Text.Json;

namespace AutoResolution;

internal sealed class AppSettings
{
    public bool Enabled { get; set; } = true;
    public bool AutomaticBatterySaver { get; set; } = true;
    public bool DarkModeOnBattery { get; set; }
    public bool DarkMode { get; set; }
    public bool English { get; set; }
    public bool StartWithWindows { get; set; }
    public int BatteryBrightnessLevel { get; set; } = 50;
    public int PluggedInBrightnessLevel { get; set; } = 100;
    public int PluggedInProcessorLimit { get; set; } = 100;
    public int BatteryProcessorLimit { get; set; } = 80;
    public DisplayResolution? PluggedInResolution { get; set; }
    public DisplayResolution? OnBatteryResolution { get; set; }

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AutoResolution",
        "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                return JsonFileStore.Load<AppSettings>(FilePath) ?? new AppSettings();
            }
        }
        catch (JsonException)
        {
            // A damaged settings file is replaced by defaults when the user saves.
        }

        return new AppSettings();
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        JsonFileStore.Save(FilePath, this);
    }
}
