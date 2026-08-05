using UnityRiderBench.Models;

namespace UnityRiderBench.SpecCollector;

public static class SystemSpecCollector
{
    public static SystemSpec Collect()
    {
        var cpu = CpuInfoCollector.Collect();
        var ram = RamInfoCollector.Collect();
        var gpu = GpuInfoCollector.Collect();
        var disks = DiskInfoCollector.Collect();
        var os = OsInfoCollector.Collect();

        return new SystemSpec(cpu, ram, gpu, disks, os);
    }
}
