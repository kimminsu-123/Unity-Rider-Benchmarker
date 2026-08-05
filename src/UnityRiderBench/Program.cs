using System.CommandLine;
using UnityRiderBench.Benchmark;
using UnityRiderBench.Models;
using UnityRiderBench.PathAnalysis;
using UnityRiderBench.Report;
using UnityRiderBench.Rules;
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
    if (!OperatingSystem.IsWindows())
    {
        Console.Error.WriteLine("scan 명령은 현재 Windows(WMI)만 지원합니다.");
        return;
    }

    var spec = SystemSpecCollector.Collect();

    Console.WriteLine("CPU/디스크/RAM 벤치마크 실행 중...");
    var benchmark = new BenchmarkReport(
        CpuBenchmark.Run(),
        DiskIoBenchmark.Run(Path.GetTempPath()),
        RamBenchmark.Run());

    var pathDiagnosis = PathDiagnosisBuilder.Build(spec, projectPath, riderPath);
    var gradedItems = BaselineRules.Evaluate(spec);

    var report = new ScanReport(DateTimeOffset.Now, spec, benchmark, pathDiagnosis, gradedItems, DomainReload: null);

    WriteReport(report, output);
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
    ConsoleReporter.PrintSpec(spec);
});

var benchCommand = new Command("bench", "실측 벤치마크만 실행")
{
    cpuOption,
    diskOption,
    ramOption,
};
benchCommand.SetHandler((bool cpu, bool disk, bool ram) =>
{
    if (!cpu && !disk && !ram)
    {
        cpu = disk = ram = true;
    }

    if (cpu)
    {
        Console.WriteLine("CPU 벤치마크 실행 중 (약 2초)...");
        var result = CpuBenchmark.Run();
        Console.WriteLine($"  처리량 {result.Score:0.#} MB/s ({result.ThreadsUsed}스레드, {result.Elapsed.TotalSeconds:0.#}s)");
    }

    if (disk)
    {
        Console.WriteLine("디스크 I/O 벤치마크 실행 중...");
        var result = DiskIoBenchmark.Run(Path.GetTempPath());
        Console.WriteLine($"  순차 쓰기 {result.SequentialWriteMbPerSec:0.#} MB/s / 순차 읽기 {result.SequentialReadMbPerSec:0.#} MB/s");
        Console.WriteLine($"  랜덤 쓰기 {result.RandomWriteIops:0.#} IOPS / 랜덤 읽기 {result.RandomReadIops:0.#} IOPS");
        Console.WriteLine($"  대상 경로 {result.TargetPath}");
    }

    if (ram)
    {
        Console.WriteLine("RAM 대역폭 벤치마크 실행 중...");
        var result = RamBenchmark.Run();
        Console.WriteLine($"  대역폭(근사치) {result.BandwidthMbPerSec:0.#} MB/s");
    }
}, cpuOption, diskOption, ramOption);

var rootCommand = new RootCommand("Unity + Rider 에디터 성능 벤치마크 CLI 도구")
{
    scanCommand,
    specCommand,
    benchCommand,
};

return await rootCommand.InvokeAsync(args);

static void WriteReport(ScanReport report, string? outputPath)
{
    if (string.IsNullOrWhiteSpace(outputPath))
    {
        ConsoleReporter.Print(report);
        return;
    }

    var content = Path.GetExtension(outputPath).Equals(".json", StringComparison.OrdinalIgnoreCase)
        ? JsonReporter.Render(report)
        : MarkdownReporter.Render(report);

    File.WriteAllText(outputPath, content);
    Console.WriteLine($"리포트를 저장했습니다: {outputPath}");
}
