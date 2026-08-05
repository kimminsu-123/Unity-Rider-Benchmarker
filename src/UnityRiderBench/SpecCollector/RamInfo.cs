using System.Management;
using UnityRiderBench.Models;

namespace UnityRiderBench.SpecCollector;

public static class RamInfoCollector
{
    public static RamSpec Collect()
    {
        long totalBytes = 0;
        long availableBytes = 0;

        using (var osSearcher = new ManagementObjectSearcher(
            "SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem"))
        {
            foreach (var obj in osSearcher.Get())
            {
                using (obj)
                {
                    // TotalVisibleMemorySize/FreePhysicalMemory 단위는 KB
                    totalBytes = Convert.ToInt64(obj["TotalVisibleMemorySize"] ?? 0) * 1024;
                    availableBytes = Convert.ToInt64(obj["FreePhysicalMemory"] ?? 0) * 1024;
                }
            }
        }

        double speedMhz = 0;
        var moduleCount = 0;

        using (var memSearcher = new ManagementObjectSearcher("SELECT Speed FROM Win32_PhysicalMemory"))
        {
            foreach (var obj in memSearcher.Get())
            {
                using (obj)
                {
                    if (moduleCount == 0)
                    {
                        speedMhz = Convert.ToDouble(obj["Speed"] ?? 0);
                    }

                    moduleCount++;
                }
            }
        }

        // ChannelCount: WMI는 메인보드 채널 배선을 직접 노출하지 않음.
        // 장착된 물리 메모리 모듈 개수로 근사(대칭 구성 가정) — 실제 채널 수와 다를 수 있음.
        return new RamSpec(totalBytes, availableBytes, speedMhz, moduleCount);
    }
}
