namespace UnityRiderBench.Models;

// Small < Medium < Large 순서로 정의되어 있어야 한다 — ProjectSizeAnalyzer가
// 두 판정 축(Assets 용량, 스크립트 수) 중 더 큰 티어를 고를 때 정수 비교(Math.Max)로 처리한다.
public enum ProjectSizeTier
{
    Unknown,
    Small,
    Medium,
    Large,
}
