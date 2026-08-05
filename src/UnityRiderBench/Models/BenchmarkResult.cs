namespace UnityRiderBench.Models;

public sealed record CpuBenchmarkResult(
    double Score,
    TimeSpan Elapsed,
    int ThreadsUsed
);

public sealed record DiskIoBenchmarkResult(
    string TargetPath,
    double SequentialWriteMbPerSec,
    double SequentialReadMbPerSec,
    double RandomWriteIops,
    double RandomReadIops
);

public sealed record RamBenchmarkResult(
    double BandwidthMbPerSec
);

public sealed record BenchmarkReport(
    CpuBenchmarkResult? Cpu,
    DiskIoBenchmarkResult? Disk,
    RamBenchmarkResult? Ram
);
