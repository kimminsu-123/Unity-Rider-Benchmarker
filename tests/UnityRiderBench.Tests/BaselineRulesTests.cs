using UnityRiderBench.Models;
using UnityRiderBench.Rules;

namespace UnityRiderBench.Tests;

public class BaselineRulesTests
{
    private static SystemSpec BuildSpec(int logicalCores, long ramTotalBytes, long vramBytes)
    {
        var cpu = new CpuSpec("Test CPU", logicalCores / 2, logicalCores, 3000, 3500);
        var ram = new RamSpec(ramTotalBytes, ramTotalBytes / 2, 3200, 2);
        var gpu = new GpuSpec("Test GPU", vramBytes);
        var disks = new List<DiskSpec>();
        var os = new OsSpec("Test OS", ".NET Test", false);
        return new SystemSpec(cpu, ram, gpu, disks, os);
    }

    [Theory]
    [InlineData(8, Grade.Good)]
    [InlineData(4, Grade.Warning)]
    [InlineData(2, Grade.Critical)]
    public void EvaluateCpuCores_GradesByThreshold(int logicalCores, Grade expected)
    {
        var spec = BuildSpec(logicalCores, 16L * 1024 * 1024 * 1024, 4L * 1024 * 1024 * 1024);

        var items = BaselineRules.Evaluate(spec);

        var cpuItem = items.Single(i => i.Category == "CPU");
        Assert.Equal(expected, cpuItem.Grade);
    }

    [Theory]
    [InlineData(16, Grade.Good)]
    [InlineData(8, Grade.Warning)]
    [InlineData(4, Grade.Critical)]
    public void EvaluateRam_GradesByThreshold(int totalGb, Grade expected)
    {
        var spec = BuildSpec(8, totalGb * 1024L * 1024 * 1024, 4L * 1024 * 1024 * 1024);

        var items = BaselineRules.Evaluate(spec);

        var ramItem = items.Single(i => i.Category == "RAM");
        Assert.Equal(expected, ramItem.Grade);
    }

    [Fact]
    public void EvaluateGpuVram_UnknownVram_ReturnsWarningWithConfirmationNeeded()
    {
        var spec = BuildSpec(8, 16L * 1024 * 1024 * 1024, -1);

        var items = BaselineRules.Evaluate(spec);

        var gpuItem = items.Single(i => i.Category == "GPU");
        Assert.Equal(Grade.Warning, gpuItem.Grade);
        Assert.Equal("확인 필요", gpuItem.MeasuredValue);
    }
}
