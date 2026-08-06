using System.Text;
using UnityRiderBench.Models;

namespace UnityRiderBench.Report;

public static class MarkdownReporter
{
    public static string Render(ScanReport report)
    {
        var sb = new StringBuilder();
        var spec = report.Spec;

        sb.AppendLine("# UnityRiderBench 리포트");
        sb.AppendLine();
        sb.AppendLine($"생성 시각: {report.GeneratedAt:yyyy-MM-dd HH:mm:ss zzz}");
        sb.AppendLine();

        sb.AppendLine("## 스펙");
        sb.AppendLine($"- CPU: {spec.Cpu.Name} (물리 {spec.Cpu.PhysicalCores} / 논리 {spec.Cpu.LogicalCores}코어, 현재 {spec.Cpu.CurrentClockMhz:0}MHz / 최대 {spec.Cpu.MaxClockMhz:0}MHz)");
        sb.AppendLine($"- RAM: 총 {ReportFormatting.FormatBytes(spec.Ram.TotalBytes)} / 사용 가능 {ReportFormatting.FormatBytes(spec.Ram.AvailableBytes)} ({spec.Ram.SpeedMhz:0}MHz)");
        var vram = spec.Gpu.VramBytes < 0 ? "확인 필요 (WMI 32비트 제약)" : ReportFormatting.FormatBytes(spec.Gpu.VramBytes);
        sb.AppendLine($"- GPU: {spec.Gpu.Name} (VRAM {vram})");
        foreach (var disk in spec.Disks)
        {
            sb.AppendLine($"- 디스크 {disk.DriveLetter} {ReportFormatting.DescribeDriveKind(disk.Kind)}: 여유 {ReportFormatting.FormatBytes(disk.FreeBytes)} / 총 {ReportFormatting.FormatBytes(disk.TotalBytes)}");
        }

        sb.AppendLine($"- OS: {spec.Os.OsVersion}");
        sb.AppendLine($"- 런타임: {spec.Os.DotNetVersion}, JDK {(spec.Os.HasJdk ? "있음" : "없음")}");
        sb.AppendLine();

        if (report.Benchmark is { } bench)
        {
            sb.AppendLine("## 벤치마크");
            if (bench.Cpu is { } cpu)
            {
                sb.AppendLine($"- CPU 처리량: {cpu.Score:0.#} MB/s ({cpu.ThreadsUsed}스레드)");
            }

            if (bench.Disk is { } disk)
            {
                sb.AppendLine($"- 디스크 순차 쓰기/읽기: {disk.SequentialWriteMbPerSec:0.#} / {disk.SequentialReadMbPerSec:0.#} MB/s");
                sb.AppendLine($"- 디스크 랜덤 쓰기/읽기: {disk.RandomWriteIops:0.#} / {disk.RandomReadIops:0.#} IOPS");
            }

            if (bench.Ram is { } ram)
            {
                sb.AppendLine($"- RAM 대역폭(근사치): {ram.BandwidthMbPerSec:0.#} MB/s");
            }

            sb.AppendLine();
        }

        sb.AppendLine("## 프로젝트 규모");
        if (report.ProjectSize.Tier == ProjectSizeTier.Unknown)
        {
            sb.AppendLine("확인 불가 (--project-path 미지정 또는 Assets 폴더 없음) — 아래 기준치는 중간 규모로 가정해 계산됨");
        }
        else
        {
            var assetsGb = report.ProjectSize.AssetsBytes / 1024.0 / 1024.0 / 1024.0;
            sb.AppendLine($"{ReportFormatting.DescribeTier(report.ProjectSize.Tier)} (Assets {assetsGb:0.#}GB, 스크립트 {report.ProjectSize.ScriptCount}개)");
        }

        sb.AppendLine();
        sb.AppendLine("## 기준치 비교");
        sb.AppendLine("| 분류 | 항목 | 측정값 | 기준 | 등급 | 코멘트 |");
        sb.AppendLine("|---|---|---|---|---|---|");
        foreach (var item in report.GradedItems)
        {
            sb.AppendLine($"| {item.Category} | {item.Label} | {item.MeasuredValue} | {item.BaselineValue} | {ReportFormatting.DescribeGrade(item.Grade)} | {item.Comment} |");
        }

        sb.AppendLine();
        sb.AppendLine("## 경로 진단");
        sb.AppendLine("| 항목 | 경로 | 등급 | 코멘트 |");
        sb.AppendLine("|---|---|---|---|");
        foreach (var item in report.PathDiagnosis)
        {
            var pathLabel = item.Exists ? item.Path : $"{item.Path} (경로 없음)";
            sb.AppendLine($"| {item.Label} | {pathLabel} | {ReportFormatting.DescribeGrade(item.Grade)} | {item.Comment} |");
        }

        if (report.DomainReload is { } reload)
        {
            sb.AppendLine();
            sb.AppendLine("## Unity 도메인 리로드");
            sb.AppendLine($"- Unity {reload.UnityVersion}: 리로드 {reload.DomainReloadSeconds:0.##}s / 임포트 {reload.AssemblyImportSeconds:0.##}s");
        }

        return sb.ToString();
    }
}
