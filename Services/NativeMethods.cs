using System.Runtime.InteropServices;

namespace IndoTweaks.Services;

/// <summary>
/// Centralized P/Invoke surface. Kept in one file so every unsafe/native
/// call in the app is auditable in a single place.
/// </summary>
internal static class NativeMethods
{
    [StructLayout(LayoutKind.Sequential)]
    public class MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;

        public MEMORYSTATUSEX()
        {
            dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

    // ---- Timer resolution (winmm) ----
    // timeBeginPeriod/timeEndPeriod is the documented, stable way to request a higher
    // system-wide timer resolution (down to 1ms). This is the same mechanism games
    // and multimedia apps use; it is reference-counted by the OS and fully reversible
    // via timeEndPeriod with the same value.
    [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod", SetLastError = true)]
    public static extern uint TimeBeginPeriod(uint uMilliseconds);

    [DllImport("winmm.dll", EntryPoint = "timeEndPeriod", SetLastError = true)]
    public static extern uint TimeEndPeriod(uint uMilliseconds);

    // ---- NtSetTimerResolution (undocumented but widely used, e.g. by CS:GO/many optimizers) ----
    // Gives finer control (down to 0.5ms on modern builds) than winmm. We keep timeBeginPeriod
    // as the safe default and only use this when the user explicitly picks "0.5ms" mode.
    [DllImport("ntdll.dll", SetLastError = true)]
    public static extern int NtSetTimerResolution(uint desiredResolution, bool setResolution, ref uint currentResolution);

    [DllImport("ntdll.dll", SetLastError = true)]
    public static extern int NtQueryTimerResolution(ref uint minimumResolution, ref uint maximumResolution, ref uint currentResolution);
}
