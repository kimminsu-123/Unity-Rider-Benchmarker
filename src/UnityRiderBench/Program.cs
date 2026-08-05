using System.CommandLine;
using UnityRiderBench.Benchmark;
using UnityRiderBench.Models;
using UnityRiderBench.PathAnalysis;
using UnityRiderBench.Report;
using UnityRiderBench.Rules;
using UnityRiderBench.SpecCollector;
using UnityRiderBench.UnityBatchRunner;

var projectPathOption = new Option<string?>("--project-path", "Unity 프로젝트 루트 경로");
var riderPathOption = new Option<string?>("--rider-path", "Rider 설치 경로");
var outputOption = new Option<string?>("--output", "리포트 저장 파일 경로 (.md 또는 .json)");
var unityTimeoutOption = new Option<int>("--unity-timeout", () => 30, "Unity 배치 모드 도메인 리로드 측정 타임아웃(분)");

var cpuOption = new Option<bool>("--cpu", "CPU 벤치마크 실행");
var diskOption = new Option<bool>("--disk", "디스크 I/O 벤치마크 실행");
var ramOption = new Option<bool>("--ram", "RAM 대역폭 벤치마크 실행");

var scanCommand = new Command("scan", "전체 진단 실행 (스펙 + 벤치마크 + 경로 분석)")
{
    projectPathOption,
    riderPathOption,
    outputOption,
    unityTimeoutOption,
};
scanCommand.SetHandler((string? projectPath, string? riderPath, string? output, int unityTimeoutMinutes) =>
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

    var domainReload = string.IsNullOrWhiteSpace(projectPath)
        ? null
        : TryRunDomainReloadProbe(projectPath, unityTimeoutMinutes);

    var report = new ScanReport(DateTimeOffset.Now, spec, benchmark, pathDiagnosis, gradedItems, domainReload);

    WriteReport(report, output);
}, projectPathOption, riderPathOption, outputOption, unityTimeoutOption);

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

static DomainReloadResult? TryRunDomainReloadProbe(string projectPath, int unityTimeoutMinutes)
{
    var editorVersion = UnityInstallLocator.TryReadProjectEditorVersion(projectPath);
    if (editorVersion is null)
    {
        Console.Error.WriteLine("Unity 프로젝트 버전을 확인할 수 없어(ProjectSettings/ProjectVersion.txt 없음) 도메인 리로드 측정을 건너뜁니다.");
        return null;
    }

    var unityExePath = UnityInstallLocator.FindUnityExecutable(editorVersion);
    if (unityExePath is null)
    {
        Console.Error.WriteLine($"Unity {editorVersion} 실행 파일을 찾을 수 없어 도메인 리로드 측정을 건너뜁니다.");
        return null;
    }

    var runningPid = UnityInstallLocator.FindRunningUnityProcessId(projectPath);
    if (runningPid is int pid)
    {
        Console.Error.WriteLine($"이 프로젝트를 이미 Unity 프로세스(PID {pid})가 열고 있어 도메인 리로드 측정을 건너뜁니다.");
        Console.Error.WriteLine("Editor GUI에서 직접 연 것이 아니라면 이전 실행이 비정상 종료되며 남은 좀비 프로세스일 수 있습니다 —");
        Console.Error.WriteLine($"  작업 관리자 또는 `taskkill /F /T /PID {pid}`로 종료한 뒤 다시 시도하세요.");
        return null;
    }

    Console.WriteLine($"Unity {editorVersion} 배치 모드로 도메인 리로드 측정 중 (헤드리스, 최대 {unityTimeoutMinutes}분)...");

    using var probe = new ProbeInjector(projectPath);
    probe.Inject();
    var result = BatchProcessRunner.Run(unityExePath, projectPath, TimeSpan.FromMinutes(unityTimeoutMinutes));

    if (result is null)
    {
        Console.Error.WriteLine($"도메인 리로드 측정에 실패했습니다 (배치 프로세스가 {unityTimeoutMinutes}분 안에 끝나지 않았거나 결과 파일이 없습니다. --unity-timeout으로 시간을 늘려보세요).");
    }

    return result;
}

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
