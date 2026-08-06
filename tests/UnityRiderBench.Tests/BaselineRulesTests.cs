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

    private static BenchmarkReport BuildBenchmark(
        double seqWriteMbPerSec = 1000,
        double seqReadMbPerSec = 1000,
        double randWriteIops = 5000,
        double randReadIops = 100000,
        double ramBandwidthMbPerSec = 8000)
    {
        return new BenchmarkReport(
            new CpuBenchmarkResult(1000, TimeSpan.FromSeconds(1), 8),
            new DiskIoBenchmarkResult("C:\\temp", seqWriteMbPerSec, seqReadMbPerSec, randWriteIops, randReadIops),
            new RamBenchmarkResult(ramBandwidthMbPerSec));
    }

    // 아래 CPU/RAM/디스크랜덤 임계값 테스트는 전부 Medium 티어 기준으로 고정한다 —
    // 티어별 문턱 자체(Small/Large가 실제로 다르게 매겨지는지)는 별도 이론 테스트에서 확인.
    [Theory]
    [InlineData(8, Grade.Good)]
    [InlineData(4, Grade.Warning)]
    [InlineData(2, Grade.Critical)]
    public void EvaluateCpuCores_GradesByThreshold(int logicalCores, Grade expected)
    {
        var spec = BuildSpec(logicalCores, 16L * 1024 * 1024 * 1024, 4L * 1024 * 1024 * 1024);

        var items = BaselineRules.Evaluate(spec, BuildBenchmark(), ProjectSizeTier.Medium);

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

        var items = BaselineRules.Evaluate(spec, BuildBenchmark(), ProjectSizeTier.Medium);

        var ramItem = items.Single(i => i.Category == "RAM" && i.Label == "총 용량");
        Assert.Equal(expected, ramItem.Grade);
    }

    [Fact]
    public void EvaluateGpuVram_UnknownVram_ReturnsWarningWithConfirmationNeeded()
    {
        var spec = BuildSpec(8, 16L * 1024 * 1024 * 1024, -1);

        var items = BaselineRules.Evaluate(spec, BuildBenchmark(), ProjectSizeTier.Medium);

        var gpuItem = items.Single(i => i.Category == "GPU");
        Assert.Equal(Grade.Warning, gpuItem.Grade);
        Assert.Equal("확인 필요", gpuItem.MeasuredValue);
    }

    [Theory]
    [InlineData(1000, Grade.Good)]
    [InlineData(300, Grade.Warning)]
    [InlineData(80, Grade.Critical)]
    public void EvaluateDiskSequential_GradesByWorstOfWriteRead(double mbPerSec, Grade expected)
    {
        var spec = BuildSpec(8, 16L * 1024 * 1024 * 1024, 4L * 1024 * 1024 * 1024);
        // 읽기는 넉넉하게 두고 쓰기 값만 낮춰서, "더 나쁜 쪽" 기준으로 등급이 매겨지는지 확인.
        var benchmark = BuildBenchmark(seqWriteMbPerSec: mbPerSec, seqReadMbPerSec: 5000);

        var items = BaselineRules.Evaluate(spec, benchmark, ProjectSizeTier.Medium);

        var diskItem = items.Single(i => i.Category == "디스크" && i.Label == "순차 처리량");
        Assert.Equal(expected, diskItem.Grade);
    }

    [Theory]
    [InlineData(5000, Grade.Good)]
    [InlineData(1000, Grade.Warning)]
    [InlineData(100, Grade.Critical)]
    public void EvaluateDiskRandom_GradesByWriteIops(double writeIops, Grade expected)
    {
        var spec = BuildSpec(8, 16L * 1024 * 1024 * 1024, 4L * 1024 * 1024 * 1024);
        var benchmark = BuildBenchmark(randWriteIops: writeIops);

        var items = BaselineRules.Evaluate(spec, benchmark, ProjectSizeTier.Medium);

        var diskItem = items.Single(i => i.Category == "디스크" && i.Label == "랜덤 IOPS");
        Assert.Equal(expected, diskItem.Grade);
    }

    [Theory]
    [InlineData(8000, Grade.Good)]
    [InlineData(3000, Grade.Warning)]
    [InlineData(500, Grade.Critical)]
    public void EvaluateRamBandwidth_GradesByThreshold(double bandwidthMbPerSec, Grade expected)
    {
        var spec = BuildSpec(8, 16L * 1024 * 1024 * 1024, 4L * 1024 * 1024 * 1024);
        var benchmark = BuildBenchmark(ramBandwidthMbPerSec: bandwidthMbPerSec);

        var items = BaselineRules.Evaluate(spec, benchmark, ProjectSizeTier.Medium);

        var ramItem = items.Single(i => i.Category == "RAM" && i.Label == "대역폭");
        Assert.Equal(expected, ramItem.Grade);
    }

    // 같은 하드웨어(4코어/8GB RAM/2GB VRAM/1000 IOPS)라도 프로젝트 규모에 따라
    // 등급이 달라져야 한다 — Small 프로젝트에는 충분하지만 Large 프로젝트에는 부족한 스펙.
    [Fact]
    public void Evaluate_SameHardware_GradesStricterAsProjectSizeGrows()
    {
        var spec = BuildSpec(4, 8L * 1024 * 1024 * 1024, 2L * 1024 * 1024 * 1024);
        var benchmark = BuildBenchmark(randWriteIops: 1000);

        var smallItems = BaselineRules.Evaluate(spec, benchmark, ProjectSizeTier.Small);
        var largeItems = BaselineRules.Evaluate(spec, benchmark, ProjectSizeTier.Large);

        Assert.Equal(Grade.Good, smallItems.Single(i => i.Category == "CPU").Grade);
        Assert.Equal(Grade.Critical, largeItems.Single(i => i.Category == "CPU").Grade);

        Assert.Equal(Grade.Good, smallItems.Single(i => i.Category == "RAM" && i.Label == "총 용량").Grade);
        Assert.Equal(Grade.Critical, largeItems.Single(i => i.Category == "RAM" && i.Label == "총 용량").Grade);

        Assert.Equal(Grade.Good, smallItems.Single(i => i.Category == "GPU").Grade);
        Assert.Equal(Grade.Critical, largeItems.Single(i => i.Category == "GPU").Grade);

        Assert.Equal(Grade.Good, smallItems.Single(i => i.Category == "디스크" && i.Label == "랜덤 IOPS").Grade);
        Assert.Equal(Grade.Critical, largeItems.Single(i => i.Category == "디스크" && i.Label == "랜덤 IOPS").Grade);
    }

    [Fact]
    public void Evaluate_UnknownProjectSize_FallsBackToMediumThresholdsWithCaveat()
    {
        var spec = BuildSpec(4, 8L * 1024 * 1024 * 1024, 4L * 1024 * 1024 * 1024);

        var unknownItems = BaselineRules.Evaluate(spec, BuildBenchmark(), ProjectSizeTier.Unknown);
        var mediumItems = BaselineRules.Evaluate(spec, BuildBenchmark(), ProjectSizeTier.Medium);

        var unknownCpu = unknownItems.Single(i => i.Category == "CPU");
        var mediumCpu = mediumItems.Single(i => i.Category == "CPU");
        Assert.Equal(mediumCpu.Grade, unknownCpu.Grade);
        Assert.Contains("확인 필요", unknownCpu.Comment);
    }
}
