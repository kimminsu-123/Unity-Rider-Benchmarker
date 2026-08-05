using System.Management;
using UnityRiderBench.Models;

namespace UnityRiderBench.SpecCollector;

public static class GpuInfoCollector
{
    public static GpuSpec Collect()
    {
        using var searcher = new ManagementObjectSearcher(
            "SELECT Name, AdapterRAM FROM Win32_VideoController");

        foreach (var obj in searcher.Get())
        {
            using (obj)
            {
                var name = (obj["Name"] as string)?.Trim() ?? "Unknown GPU";
                var rawAdapterRam = obj["AdapterRAM"];

                long vramBytes = -1;
                if (rawAdapterRam is not null)
                {
                    var value = Convert.ToUInt32(rawAdapterRam);
                    // 0 또는 uint.MaxValue는 32비트 오버플로/미보고로 판단 → Unknown 처리
                    if (value != 0 && value != uint.MaxValue)
                    {
                        vramBytes = value;
                    }
                }

                return new GpuSpec(name, vramBytes);
            }
        }

        return new GpuSpec("Unknown GPU", -1);
    }
}
