using System;
using System.IO;

namespace Synthea.Cli;

internal interface IDiskSpaceProbe
{
    long? GetFreeBytes(string path);
}

// Real probe: matches the directory's drive root against DriveInfo.GetDrives.
// Returns null if no matching drive is found (e.g. UNC path on Linux) so the
// doctor check can downgrade to Warn rather than guess at a number.
internal sealed class DefaultDiskSpaceProbe : IDiskSpaceProbe
{
    public long? GetFreeBytes(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(path));
            if (string.IsNullOrWhiteSpace(root)) return null;
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (string.Equals(drive.Name, root, StringComparison.OrdinalIgnoreCase) && drive.IsReady)
                    return drive.AvailableFreeSpace;
            }
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}
