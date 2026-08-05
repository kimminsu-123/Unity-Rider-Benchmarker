# UnityRiderBench

Unity + JetBrains Rider를 함께 사용하는 개발 환경의 하드웨어 스펙, 실측 성능, 프로젝트/캐시 경로의 드라이브 배치를 진단하는 Windows용 CLI 도구입니다.

자세한 설계 배경과 로드맵은 [Plan.md](./Plan.md)를 참고하세요.

## 설치

### PowerShell 원라이너 (권장)

```powershell
irm https://raw.githubusercontent.com/kimminsu-123/Unity-Rider-Benchmarker/main/install/install.ps1 | iex
```

GitHub Releases에서 최신 self-contained 실행 파일을 받아 `%LOCALAPPDATA%\UnityRiderBench`에 설치하고 사용자 PATH에 등록합니다. .NET 런타임 설치가 필요 없습니다.

### 소스에서 직접 빌드

```powershell
git clone https://github.com/kimminsu-123/Unity-Rider-Benchmarker.git
cd Unity-Rider-Benchmarker
dotnet run --project src/UnityRiderBench -- --help
```

.NET 10 SDK가 필요합니다.

## 사용법

```powershell
# 전체 진단 (스펙 + 벤치마크 + 경로 분석 + 기준치 비교)
unityrider-bench scan --project-path "D:\MyGame" --rider-path "C:\Program Files\JetBrains\JetBrains Rider 2026.1.1"

# 정적 스펙 조회만
unityrider-bench spec

# 실측 벤치마크만 (CPU/디스크/RAM 중 선택 가능, 옵션 생략 시 전체 실행)
unityrider-bench bench --cpu --disk

# 리포트를 파일로 저장 (확장자로 형식 결정: .md 또는 .json)
unityrider-bench scan --output report.md

# 도메인 리로드 측정 타임아웃을 60분으로 늘리기 (기본값 30분)
unityrider-bench scan --project-path "D:\MyGame" --unity-timeout 60
```

`--project-path`로 Unity 프로젝트를 지정하면 해당 프로젝트의 Unity 버전과 일치하는 에디터를 찾아 배치 모드(GUI 없음)로 실행해 도메인 리로드 시간을 함께 측정합니다. 대상 프로젝트를 Editor GUI로 한 번도 연 적이 없다면 최초 임포트/인덱싱 때문에 수 분~수십 분까지도 걸릴 수 있으니, 가능하면 Editor로 한 번 열어 인덱싱을 끝낸 뒤 CLI를 실행하는 것을 권장합니다. 기본 타임아웃은 30분이며 `--unity-timeout <분>`으로 조절할 수 있고, 대기 중에는 30초 간격으로 경과 시간이 출력됩니다. 타임아웃이 지나면 자동으로 종료되고 나머지 리포트만 출력됩니다.

## 제약 사항

- Windows 전용입니다(WMI 기반 스펙 수집).
- 벤치마크 수치는 이 머신 내에서의 상대 비교용이며, 공인된 절대 기준이 아닙니다.
- Unity 배치 모드 연동은 라이선스가 이미 활성화된 로컬 개발 머신을 전제로 합니다. CI 서버 등 미활성화 환경에서는 동작이 다를 수 있습니다.
- 도메인 리로드 측정(`--project-path`)은 Unity의 최초 Search 인덱싱이 느린 환경(특히 RAM이 빠듯한 머신)에서 기본 30분 타임아웃 안에도 끝나지 않을 수 있습니다. `--unity-timeout`으로 늘리거나, Editor GUI로 프로젝트를 한 번 열어 인덱싱을 끝낸 뒤 재시도하세요. 타임아웃이 나도 나머지 리포트는 정상 출력되고 도메인 리로드 항목만 생략됩니다.

## 개발

```powershell
dotnet build
dotnet test
```
