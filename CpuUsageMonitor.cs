using System.Runtime.InteropServices;

namespace AutoResolution;

internal static class CpuUsageMonitor
{
    private static ulong lastIdle;
    private static ulong lastKernel;
    private static ulong lastUser;

    public static double GetPercent()
    {
        if (!GetSystemTimes(out var idle, out var kernel, out var user))
        {
            return 0;
        }

        var idleValue = ToUInt64(idle);
        var kernelValue = ToUInt64(kernel);
        var userValue = ToUInt64(user);
        var totalDelta = (kernelValue - lastKernel) + (userValue - lastUser);
        var idleDelta = idleValue - lastIdle;
        lastIdle = idleValue;
        lastKernel = kernelValue;
        lastUser = userValue;
        return totalDelta == 0 ? 0 : Math.Clamp((1d - idleDelta / (double)totalDelta) * 100d, 0, 100);
    }

    private static ulong ToUInt64(SystemTime value) => ((ulong)value.High << 32) | value.Low;

    [DllImport("kernel32.dll")]
    private static extern bool GetSystemTimes(out SystemTime idle, out SystemTime kernel, out SystemTime user);

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemTime
    {
        public uint Low;
        public int High;
    }
}
