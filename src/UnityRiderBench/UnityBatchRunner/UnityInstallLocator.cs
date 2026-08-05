using System.Management;
using System.Text.Json;
using UnityRiderBench.PathAnalysis;

namespace UnityRiderBench.UnityBatchRunner;

public static class UnityInstallLocator
{
    // 같은 프로젝트를 이미 열고 있는 Unity 프로세스가 있으면 배치 모드 실행이 프로젝트 락을
    // 얻지 못해 곧바로("Exiting without the bug reporter") 실패한다. 특히 이전 실행이
    // Ctrl+C 등으로 비정상 종료돼 자식 프로세스가 고아로 남은 경우 재현되는 문제라
    // 시작 전에 미리 감지해 원인을 명확히 알려준다.
    public static int? FindRunningUnityProcessId(string projectPath)
    {
        try
        {
            var normalizedPath = Path.GetFullPath(projectPath).TrimEnd('\\');
            using var searcher = new ManagementObjectSearcher(
                "SELECT ProcessId, CommandLine FROM Win32_Process WHERE Name = 'Unity.exe'");

            foreach (var obj in searcher.Get())
            {
                using (obj)
                {
                    var commandLine = obj["CommandLine"] as string;
                    if (commandLine is not null &&
                        commandLine.Contains("-projectPath", StringComparison.OrdinalIgnoreCase) &&
                        commandLine.Contains(normalizedPath, StringComparison.OrdinalIgnoreCase))
                    {
                        return Convert.ToInt32(obj["ProcessId"]);
                    }
                }
            }
        }
        catch (ManagementException)
        {
        }

        return null;
    }

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
