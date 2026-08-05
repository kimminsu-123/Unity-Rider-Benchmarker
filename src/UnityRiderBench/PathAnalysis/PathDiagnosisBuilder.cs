using UnityRiderBench.Models;

namespace UnityRiderBench.PathAnalysis;

public static class PathDiagnosisBuilder
{
    private const long FreeSpaceWarningBytes = 20L * 1024 * 1024 * 1024;

    public static List<PathDiagnosisItem> Build(SystemSpec spec, string? projectPath, string? riderPath)
    {
        var items = new List<PathDiagnosisItem>();

        if (!string.IsNullOrWhiteSpace(projectPath))
        {
            items.Add(BuildDrivePathItem("프로젝트 Library", Path.Combine(projectPath, "Library"), spec.Disks));
        }

        var unityHubRoot = PathResolver.FindUnityHubEditorRoot();
        if (unityHubRoot is not null)
        {
            items.Add(BuildDrivePathItem("Unity Hub/Editor 설치 경로", unityHubRoot, spec.Disks));
        }

        var resolvedRiderPath = !string.IsNullOrWhiteSpace(riderPath) ? riderPath : PathResolver.FindRiderInstallPath();
        if (resolvedRiderPath is not null)
        {
            items.Add(BuildDrivePathItem("Rider 설치 경로", resolvedRiderPath, spec.Disks));
        }

        var riderCache = PathResolver.FindRiderCachePath(projectPath);
        if (riderCache is not null)
        {
            items.Add(BuildDrivePathItem("Rider 캐시 경로", riderCache, spec.Disks));
        }

        items.Add(BuildSystemDriveFreeSpaceItem(spec.Disks));

        return items;
    }

    private static PathDiagnosisItem BuildDrivePathItem(string label, string path, IReadOnlyList<DiskSpec> disks)
    {
        var exists = Directory.Exists(path) || File.Exists(path);
        var kind = DriveMatcher.Match(path, disks);
        var grade = kind switch
        {
            DriveKind.Hdd => Grade.Warning,
            DriveKind.SataSsd or DriveKind.Nvme => Grade.Good,
            _ => Grade.Warning,
        };
        var comment = kind switch
        {
            DriveKind.Hdd => "HDD 감지 — SSD 이전 권장 (Library/캐시 I/O 병목 가능)",
            DriveKind.SataSsd or DriveKind.Nvme => "양호",
            _ => "드라이브 타입 확인 필요",
        };

        return new PathDiagnosisItem(label, path, exists, kind, grade, comment);
    }

    private static PathDiagnosisItem BuildSystemDriveFreeSpaceItem(IReadOnlyList<DiskSpec> disks)
    {
        var systemDrive = disks.FirstOrDefault(d => string.Equals(d.DriveLetter, "C:", StringComparison.OrdinalIgnoreCase));
        if (systemDrive is null)
        {
            return new PathDiagnosisItem("C 드라이브 여유공간", "C:\\", false, DriveKind.Unknown, Grade.Warning, "C 드라이브를 찾을 수 없음");
        }

        var grade = systemDrive.FreeBytes < FreeSpaceWarningBytes ? Grade.Warning : Grade.Good;
        var freeGb = systemDrive.FreeBytes / 1024.0 / 1024.0 / 1024.0;
        var comment = grade == Grade.Warning
            ? $"{freeGb:0.#}GB (경고: 20GB 이상 권장)"
            : $"{freeGb:0.#}GB (양호)";

        return new PathDiagnosisItem("C 드라이브 여유공간", "C:\\", true, systemDrive.Kind, grade, comment);
    }
}
