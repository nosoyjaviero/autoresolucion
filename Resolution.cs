using System.Runtime.InteropServices;

namespace AutoResolution;

internal sealed record DisplayResolution(int Width, int Height, int BitsPerPixel, int Frequency)
{
    public override string ToString() => $"{Width} x {Height} ({Frequency} Hz)";
}

internal static class DisplayManager
{
    private const int EnumCurrentSettings = -1;
    private const int EnumSuccess = 1;
    private const int ChangeSuccessful = 0;
    private const int CdsFullscreen = 0x00000004;
    private const int DispChangeRestart = 1;
    private const int DispChangeBadMode = -2;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int EnumDisplaySettings(string? deviceName, int modeNum, ref DevMode devMode);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int ChangeDisplaySettingsEx(
        string? deviceName,
        ref DevMode devMode,
        IntPtr hwnd,
        int flags,
        IntPtr lParam);

    public static IReadOnlyList<DisplayResolution> GetResolutions()
    {
        var modes = new Dictionary<(int Width, int Height, int Frequency), DisplayResolution>();
        var modeIndex = 0;
        while (true)
        {
            var mode = CreateDevMode();
            var result = EnumDisplaySettings(null, modeIndex++, ref mode);
            if (result != EnumSuccess)
            {
                break;
            }

            var resolution = new DisplayResolution(mode.dmPelsWidth, mode.dmPelsHeight, mode.dmBitsPerPel, mode.dmDisplayFrequency);
            modes.TryAdd((resolution.Width, resolution.Height, resolution.Frequency), resolution);
        }

        return modes.Values
            .OrderByDescending(mode => mode.Width * mode.Height)
            .ThenByDescending(mode => mode.Frequency)
            .ToList();
    }

    public static DisplayResolution GetCurrentResolution()
    {
        var mode = CreateDevMode();
        if (EnumDisplaySettings(null, EnumCurrentSettings, ref mode) != EnumSuccess)
        {
            throw new InvalidOperationException("Windows no pudo consultar la resolución actual.");
        }

        return new DisplayResolution(mode.dmPelsWidth, mode.dmPelsHeight, mode.dmBitsPerPel, mode.dmDisplayFrequency);
    }

    public static void Apply(DisplayResolution resolution)
    {
        var mode = CreateDevMode();
        mode.dmPelsWidth = resolution.Width;
        mode.dmPelsHeight = resolution.Height;
        mode.dmBitsPerPel = resolution.BitsPerPixel;
        mode.dmDisplayFrequency = resolution.Frequency;
        mode.dmFields = DevModeFields.PelsWidth | DevModeFields.PelsHeight | DevModeFields.BitsPerPel | DevModeFields.DisplayFrequency;

        var result = ChangeDisplaySettingsEx(null, ref mode, IntPtr.Zero, CdsFullscreen, IntPtr.Zero);
        if (result == DispChangeBadMode)
        {
            throw new InvalidOperationException("La resolución seleccionada no es compatible con la pantalla.");
        }

        if (result != ChangeSuccessful && result != DispChangeRestart)
        {
            throw new InvalidOperationException($"Windows rechazó el cambio de resolución (código {result}).");
        }
    }

    private static DevMode CreateDevMode() => new()
    {
        dmDeviceName = new string('\0', 32),
        dmFormName = new string('\0', 32),
        dmSize = (short)Marshal.SizeOf<DevMode>()
    };

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DevMode
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
        public short dmSpecVersion;
        public short dmDriverVersion;
        public short dmSize;
        public short dmDriverExtra;
        public DevModeFields dmFields;
        public int dmPositionX;
        public int dmPositionY;
        public int dmDisplayOrientation;
        public int dmDisplayFixedOutput;
        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
        public short dmLogPixels;
        public int dmBitsPerPel;
        public int dmPelsWidth;
        public int dmPelsHeight;
        public int dmDisplayFlags;
        public int dmDisplayFrequency;
        public int dmICMMethod;
        public int dmICMIntent;
        public int dmMediaType;
        public int dmDitherType;
        public int dmReserved1;
        public int dmReserved2;
        public int dmPanningWidth;
        public int dmPanningHeight;
    }

    [Flags]
    private enum DevModeFields
    {
        PelsWidth = 0x80000,
        PelsHeight = 0x100000,
        BitsPerPel = 0x40000,
        DisplayFrequency = 0x400000
    }
}
