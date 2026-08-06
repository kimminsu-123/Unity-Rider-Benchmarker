using UnityRiderBench.Models;

namespace UnityRiderBench.PathAnalysis;

// Assets/ 폴더를 실측해 프로젝트 규모 티어를 판정한다. --project-size 같은 수동 오버라이드는
// 없음 — 사용자 주관 대신 항상 실측치로만 판정한다(의도된 설계).
// 두 축을 따로 보는 이유: 용량(대형 텍스처/오디오 등 에셋 볼륨)은 VRAM·디스크 I/O 부담과,
// 스크립트 수는 CPU(컴파일/Burst)·RAM(Rider/ReSharper 인덱싱) 부담과 서로 다른 메커니즘으로
// 연결되어 있어 하나만 보면 왜곡될 수 있다(예: 대용량 비디오 에셋 몇 개뿐인 소규모 코드베이스).
// 둘 중 더 큰 티어를 최종 규모로 채택한다.
public static class ProjectSizeAnalyzer
{
    private const long SmallAssetsBytes = 1L * 1024 * 1024 * 1024;
    private const long MediumAssetsBytes = 10L * 1024 * 1024 * 1024;
    private const int SmallScriptCount = 300;
    private const int MediumScriptCount = 3000;

    // Assets/ 전체를 순회하며 파일 메타데이터만 읽는다 — 대형 프로젝트(수만 파일)에서는
    // 이 스캔 자체가 수 초 걸릴 수 있다(확인 필요: 정확한 체감 시간은 파일시스템/파일 수에 따라 다름).
    public static ProjectSizeInfo Analyze(string? projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            return new ProjectSizeInfo(ProjectSizeTier.Unknown, 0, 0);
        }

        var assetsPath = Path.Combine(projectPath, "Assets");
        if (!Directory.Exists(assetsPath))
        {
            return new ProjectSizeInfo(ProjectSizeTier.Unknown, 0, 0);
        }

        long totalBytes = 0;
        var scriptCount = 0;

        try
        {
            foreach (var file in Directory.EnumerateFiles(assetsPath, "*", SearchOption.AllDirectories))
            {
                var info = new FileInfo(file);
                totalBytes += info.Length;
                if (string.Equals(info.Extension, ".cs", StringComparison.OrdinalIgnoreCase))
                {
                    scriptCount++;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 스캔 도중 접근 불가 파일을 만나면 그때까지 누적된 값으로 판정한다 —
            // 실패시키기보다 근사치라도 내는 쪽이 벤치마크 도구 목적에 맞는다.
        }

        var tier = (ProjectSizeTier)Math.Max((int)ClassifyByBytes(totalBytes), (int)ClassifyByScriptCount(scriptCount));
        return new ProjectSizeInfo(tier, totalBytes, scriptCount);
    }

    private static ProjectSizeTier ClassifyByBytes(long totalBytes) =>
        totalBytes >= MediumAssetsBytes ? ProjectSizeTier.Large
        : totalBytes >= SmallAssetsBytes ? ProjectSizeTier.Medium
        : ProjectSizeTier.Small;

    private static ProjectSizeTier ClassifyByScriptCount(int scriptCount) =>
        scriptCount >= MediumScriptCount ? ProjectSizeTier.Large
        : scriptCount >= SmallScriptCount ? ProjectSizeTier.Medium
        : ProjectSizeTier.Small;
}
