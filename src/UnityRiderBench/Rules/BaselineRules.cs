using UnityRiderBench.Models;

namespace UnityRiderBench.Rules;

// Unity Editor + JetBrains Rider(ReSharper 인덱싱 포함)를 함께 구동하는 조합 기준의 경험적 임계값.
// 각 제품 공식 시스템 요구사항은 버전마다 달라질 수 있으므로 정기적인 재확인이 필요하다(확인 필요).
public static class BaselineRules
{
    private const int MinLogicalCores = 4;
    private const int RecommendedLogicalCores = 8;

    private const long MinRamBytes = 8L * 1024 * 1024 * 1024;
    private const long RecommendedRamBytes = 16L * 1024 * 1024 * 1024;

    private const long MinVramBytes = 2L * 1024 * 1024 * 1024;
    private const long RecommendedVramBytes = 4L * 1024 * 1024 * 1024;

    public static List<GradedItem> Evaluate(SystemSpec spec)
    {
        return
        [
            EvaluateCpuCores(spec),
            EvaluateRam(spec),
            EvaluateGpuVram(spec),
        ];
    }

    private static GradedItem EvaluateCpuCores(SystemSpec spec)
    {
        var cores = spec.Cpu.LogicalCores;
        var grade = cores >= RecommendedLogicalCores ? Grade.Good
            : cores >= MinLogicalCores ? Grade.Warning
            : Grade.Critical;
        var comment = grade switch
        {
            Grade.Good => "Unity Burst 컴파일/Rider 인덱싱 병렬 처리에 충분",
            Grade.Warning => $"최소 기준은 충족하나 권장({RecommendedLogicalCores}코어)보다 낮음 — 대형 프로젝트에서 컴파일/인덱싱 지연 가능",
            _ => $"최소 기준({MinLogicalCores}코어) 미달 — 체감 성능 저하 예상",
        };

        return new GradedItem("CPU", "논리 코어 수", $"{cores}코어", $"최소 {MinLogicalCores} / 권장 {RecommendedLogicalCores}", grade, comment);
    }

    private static GradedItem EvaluateRam(SystemSpec spec)
    {
        var totalGb = spec.Ram.TotalBytes / 1024.0 / 1024.0 / 1024.0;
        var grade = spec.Ram.TotalBytes >= RecommendedRamBytes ? Grade.Good
            : spec.Ram.TotalBytes >= MinRamBytes ? Grade.Warning
            : Grade.Critical;
        var comment = grade switch
        {
            Grade.Good => "Unity Editor + Rider 동시 구동에 충분",
            Grade.Warning => "동시 구동 시 스와핑/GC 압박 가능 — 대형 프로젝트라면 증설 권장",
            _ => "Unity + Rider 동시 구동 최소 기준 미달",
        };

        return new GradedItem(
            "RAM",
            "총 용량",
            $"{totalGb:0.#}GB",
            $"최소 {MinRamBytes / 1024 / 1024 / 1024}GB / 권장 {RecommendedRamBytes / 1024 / 1024 / 1024}GB",
            grade,
            comment);
    }

    private static GradedItem EvaluateGpuVram(SystemSpec spec)
    {
        var baseline = $"최소 {MinVramBytes / 1024 / 1024 / 1024}GB / 권장 {RecommendedVramBytes / 1024 / 1024 / 1024}GB";

        if (spec.Gpu.VramBytes < 0)
        {
            return new GradedItem("GPU", "VRAM", "확인 필요", baseline, Grade.Warning, "WMI에서 VRAM 조회 실패(32비트 제약) — 제조사 스펙 수동 확인 필요");
        }

        var vramGb = spec.Gpu.VramBytes / 1024.0 / 1024.0 / 1024.0;
        var grade = spec.Gpu.VramBytes >= RecommendedVramBytes ? Grade.Good
            : spec.Gpu.VramBytes >= MinVramBytes ? Grade.Warning
            : Grade.Critical;
        var comment = grade switch
        {
            Grade.Good => "Scene 뷰/URP 프리뷰 구동에 충분",
            Grade.Warning => "최소 기준은 충족하나 고해상도 텍스처/URP 프리뷰 시 부족할 수 있음",
            _ => "Unity Editor Scene 뷰 구동 최소 기준 미달",
        };

        return new GradedItem("GPU", "VRAM", $"{vramGb:0.#}GB", baseline, grade, comment);
    }
}
