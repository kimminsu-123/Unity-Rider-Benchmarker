using System.Text.Json;
using System.Text.Json.Serialization;
using UnityRiderBench.Models;

namespace UnityRiderBench.Report;

public static class JsonReporter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Render(ScanReport report) => JsonSerializer.Serialize(report, Options);
}
