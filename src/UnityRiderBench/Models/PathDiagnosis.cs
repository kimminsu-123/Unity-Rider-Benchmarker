namespace UnityRiderBench.Models;

public sealed record PathDiagnosisItem(
    string Label,
    string Path,
    bool Exists,
    DriveKind DriveKind,
    Grade Grade,
    string Comment
);
