using System.Management;

namespace AutoResolution;

internal static class BatteryInfo
{
    public static double? GetCapacityWh()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT DesignCapacity, FullChargeCapacity FROM Win32_Battery");
            var batteries = searcher.Get().Cast<ManagementObject>().ToList();
            var capacities = batteries
                .Select(item => Convert.ToDouble(item["FullChargeCapacity"] ?? item["DesignCapacity"] ?? 0))
                .Where(value => value > 0)
                .ToList();

            if (capacities.Count > 0)
            {
                return capacities.Sum() / 1000d;
            }

            using var wmiSearcher = new ManagementObjectSearcher(
                @"root\WMI",
                "SELECT FullChargedCapacity FROM BatteryFullChargedCapacity");
            var fullCapacity = wmiSearcher.Get()
                .Cast<ManagementObject>()
                .Select(item => Convert.ToDouble(item["FullChargedCapacity"] ?? 0))
                .Where(value => value > 0)
                .Sum();
            return fullCapacity > 0 ? fullCapacity / 1000d : null;
        }
        catch (ManagementException)
        {
            return null;
        }
    }
}
