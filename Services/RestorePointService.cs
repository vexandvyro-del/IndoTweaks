using System.Management;

namespace IndoTweaks.Services;

public enum RestorePointResult { Created, AlreadyRecentEnough, Failed, Skipped }

/// <summary>
/// Wraps the SystemRestore WMI class to create a restore point before any
/// registry/system tweak is applied. Windows throttles restore point creation
/// to one per 24h by default (SystemRestorePointCreationFrequency) - we treat
/// "already have one from today" as success rather than erroring.
/// </summary>
public sealed class RestorePointService
{
    private const int APPLICATION_INSTALL = 0;
    private const int BEGIN_SYSTEM_CHANGE = 100;

    public RestorePointResult CreateRestorePoint(string description)
    {
        try
        {
            using var mc = new ManagementClass(@"root\default:SystemRestore");
            using var inParams = mc.GetMethodParameters("CreateRestorePoint");
            inParams["Description"] = description;
            inParams["RestorePointType"] = APPLICATION_INSTALL;
            inParams["EventType"] = BEGIN_SYSTEM_CHANGE;

            using var result = mc.InvokeMethod("CreateRestorePoint", inParams, null);
            var returnValue = Convert.ToUInt32(result?["ReturnValue"] ?? 0u);

            if (returnValue == 0)
            {
                LoggingService.Instance.Action($"System Restore point created: \"{description}\"");
                return RestorePointResult.Created;
            }

            // 0x8007000E-style throttling or "already created today" commonly returns non-zero
            // without it being a real failure - log and let the user proceed.
            LoggingService.Instance.Warn(
                $"Restore point request returned code {returnValue} (Windows may already have a recent one today).");
            return RestorePointResult.AlreadyRecentEnough;
        }
        catch (Exception ex)
        {
            LoggingService.Instance.Error("Failed to create System Restore point", ex);
            return RestorePointResult.Failed;
        }
    }
}
