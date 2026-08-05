using UnityRiderBench.Models;

namespace UnityRiderBench.PathAnalysis;

public static class DriveMatcher
{
    public static DriveKind Match(string path, IReadOnlyList<DiskSpec> disks)
    {
        string? root;
        try
        {
            root = Path.GetPathRoot(Path.GetFullPath(path));
        }
        catch (Exception ex) when (ex is ArgumentException or PathTooLongException or NotSupportedException)
        {
            return DriveKind.Unknown;
        }

        if (string.IsNullOrEmpty(root))
        {
            return DriveKind.Unknown;
        }

        var driveLetter = root.TrimEnd('\\');
        var disk = disks.FirstOrDefault(d => string.Equals(d.DriveLetter, driveLetter, StringComparison.OrdinalIgnoreCase));
        return disk?.Kind ?? DriveKind.Unknown;
    }
}
