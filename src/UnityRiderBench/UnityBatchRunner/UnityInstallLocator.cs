using System.Text.Json;
using UnityRiderBench.PathAnalysis;

namespace UnityRiderBench.UnityBatchRunner;

public static class UnityInstallLocator
{
    public static string? TryReadProjectEditorVersion(string projectPath)
    {
        var versionFile = Path.Combine(projectPath, "ProjectSettings", "ProjectVersion.txt");
        if (!File.Exists(versionFile))
        {
            return null;
        }

        const string prefix = "m_EditorVersion:";
        foreach (var line in File.ReadLines(versionFile))
        {
            if (line.StartsWith(prefix, StringComparison.Ordinal))
            {
                return line[prefix.Length..].Trim();
            }
        }

        return null;
    }

    // Unity Hub 기본 설치 경로(<HubEditorRoot>\<version>\Editor\Unity.exe) 규칙을 우선 사용.
    // editors.json 스키마는 Hub 버전에 따라 달라질 수 있어(확인 필요) 보조 수단으로만 시도한다.
    public static string? FindUnityExecutable(string editorVersion)
    {
        var hubEditorRoot = PathResolver.FindUnityHubEditorRoot();
        if (hubEditorRoot is not null)
        {
            var candidate = Path.Combine(hubEditorRoot, editorVersion, "Editor", "Unity.exe");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return TryFindFromEditorsJson(editorVersion);
    }

    private static string? TryFindFromEditorsJson(string editorVersion)
    {
        try
        {
            var editorsJsonPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "UnityHub", "editors.json");

            if (!File.Exists(editorsJsonPath))
            {
                return null;
            }

            using var doc = JsonDocument.Parse(File.ReadAllText(editorsJsonPath));
            if (doc.RootElement.TryGetProperty(editorVersion, out var entry) &&
                entry.TryGetProperty("location", out var location))
            {
                var path = location.GetString();
                return path is not null && File.Exists(path) ? path : null;
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
        }

        return null;
    }
}
