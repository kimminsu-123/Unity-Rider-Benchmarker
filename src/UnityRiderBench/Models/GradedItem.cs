namespace UnityRiderBench.Models;

public sealed record GradedItem(
    string Category,
    string Label,
    string MeasuredValue,
    string BaselineValue,
    Grade Grade,
    string Comment
);
