namespace UnityRiderBench.Models;

// WMI(Win32_Processor)는 "베이스 클럭"을 별도로 제공하지 않는다.
// CurrentClockMhz: 조회 시점의 실제 클럭(부하/터보 상태에 따라 변동)
// MaxClockMhz: 제조사가 신고한 정격 최대 클럭
public sealed record CpuSpec(
    string Name,
    int PhysicalCores,
    int LogicalCores,
    double CurrentClockMhz,
    double MaxClockMhz
);

public sealed record RamSpec(
    long TotalBytes,
    long AvailableBytes,
    double SpeedMhz,
    int ChannelCount
);

// VramBytes: WMI Win32_VideoController.AdapterRAM은 32비트 값이라
// 4GB 이상 VRAM을 가진 GPU에서 오버플로로 잘못된 값을 반환하는 경우가 있음(Windows 10/11 알려진 이슈).
// 신뢰할 수 없는 값으로 판단되면 -1(Unknown)로 채움.
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
