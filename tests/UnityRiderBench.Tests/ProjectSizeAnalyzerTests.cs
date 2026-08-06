using UnityRiderBench.Models;
using UnityRiderBench.PathAnalysis;

namespace UnityRiderBench.Tests;

public class ProjectSizeAnalyzerTests : IDisposable
{
    private readonly string _tempProjectPath =
        Path.Combine(Path.GetTempPath(), $"urbench-projsize-test-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_tempProjectPath))
        {
            Directory.Delete(_tempProjectPath, recursive: true);
        }
    }

    [Fact]
    public void Analyze_NullProjectPath_ReturnsUnknown()
    {
        var result = ProjectSizeAnalyzer.Analyze(null);

        Assert.Equal(ProjectSizeTier.Unknown, result.Tier);
    }

    [Fact]
    public void Analyze_MissingAssetsFolder_ReturnsUnknown()
    {
        Directory.CreateDirectory(_tempProjectPath);

        var result = ProjectSizeAnalyzer.Analyze(_tempProjectPath);

        Assert.Equal(ProjectSizeTier.Unknown, result.Tier);
    }

    [Fact]
    public void Analyze_FewSmallScripts_ReturnsSmall()
    {
        var assetsPath = Path.Combine(_tempProjectPath, "Assets");
        Directory.CreateDirectory(assetsPath);
        for (var i = 0; i < 5; i++)
        {
            File.WriteAllText(Path.Combine(assetsPath, $"Script{i}.cs"), "// test");
        }

        var result = ProjectSizeAnalyzer.Analyze(_tempProjectPath);

        Assert.Equal(ProjectSizeTier.Small, result.Tier);
        Assert.Equal(5, result.ScriptCount);
    }

    [Fact]
    public void Analyze_ManyScriptsButTinyAssets_ReturnsLargeByScriptAxis()
    {
        var assetsPath = Path.Combine(_tempProjectPath, "Assets", "Scripts");
        Directory.CreateDirectory(assetsPath);
        for (var i = 0; i < 3001; i++)
        {
            File.WriteAllText(Path.Combine(assetsPath, $"Script{i}.cs"), "// test");
        }

        var result = ProjectSizeAnalyzer.Analyze(_tempProjectPath);

        // 에셋 용량은 미미해도(수십 KB) 스크립트 수 축만으로 Large 판정되어야 한다 —
        // 둘 중 더 큰 티어를 채택하는 설계 확인.
        Assert.Equal(ProjectSizeTier.Large, result.Tier);
        Assert.Equal(3001, result.ScriptCount);
    }

    [Fact]
    public void Analyze_NonCsFilesAreCountedInBytesButNotScriptCount()
    {
        var assetsPath = Path.Combine(_tempProjectPath, "Assets");
        Directory.CreateDirectory(assetsPath);
        File.WriteAllText(Path.Combine(assetsPath, "notes.txt"), "not a script");
        File.WriteAllText(Path.Combine(assetsPath, "Script.cs"), "// test");

        var result = ProjectSizeAnalyzer.Analyze(_tempProjectPath);

        Assert.Equal(1, result.ScriptCount);
        Assert.True(result.AssetsBytes > 0);
    }
}
