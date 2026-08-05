using System.CommandLine;
using UnityRiderBench.Models;
using UnityRiderBench.SpecCollector;

var projectPathOption = new Option<string?>("--project-path", "Unity 프로젝트 루트 경로");
var riderPathOption = new Option<string?>("--rider-path", "Rider 설치 경로");
var outputOption = new Option<string?>("--output", "리포트 저장 파일 경로 (.md 또는 .json)");

var cpuOption = new Option<bool>("--cpu", "CPU 벤치마크 실행");
var diskOption = new Option<bool>("--disk", "디스크 I/O 벤치마크 실행");
var ramOption = new Option<bool>("--ram", "RAM 대역폭 벤치마크 실행");

var scanCommand = new Command("scan", "전체 진단 실행 (스펙 + 벤치마크 + 경로 분석)")
{
    projectPathOption,
    riderPathOption,
    outputOption,
};
scanCommand.SetHandler((string? projectPath, string? riderPath, string? output) =>
{
    Console.WriteLine("scan: Phase 1~4 구현 예정");
}, projectPathOption, riderPathOption, outputOption);

var specCommand = new Command("spec", "정적 스펙 조회만 실행");
specCommand.SetHandler(() =>
{
    if (!OperatingSystem.IsWindows())
    {
        Console.Error.WriteLine("spec 명령은 현재 Windows(WMI)만 지원합니다.");
        return;
    }

    var spec = SystemSpecCollector.Collect();
    PrintSpec(spec);
});

var benchCommand = new Command("bench", "실측 벤치마크만 실행")
{
    cpuOption,
    diskOption,
    ramOption,
};
benchCommand.SetHandler((bool cpu, bool disk, bool ram) =>
{
    Console.WriteLine("bench: Phase 2에서 구현 예정");
}, cpuOption, diskOption, ramOption);

var rootCommand = new RootCommand("Unity + Rider 에디터 성능 벤치마크 CLI 도구")
{
    scanCommand,
    specCommand,
    benchCommand,
};

return await rootCommand.InvokeAsync(args);

static void PrintSpec(SystemSpec spec)
{
    Console.WriteLine("=== CPU ===");
    Console.WriteLine($"  {spec.Cpu.Name}");
    Console.WriteLine($"  물리 코어 {spec.Cpu.PhysicalCores} / 논리 코어 {spec.Cpu.LogicalCores}");
    Console.WriteLine($"  클럭: 현재 {spec.Cpu.CurrentClockMhz:0} MHz / 정격 최대 {spec.Cpu.MaxClockMhz:0} MHz");

    Console.WriteLine();
    Console.WriteLine("=== RAM ===");
    Console.WriteLine($"  총 {FormatBytes(spec.Ram.TotalBytes)} / 사용 가능 {FormatBytes(spec.Ram.AvailableBytes)}");
    Console.WriteLine($"  속도 {spec.Ram.SpeedMhz:0} MHz / 모듈 {spec.Ram.ChannelCount}개(근사치)");

    Console.WriteLine();
    Console.WriteLine("=== GPU ===");
    var vram = spec.Gpu.VramBytes < 0 ? "확인 필요 (WMI 32비트 제약)" : FormatBytes(spec.Gpu.VramBytes);
    Console.WriteLine($"  {spec.Gpu.Name} / VRAM {vram}");

    Console.WriteLine();
    Console.WriteLine("=== 디스크 ===");
    foreach (var disk in spec.Disks)
    {
        Console.WriteLine($"  {disk.DriveLetter} {DescribeDriveKind(disk.Kind)}  여유 {FormatBytes(disk.FreeBytes)} / 총 {FormatBytes(disk.TotalBytes)}");
    }

    Console.WriteLine();
    Console.WriteLine("=== OS / 런타임 ===");
    Console.WriteLine($"  {spec.Os.OsVersion}");
    Console.WriteLine($"  {spec.Os.DotNetVersion}");
    Console.WriteLine($"  JDK: {(spec.Os.HasJdk ? "있음" : "없음")}");
}

static string DescribeDriveKind(DriveKind kind) => kind switch
{
    DriveKind.Nvme => "NVMe SSD",
    DriveKind.SataSsd => "SATA SSD",
    DriveKind.Hdd => "HDD",
    _ => "알 수 없음",
};

static string FormatBytes(long bytes)
{
    if (bytes < 0)
    {
        return "확인 필요";
    }

    string[] units = ["B", "KB", "MB", "GB", "TB"];
    double value = bytes;
    var unitIndex = 0;
    while (value >= 1024 && unitIndex < units.Length - 1)
    {
        value /= 1024;
        unitIndex++;
    }

    return $"{value:0.##} {units[unitIndex]}";
}
