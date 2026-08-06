namespace UnityRiderBench.Models;

public sealed record ScanReport(
    DateTimeOffset GeneratedAt,
    SystemSpec Spec,
    BenchmarkReport? Benchmark,
    IReadOnlyList<PathDiagnosisItem> PathDiagnosis,
    IReadOnlyList<GradedItem> GradedItems,
    DomainReloadResult? DomainReload,
    ProjectSizeInfo ProjectSize
);
