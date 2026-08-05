using System.Management;
using UnityRiderBench.Models;

namespace UnityRiderBench.SpecCollector;

public static class DiskInfoCollector
{
    // MSFT_PhysicalDisk.BusType (root\Microsoft\Windows\Storage)
    private const int BusTypeSata = 11;
    private const int BusTypeNvme = 17;

    // MSFT_PhysicalDisk.MediaType
    private const int MediaTypeHdd = 3;
    private const int MediaTypeSsd = 4;

    public static List<DiskSpec> Collect()
    {
        var physicalDiskMedia = TryGetPhysicalDiskMedia();
        var results = new List<DiskSpec>();

        using var logicalDiskSearcher = new ManagementObjectSearcher(
            "SELECT DeviceID, Size, FreeSpace FROM Win32_LogicalDisk WHERE DriveType=3");

        foreach (var logicalDisk in logicalDiskSearcher.Get())
        {
            using (logicalDisk)
            {
                var deviceId = (logicalDisk["DeviceID"] as string) ?? "?:";
                var totalBytes = Convert.ToInt64(logicalDisk["Size"] ?? 0);
                var freeBytes = Convert.ToInt64(logicalDisk["FreeSpace"] ?? 0);

                var physicalIndex = TryGetPhysicalDiskIndex(deviceId);
                var kind = DriveKind.Unknown;
                if (physicalIndex is int idx && physicalDiskMedia.TryGetValue(idx, out var media))
                {
                    kind = ClassifyDriveKind(media.MediaType, media.BusType);
                }

                results.Add(new DiskSpec(deviceId, kind, totalBytes, freeBytes));
            }
        }

        return results;
    }

    private static DriveKind ClassifyDriveKind(int mediaType, int busType)
    {
        if (busType == BusTypeNvme)
        {
            return DriveKind.Nvme;
        }

        if (mediaType == MediaTypeSsd)
        {
            return DriveKind.SataSsd;
        }

        if (mediaType == MediaTypeHdd)
        {
            return DriveKind.Hdd;
        }

        return DriveKind.Unknown;
    }

    private static int? TryGetPhysicalDiskIndex(string logicalDeviceId)
    {
        try
        {
            var escaped = logicalDeviceId.Replace("'", "''");
            using var partitionSearcher = new ManagementObjectSearcher(
                $"ASSOCIATORS OF {{Win32_LogicalDisk.DeviceID='{escaped}'}} WHERE AssocClass = Win32_LogicalDiskToPartition");

            foreach (var partition in partitionSearcher.Get())
            {
                using (partition)
                {
                    var partitionDeviceId = (partition["DeviceID"] as string) ?? string.Empty;
                    var escapedPartitionId = partitionDeviceId.Replace("'", "''");

                    using var diskSearcher = new ManagementObjectSearcher(
                        $"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{escapedPartitionId}'}} WHERE AssocClass = Win32_DiskDriveToDiskPartition");

                    foreach (var disk in diskSearcher.Get())
                    {
                        using (disk)
                        {
                            return Convert.ToInt32(disk["Index"] ?? -1);
                        }
                    }
                }
            }
        }
        catch (ManagementException)
        {
            // 드라이브-파티션 연결 조회 실패 시 Unknown으로 폴백
        }

        return null;
    }

    private static Dictionary<int, (int MediaType, int BusType)> TryGetPhysicalDiskMedia()
    {
        var result = new Dictionary<int, (int MediaType, int BusType)>();

        try
        {
            var scope = new ManagementScope(@"root\Microsoft\Windows\Storage");
            scope.Connect();

            using var searcher = new ManagementObjectSearcher(
                scope,
                new ObjectQuery("SELECT DeviceId, MediaType, BusType FROM MSFT_PhysicalDisk"));

            foreach (var disk in searcher.Get())
            {
                using (disk)
                {
                    if (int.TryParse(disk["DeviceId"] as string, out var deviceId))
                    {
                        var mediaType = Convert.ToInt32(disk["MediaType"] ?? 0);
                        var busType = Convert.ToInt32(disk["BusType"] ?? 0);
                        result[deviceId] = (mediaType, busType);
                    }
                }
            }
        }
        catch (ManagementException)
        {
            // Storage 네임스페이스 미지원(구형 Windows 등) — 빈 결과로 폴백, 전체 드라이브 Unknown 처리
        }

        return result;
    }
}
