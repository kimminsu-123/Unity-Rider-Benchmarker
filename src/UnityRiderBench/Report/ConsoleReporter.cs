using UnityRiderBench.Models;

namespace UnityRiderBench.Report;

public static class ConsoleReporter
{
    public static void PrintSpec(SystemSpec spec)
    {
        Console.WriteLine("=== CPU ===");
        Console.WriteLine($"  {spec.Cpu.Name}");
        Console.WriteLine($"  물리 코어 {spec.Cpu.PhysicalCores} / 논리 코어 {spec.Cpu.LogicalCores}");
        Console.WriteLine($"  클럭: 현재 {spec.Cpu.CurrentClockMhz:0} MHz / 정격 최대 {spec.Cpu.MaxClockMhz:0} MHz");

        Console.WriteLine();
        Console.WriteLine("=== RAM ===");
        Console.WriteLine($"  총 {ReportFormatting.FormatBytes(spec.Ram.TotalBytes)} / 사용 가능 {ReportFormatting.FormatBytes(spec.Ram.AvailableBytes)}");
        Console.WriteLine($"  속도 {spec.Ram.SpeedMhz:0} MHz / 모듈 {spec.Ram.ChannelCount}개(근사치)");

        Console.WriteLine();
        Console.WriteLine("=== GPU ===");
        var vram = spec.Gpu.VramBytes < 0 ? "확인 필요 (WMI 32비트 제약)" : ReportFormatting.FormatBytes(spec.Gpu.VramBytes);
        Console.WriteLine($"  {spec.Gpu.Name} / VRAM {vram}");

        Console.WriteLine();
        Console.WriteLine("=== 디스크 ===");
        foreach (var disk in spec.Disks)
        {
            Console.WriteLine($"  {disk.DriveLetter} {ReportFormatting.DescribeDriveKind(disk.Kind)}  여유 {ReportFormatting.FormatBytes(disk.FreeBytes)} / 총 {ReportFormatting.FormatBytes(disk.TotalBytes)}");
        }

        Console.WriteLine();
        Console.WriteLine("=== OS / 런타임 ===");
        Console.WriteLine($"  {spec.Os.OsVersion}");
        Console.WriteLine($"  {spec.Os.DotNetVersion}");
        Console.WriteLine($"  JDK: {(spec.Os.HasJdk ? "있음" : "없음")}");
    }

    public static void Print(ScanReport report)
    {
        PrintSpec(report.Spec);

        if (report.Benchmark is { } bench)
        {
            Console.WriteLine();
            Console.WriteLine("=== 벤치마크 ===");
            if (bench.Cpu is { } cpu)
            {
                Console.WriteLine($"  CPU 처리량 {cpu.Score:0.#} MB/s ({cpu.ThreadsUsed}스레드)");
            }

            if (bench.Disk is { } disk)
            {
                Console.WriteLine($"  디스크 순차 쓰기/읽기 {disk.SequentialWriteMbPerSec:0.#} / {disk.SequentialReadMbPerSec:0.#} MB/s");
                Console.WriteLine($"  디스크 랜덤 쓰기/읽기 {disk.RandomWriteIops:0.#} / {disk.RandomReadIops:0.#} IOPS");
            }

            if (bench.Ram is { } ram)
            {
                Console.WriteLine($"  RAM 대역폭(근사치) {ram.BandwidthMbPerSec:0.#} MB/s");
            }
        }

        Console.WriteLine();
        Console.WriteLine("=== 프로젝트 규모 ===");
        if (report.ProjectSize.Tier == ProjectSizeTier.Unknown)
        {
            Console.WriteLine("  확인 불가 (--project-path 미지정 또는 Assets 폴더 없음) — 아래 기준치는 중간 규모로 가정해 계산됨");
        }
        else
        {
            var assetsGb = report.ProjectSize.AssetsBytes / 1024.0 / 1024.0 / 1024.0;
            Console.WriteLine($"  {ReportFormatting.DescribeTier(report.ProjectSize.Tier)} (Assets {assetsGb:0.#}GB, 스크립트 {report.ProjectSize.ScriptCount}개)");
        }

        Console.WriteLine();
        Console.WriteLine("=== 기준치 비교 ===");
        foreach (var item in report.GradedItems)
        {
            Console.WriteLine($"  [{item.Category}] {item.Label}: {item.MeasuredValue} (기준 {item.BaselineValue}) → [{ReportFormatting.DescribeGrade(item.Grade)}] {item.Comment}");
        }

        Console.WriteLine();
        Console.WriteLine("=== 경로 진단 ===");
        foreach (var item in report.PathDiagnosis)
        {
            var existsLabel = item.Exists ? string.Empty : " (경로 없음)";
            Console.WriteLine($"  [{item.Label}] {item.Path}{existsLabel} → [{ReportFormatting.DescribeGrade(item.Grade)}] {item.Comment}");
        }

        if (report.DomainReload is { } reload)
        {
            Console.WriteLine();
            Console.WriteLine("=== Unity 도메인 리로드 ===");
            Console.WriteLine($"  Unity {reload.UnityVersion} — 리로드 {reload.DomainReloadSeconds:0.##}s / 임포트 {reload.AssemblyImportSeconds:0.##}s");
        }
    }
}
