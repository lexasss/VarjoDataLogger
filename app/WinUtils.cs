using System.Runtime.InteropServices;

namespace VarjoDataLogger;

internal static class WinUtils
{
    public static void HideConsoleWindow() => 
        ShowWindow(GetConsoleWindow(), SW_MINIMIZE);

    public static void ShowConsoleWindow() =>
        ShowWindow(GetConsoleWindow(), SW_SHOWNORMAL);

    // Internal

    private const int SW_SHOWNORMAL = 1;
    private const int SW_MINIMIZE = 6;

    [DllImport("User32.dll", CallingConvention = CallingConvention.StdCall, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow([In] IntPtr hWnd, [In] int nCmdShow);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();
}
