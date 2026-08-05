using System.Management;
using UnityRiderBench.Models;

namespace UnityRiderBench.SpecCollector;

public static class CpuInfoCollector
{
    public static CpuSpec Collect()
    {
        using var searcher = new ManagementObjectSearcher(
            "SELECT Name, NumberOfCores, NumberOfLogicalProcessors, CurrentClockSpeed, MaxClockSpeed FROM Win32_Processor");

        foreach (var obj in searcher.Get())
        {
            using (obj)
            {
                var name = (obj["Name"] as string)?.Trim() ?? "Unknown CPU";
                var physicalCores = Convert.ToInt32(obj["NumberOfCores"] ?? 0);
                var logicalCores = Convert.ToInt32(obj["NumberOfLogicalProcessors"] ?? 0);
                var currentClock = Convert.ToDouble(obj["CurrentClockSpeed"] ?? 0);
                var maxClock = Convert.ToDouble(obj["MaxClockSpeed"] ?? 0);

                return new CpuSpec(name, physicalCores, logicalCores, currentClock, maxClock);
            }
        }

        return new CpuSpec("Unknown CPU", Environment.ProcessorCount, Environment.ProcessorCount, 0, 0);
    }
}
