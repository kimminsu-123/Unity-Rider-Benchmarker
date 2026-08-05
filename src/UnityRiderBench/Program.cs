using System.CommandLine;

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
    Console.WriteLine("spec: Phase 1에서 구현 예정");
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
