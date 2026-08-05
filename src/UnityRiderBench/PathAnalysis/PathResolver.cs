using System.Text;
using Microsoft.Win32;

namespace UnityRiderBench.PathAnalysis;

public static class PathResolver
{
    // Unity는 최근 프로젝트 경로를 HKCU\SOFTWARE\Unity Technologies\Unity Editor 5.x 아래
    // RecentlyUsedProjectPaths-N 값(REG_BINARY, null 종료 없는 UTF-8)에 저장한다.
    // 이 레지스트리 구조는 Unity 버전에 따라 달라질 수 있어 값이 없으면 조용히 빈 목록을 반환한다(확인 필요).
    public static IReadOnlyList<string> FindRecentUnityProjectPaths()
    {
        var results = new List<string>();
        if (!OperatingSystem.IsWindows())
        {
            return results;
        }

        try
        {
            using var unityRoot = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Unity Technologies");
            if (unityRoot is null)
            {
                return results;
            }

            foreach (var subKeyName in unityRoot.GetSubKeyNames().Where(n => n.StartsWith("Unity Editor", StringComparison.OrdinalIgnoreCase)))
            {
                using var editorKey = unityRoot.OpenSubKey(subKeyName);
                if (editorKey is null)
                {
                    continue;
                }

                foreach (var valueName in editorKey.GetValueNames().Where(n => n.StartsWith("RecentlyUsedProjectPaths-", StringComparison.OrdinalIgnoreCase)))
                {
                    if (editorKey.GetValue(valueName) is byte[] bytes)
                    {
                        var path = Encoding.UTF8.GetString(bytes).TrimEnd('\0');
                        if (!string.IsNullOrWhiteSpace(path))
                        {
                            results.Add(path);
                        }
                    }
                }
            }
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException)
        {
        }

        return results;
    }

    public static string? FindUnityHubEditorRoot()
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var candidate = Path.Combine(programFiles, "Unity", "Hub", "Editor");
        return Directory.Exists(candidate) ? candidate : null;
    }

    public static string? FindRiderInstallPath()
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var jetbrainsRoot = Path.Combine(programFiles, "JetBrains");
        if (Directory.Exists(jetbrainsRoot))
        {
            var riderDir = Directory.GetDirectories(jetbrainsRoot, "JetBrains Rider*")
                .OrderDescending(StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            if (riderDir is not null)
            {
                return riderDir;
            }
        }

        var toolboxRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "JetBrains", "Toolbox", "apps", "Rider");
        return Directory.Exists(toolboxRoot) ? toolboxRoot : null;
    }

    public static string? FindRiderCachePath(string? projectPath)
    {
        if (!string.IsNullOrWhiteSpace(projectPath))
        {
            var ideaPath = Path.Combine(projectPath, ".idea");
            if (Directory.Exists(ideaPath))
            {
                return ideaPath;
            }
        }

        var globalCache = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JetBrains");
        return Directory.Exists(globalCache) ? globalCache : null;
    }
}
