using UnityRiderBench.Models;

namespace UnityRiderBench.Rules;

// Unity Editor + JetBrains Rider(ReSharper 인덱싱 포함)를 함께 구동하는 조합 기준의 임계값.
// Unity/JetBrains 공식 시스템 요구사항은 CPU 코어 수 등 극히 일부만 명시하고(확인됨,
// 2026-08-06 기준 Unity 6.4/Rider 공식 문서 대조), 디스크 속도·RAM 대역폭·프로젝트 규모별
// 권장치는 아예 공개된 적이 없다. 그래서 여기 값들은 벤더 스펙이 아니라 컴퓨터 하드웨어/OS
// 이론(암달의 법칙, 가상 메모리 페이징, 디스크 탐색시간 등)에 근거해 도출한 것이다 —
// 실사용 데이터가 쌓이면 재조정이 필요하다(확인 필요).
public static class BaselineRules
{
    private const long Gb = 1024L * 1024 * 1024;

    // CPU 코어: Unity 도메인 리로드는 RebuildCommonClasses/FinalizeReload 같은 직렬 구간과
    // TypeCache.ScanAssembly·에셋 임포트·Burst/IL2CPP 컴파일 같은 병렬 구간이 섞여 있다
    // (실측 로그에서도 직렬 구간은 코어 수와 무관하게 고정 비용으로 나타남). 병렬 구간이
    // 전체에서 차지하는 비중은 프로젝트가 커질수록(스크립트/에셋이 많을수록) 커지므로
    // 코어 추가의 이득도 프로젝트 규모에 비례한다(암달의 법칙).
    private static (int Min, int Recommended) CpuCoreThresholds(ProjectSizeTier tier) => tier switch
    {
        ProjectSizeTier.Small => (4, 4),
        ProjectSizeTier.Large => (8, 12),
        _ => (4, 8), // Medium 및 Unknown(프로젝트 미지정 시 중간 규모로 가정)
    };

    // RAM: OS 베이스라인(~2-4GB) + Unity 에디터 상주 메모리(에셋 규모에 비례) +
    // Rider/ReSharper 힙(코드베이스 크기에 비례) + 스와핑 방지 여유의 합으로 추정.
    // 물리 RAM을 넘어서면 가상 메모리가 디스크로 페이징되는데, RAM 접근(~100ns)과 디스크
    // 접근(SSD도 ~0.1ms 이상)은 자릿수가 3~4개 차이 나 한 번 스와핑되면 체감이 급격히 떨어진다.
    private static (long Min, long Recommended) RamThresholds(ProjectSizeTier tier) => tier switch
    {
        ProjectSizeTier.Small => (8L * Gb, 8L * Gb),
        ProjectSizeTier.Large => (16L * Gb, 32L * Gb),
        _ => (8L * Gb, 16L * Gb),
    };

    // GPU VRAM: Scene/Game 뷰 프레임버퍼 고정 오버헤드(~0.5-1GB) + 상주 텍스처 총량
    // (에셋 텍스처 볼륨에 비례) + URP 렌더타겟/MSAA/HDR 버퍼로 추정.
    private static (long Min, long Recommended) VramThresholds(ProjectSizeTier tier) => tier switch
    {
        ProjectSizeTier.Small => (2L * Gb, 2L * Gb),
        ProjectSizeTier.Large => (4L * Gb, 8L * Gb),
        _ => (2L * Gb, 4L * Gb),
    };

    // 디스크 랜덤 IOPS: Library 재빌드/에셋 임포트는 파일 개수만큼의 랜덤 I/O다.
    // HDD 평균 탐색시간 5~10ms(≈초당 100~200회)로 파일 수만 개짜리 프로젝트를 재빌드하면
    // 탐색 대기만 분 단위, SSD(수천 IOPS)는 같은 작업이 초 단위로 끝난다 — 프로젝트가
    // 커질수록(파일 수가 많을수록) 낮은 IOPS의 체감 페널티가 커지므로 규모별로 문턱을 올린다.
    private static (double Min, double Recommended) DiskRandomThresholds(ProjectSizeTier tier) => tier switch
    {
        ProjectSizeTier.Small => (300, 1000),
        ProjectSizeTier.Large => (2000, 5000),
        _ => (1000, 3000),
    };

    // 디스크 순차 처리량·RAM 대역폭은 프로젝트 규모에 따라 문턱을 다르게 잡을 이론적 근거가
    // 약해(순차 처리량은 빌드 산출물 복사 같은 제한적 시나리오에서만 의미 있고, RAM 대역폭은
    // Unity/Rider의 포인터 추적 위주 워크로드와 상관관계가 약함) 고정값을 쓴다.
    // 순차 쓰기는 WriteThrough(캐시 우회, 1MB 청크마다 동기 플러시)로 측정해 스펙시트 공칭
    // 속도보다 낮게 나온다 — 실측으로 확인된 WMI 감지 NVMe 드라이브의 WriteThrough 순차
    // 쓰기가 350~450MB/s대였다(2026-08-06, i5-8265U 랩톱). 그 값을 반영해 Good 기준을 잡음.
    private const double MinSequentialMbPerSec = 150;
    private const double RecommendedSequentialMbPerSec = 350;

    // RAM 대역폭은 벤더 스펙이 아니라 이 도구 자체의 단일 스레드 관리 배열 복사 측정치를
    // 기준으로 잡은 상대적 임계값이다 — 절대 기준으로 사용하지 말 것(확인 필요).
    private const double MinRamBandwidthMbPerSec = 2000;
    private const double RecommendedRamBandwidthMbPerSec = 6000;

    public static List<GradedItem> Evaluate(SystemSpec spec, BenchmarkReport benchmark, ProjectSizeTier projectSizeTier)
    {
        var items = new List<GradedItem>
        {
            EvaluateCpuCores(spec, projectSizeTier),
            EvaluateRam(spec, projectSizeTier),
            EvaluateGpuVram(spec, projectSizeTier),
        };

        if (benchmark.Disk is { } disk)
        {
            items.Add(EvaluateDiskSequential(disk));
            items.Add(EvaluateDiskRandom(disk, projectSizeTier));
        }

        if (benchmark.Ram is { } ram)
        {
            items.Add(EvaluateRamBandwidth(ram));
        }

        return items;
    }

    private static string AppendUnknownCaveat(string comment, ProjectSizeTier tier) =>
        tier == ProjectSizeTier.Unknown
            ? comment + " (확인 필요: --project-path 미지정/Assets 폴더 없음으로 프로젝트 규모를 판정하지 못해 중간 규모로 가정한 기준임)"
            : comment;

    private static GradedItem EvaluateCpuCores(SystemSpec spec, ProjectSizeTier tier)
    {
        var (min, recommended) = CpuCoreThresholds(tier);
        var cores = spec.Cpu.LogicalCores;
        var grade = cores >= recommended ? Grade.Good
            : cores >= min ? Grade.Warning
            : Grade.Critical;
        var comment = grade switch
        {
            Grade.Good => "도메인 리로드 병렬 구간(TypeCache 스캔/임포트/Burst)과 Rider 인덱싱 동시 처리에 충분",
            Grade.Warning => $"최소 기준은 충족하나 권장({recommended}코어)보다 낮음 — 도메인 리로드의 직렬 구간은 코어를 늘려도 안 빨라지지만, 병렬 구간(임포트/Burst)에서 이 프로젝트 규모 기준 체감 지연 가능",
            _ => $"최소 기준({min}코어) 미달 — 체감 성능 저하 예상",
        };

        return new GradedItem(
            "CPU",
            "논리 코어 수",
            $"{cores}코어",
            $"최소 {min} / 권장 {recommended}",
            grade,
            AppendUnknownCaveat(comment, tier));
    }

    private static GradedItem EvaluateRam(SystemSpec spec, ProjectSizeTier tier)
    {
        var (minBytes, recommendedBytes) = RamThresholds(tier);
        var totalGb = spec.Ram.TotalBytes / 1024.0 / 1024.0 / 1024.0;
        var grade = spec.Ram.TotalBytes >= recommendedBytes ? Grade.Good
            : spec.Ram.TotalBytes >= minBytes ? Grade.Warning
            : Grade.Critical;
        var comment = grade switch
        {
            Grade.Good => "Unity Editor + Rider 동시 구동에 충분",
            Grade.Warning => "동시 구동 시 스와핑/GC 압박 가능 — 스와핑 발생 시 RAM 대비 디스크 접근이 자릿수로 느려 체감 저하가 큼",
            _ => "Unity + Rider 동시 구동 최소 기준 미달",
        };

        return new GradedItem(
            "RAM",
            "총 용량",
            $"{totalGb:0.#}GB",
            $"최소 {minBytes / Gb}GB / 권장 {recommendedBytes / Gb}GB",
            grade,
            AppendUnknownCaveat(comment, tier));
    }

    private static GradedItem EvaluateGpuVram(SystemSpec spec, ProjectSizeTier tier)
    {
        var (minBytes, recommendedBytes) = VramThresholds(tier);
        var baseline = $"최소 {minBytes / Gb}GB / 권장 {recommendedBytes / Gb}GB";

        if (spec.Gpu.VramBytes < 0)
        {
            return new GradedItem("GPU", "VRAM", "확인 필요", baseline, Grade.Warning, "WMI에서 VRAM 조회 실패(32비트 제약) — 제조사 스펙 수동 확인 필요");
        }

        var vramGb = spec.Gpu.VramBytes / 1024.0 / 1024.0 / 1024.0;
        var grade = spec.Gpu.VramBytes >= recommendedBytes ? Grade.Good
            : spec.Gpu.VramBytes >= minBytes ? Grade.Warning
            : Grade.Critical;
        var comment = grade switch
        {
            Grade.Good => "Scene 뷰/URP 프리뷰 구동에 충분",
            Grade.Warning => "최소 기준은 충족하나 고해상도 텍스처/URP 프리뷰 시 부족할 수 있음",
            _ => "Unity Editor Scene 뷰 구동 최소 기준 미달",
        };

        return new GradedItem("GPU", "VRAM", $"{vramGb:0.#}GB", baseline, grade, AppendUnknownCaveat(comment, tier));
    }

    private static GradedItem EvaluateDiskSequential(DiskIoBenchmarkResult disk)
    {
        var worst = Math.Min(disk.SequentialWriteMbPerSec, disk.SequentialReadMbPerSec);
        var grade = worst >= RecommendedSequentialMbPerSec ? Grade.Good
            : worst >= MinSequentialMbPerSec ? Grade.Warning
            : Grade.Critical;
        var comment = grade switch
        {
            Grade.Good => "SSD(NVMe급) 수준 — Library 재빌드/에셋 임포트 I/O 병목 가능성 낮음",
            Grade.Warning => "SATA SSD 수준 — 대형 프로젝트 Library 재빌드 시 체감 지연 가능, NVMe 전환 고려 (측정치가 낮게 나온 것이 백그라운드 디스크 사용 등 일시적 요인일 수 있으니 한 번 더 실행해 재확인 권장)",
            _ => "HDD 수준 추정 — Unity Library/Rider 캐시 I/O 병목 큼, SSD 교체 고려 전 한 번 더 실행해 재현되는지 확인 권장(백신 실시간 검사 등 일시적 요인 배제)",
        };

        return new GradedItem(
            "디스크",
            "순차 처리량",
            $"쓰기 {disk.SequentialWriteMbPerSec:0.#} / 읽기 {disk.SequentialReadMbPerSec:0.#} MB/s",
            $"최소 {MinSequentialMbPerSec:0}MB/s / 권장 {RecommendedSequentialMbPerSec:0}MB/s",
            grade,
            comment);
    }

    private static GradedItem EvaluateDiskRandom(DiskIoBenchmarkResult disk, ProjectSizeTier tier)
    {
        var (min, recommended) = DiskRandomThresholds(tier);
        var grade = disk.RandomWriteIops >= recommended ? Grade.Good
            : disk.RandomWriteIops >= min ? Grade.Warning
            : Grade.Critical;
        var comment = grade switch
        {
            Grade.Good => "랜덤 쓰기 성능 양호 — Rider 인덱싱/다수 소용량 파일 처리에 유리",
            Grade.Warning => "이 프로젝트 규모 기준 랜덤 쓰기 성능이 다소 부족 — Library 재빌드 시 파일 수만큼 탐색 대기가 누적돼 체감 지연 가능",
            _ => "랜덤 쓰기 성능 낮음(HDD 특성) — Library/캐시처럼 소용량 파일 다수 I/O에서 지연 가능",
        };
        comment += $" (읽기 {disk.RandomReadIops:0.#} IOPS는 직전에 쓴 파일을 재읽어 OS 캐시 영향을 받을 수 있어 참고용)";

        return new GradedItem(
            "디스크",
            "랜덤 IOPS",
            $"쓰기 {disk.RandomWriteIops:0.#} / 읽기 {disk.RandomReadIops:0.#} IOPS",
            $"최소 {min:0} / 권장 {recommended:0} IOPS(쓰기 기준)",
            grade,
            AppendUnknownCaveat(comment, tier));
    }

    private static GradedItem EvaluateRamBandwidth(RamBenchmarkResult ram)
    {
        var grade = ram.BandwidthMbPerSec >= RecommendedRamBandwidthMbPerSec ? Grade.Good
            : ram.BandwidthMbPerSec >= MinRamBandwidthMbPerSec ? Grade.Warning
            : Grade.Critical;
        var comment = grade switch
        {
            Grade.Good => "대역폭 양호 — 큰 씬 로드/대량 직렬화 시 병목 가능성 낮음",
            Grade.Warning => "대역폭 다소 낮음 — 단일 채널 구성이거나 저속 모듈일 수 있음(확인 필요: 경험적 추정 임계값)",
            _ => "대역폭 낮음 — 메모리 채널/속도 구성 점검 권장(확인 필요: 절대 기준 아님, 상대 비교 참고용)",
        };

        return new GradedItem(
            "RAM",
            "대역폭",
            $"{ram.BandwidthMbPerSec:0.#}MB/s",
            $"최소 {MinRamBandwidthMbPerSec:0}MB/s / 권장 {RecommendedRamBandwidthMbPerSec:0}MB/s(경험적 추정치)",
            grade,
            comment);
    }
}
