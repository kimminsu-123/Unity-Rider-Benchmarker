using UnityRiderBench.Models;

namespace UnityRiderBench.Report;

internal static class ReportFormatting
{
    public static string DescribeGrade(Grade grade) => grade switch
    {
        Grade.Good => "양호",
        Grade.Warning => "주의",
        Grade.Critical => "경고",
        _ => "?",
    };

    public static string DescribeDriveKind(DriveKind kind) => kind switch
    {
        DriveKind.Nvme => "NVMe SSD",
        DriveKind.SataSsd => "SATA SSD",
        DriveKind.Hdd => "HDD",
        _ => "알 수 없음",
    };

    public static string DescribeTier(ProjectSizeTier tier) => tier switch
    {
        ProjectSizeTier.Small => "소규모",
        ProjectSizeTier.Medium => "중간 규모",
        ProjectSizeTier.Large => "대규모",
        _ => "확인 불가",
    };

    public static string FormatBytes(long bytes)
    {
        if (bytes < 0)
        {
            return "확인 필요";
        }

        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        var unitIndex = 0;
        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return $"{value:0.##} {units[unitIndex]}";
    }
}
