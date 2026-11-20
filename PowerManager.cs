using System.Diagnostics;

namespace AutoResolution;

internal static class PowerManager
{
    private const string PowerSaverScheme = "a1841308-3541-4fab-bc81-f71556f20b4a";
    private const string HighPerformanceScheme = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
    private const string ProcessorSubgroup = "54533251-82be-4824-96c1-47b60b740d00";
    private const string MaximumProcessorState = "bc5038f7-23e0-4960-96da-33abaf5935ec";

    public static void SetBatterySaver(bool enabled)
    {
        var scheme = enabled ? PowerSaverScheme : HighPerformanceScheme;
        RunPowerCfg($"/setactive {scheme}");
        var activeScheme = RunPowerCfg("/getactivescheme");
        if (!activeScheme.Contains(scheme, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Windows no confirmó el cambio del plan de energía.");
        }

    }

    public static void SetProcessorLimits(int pluggedInLimit, int batteryLimit)
    {
        foreach (var scheme in new[] { PowerSaverScheme, HighPerformanceScheme })
        {
            RunPowerCfg($"/setacvalueindex {scheme} {ProcessorSubgroup} {MaximumProcessorState} {pluggedInLimit}");
            RunPowerCfg($"/setdcvalueindex {scheme} {ProcessorSubgroup} {MaximumProcessorState} {batteryLimit}");
        }
    }

    private static string RunPowerCfg(string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "powercfg.exe",
            Arguments = arguments,
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        }) ?? throw new InvalidOperationException("No se pudo iniciar powercfg.exe.");

        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            var error = process.StandardError.ReadToEnd().Trim();
            throw new InvalidOperationException($"Windows no pudo cambiar el plan de energía{(error.Length > 0 ? $": {error}" : ".")}");
        }

        return process.StandardOutput.ReadToEnd();
    }
}
