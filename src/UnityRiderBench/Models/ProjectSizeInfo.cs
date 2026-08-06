namespace UnityRiderBench.Models;

public sealed record ProjectSizeInfo(
    ProjectSizeTier Tier,
    long AssetsBytes,
    int ScriptCount
);
