using System.Runtime.InteropServices;

namespace IndoTweaks.Services;

/// <summary>
/// Flushes the Windows "Standby" memory list - cached file data the OS is holding
/// onto opportunistically. This is the same mechanism tools like EmptyStandbyList.exe
/// use. It is always safe: standby pages are, by definition, reclaimable and not
/// backing any active allocation - flushing just forces immediate reclaim instead of
/// waiting for memory pressure.
/// </summary>
public static class MemoryFlushService
{
    private const int SystemMemoryListInformation = 0x50;
    private const int MemoryPurgeStandbyList = 4;

    [DllImport("ntdll.dll")]
    private static extern int NtSetSystemInformation(int infoClass, ref int info, int length);

    public static Task FlushStandbyListAsync() => Task.Run(() =>
    {
        int command = MemoryPurgeStandbyList;
        int status = NtSetSystemInformation(SystemMemoryListInformation, ref command, sizeof(int));
        if (status != 0)
            throw new InvalidOperationException($"NtSetSystemInformation failed with status 0x{status:X8}. Ensure IndoTweaks is running as Administrator.");
    });
}
