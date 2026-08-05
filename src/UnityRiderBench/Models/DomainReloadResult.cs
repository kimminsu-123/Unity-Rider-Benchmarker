namespace UnityRiderBench.Models;

public sealed record DomainReloadResult(
    string UnityVersion,
    double DomainReloadSeconds,
    double AssemblyImportSeconds
);
