namespace UnityRiderBench.Models;

public sealed record CpuSpec(
    string Name,
    int PhysicalCores,
    int LogicalCores,
    double BaseClockMhz,
    double MaxClockMhz
);

public sealed record RamSpec(
    long TotalBytes,
    long AvailableBytes,
    double SpeedMhz,
    int ChannelCount
);

public sealed record GpuSpec(
    string Name,
    long VramBytes
);

public sealed record DiskSpec(
    string DriveLetter,
    DriveKind Kind,
    long TotalBytes,
    long FreeBytes
);

public sealed record OsSpec(
    string OsVersion,
    string DotNetVersion,
    bool HasJdk
);

public sealed record SystemSpec(
    CpuSpec Cpu,
    RamSpec Ram,
    GpuSpec Gpu,
    IReadOnlyList<DiskSpec> Disks,
    OsSpec Os
);
