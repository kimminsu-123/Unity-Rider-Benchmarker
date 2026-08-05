# Unity + Rider 에디터 성능 벤치마크 CLI 도구 — 개발 계획

## 1. 목표

Unity 게임엔진과 JetBrains Rider를 함께 사용하는 개발 환경에서:

1. 현재 컴퓨터 사양이 두 프로그램을 원활히 구동하기에 충분한지 진단
2. 부족한 부분이 무엇이고 어느 정도 부족한지 정량적으로 제시
3. Unity 프로젝트 / Rider 캐시 / 설치 경로의 드라이브(SSD/HDD/NVMe) 배치 문제까지 함께 진단

를 CLI 환경에서 실행 가능한 프로그램으로 만든다.

---

## 2. 대상 환경 및 스택

- **대상 OS**: Windows 우선 (WMI 활용도가 높아 진단 정확도가 좋음). 추후 macOS 확장 여지 남겨둠.
- **언어/런타임**: C# (.NET 10) — CLI 앱
  - 이유: `System.Management`(WMI) 접근이 자연스럽고, Unity/Rider 사용자 대상 도구로서 진입장벽이 낮음
  - 참고: 최초 계획은 .NET 8이었으나 개발 머신에 .NET 10 SDK만 설치되어 있어 net10.0으로 타겟 조정. 배포는 self-contained 게시이므로 최종 사용자 머신의 런타임 설치 여부와 무관함.
  - 대안: Python(`psutil`, `py-cpuinfo`, `wmi`)으로 빠른 프로토타입 후 포팅도 가능 — 초기 속도 우선이면 이 경로 채택
- **CLI 프레임워크**: `System.CommandLine` (또는 Python이면 `argparse`/`typer`)
- **출력 형식**: 콘솔 리포트(기본) + `--json`, `--markdown` 리포트 파일 저장 옵션

---

## 3. 기능 범위 (Feature Scope)

### 3.1 정적 스펙 조회
- CPU: 모델명, 코어/스레드 수, 베이스/부스트 클럭
- RAM: 총 용량, 사용 가능 용량, 속도(MHz), 채널 수(가능한 경우)
- GPU: 모델명, VRAM 용량
- 디스크: 연결된 모든 드라이브의 타입(HDD/SATA SSD/NVMe), 여유 공간
- OS/런타임: OS 버전, .NET 버전, (Android 빌드 대상 시) JDK 유무

### 3.2 실측 벤치마크
- **CPU 벤치마크**: 멀티스레드 연산(해시 반복 또는 행렬 곱)으로 처리량 측정 → Unity 스크립트 컴파일/Burst 컴파일 체감 속도 추정
- **디스크 I/O 벤치마크**: 대상 경로에 소용량 파일 다수 쓰기/읽기 → 순차 및 랜덤 IOPS 측정 (Library 캐시 갱신 시나리오 근사)
- **RAM 벤치마크**: 대역폭 측정 (선택 항목, 우선순위 낮음)
- **(확장) Unity 프로젝트 연동 벤치마크**: Unity Editor를 배치 모드(`-batchmode -nographics -executeMethod`)로 무인(headless) 실행해 프로젝트 오픈 시 도메인 리로드/임포트 시간을 측정. GUI 창 없이 CLI 명령 한 번으로 완결되며, 측정용 Editor 스크립트는 CLI가 실행 시점에 대상 프로젝트의 `Assets/Editor/`에 임시 복사했다가 종료 후 자동 삭제한다.
  구현 중 `-quit` 플래그는 사용하지 않기로 확정했다 — `RequestScriptReload()`에 의한 도메인 리로드는 비동기로 일어나므로, `AssemblyReloadEvents.afterAssemblyReload` 콜백이 실제로 완료된 뒤 프로브 스크립트가 스스로 `EditorApplication.Exit()`를 호출해야 리로드 소요 시간을 정확히 잴 수 있다. C# CLI 쪽은 안전장치로 타임아웃(5분) 후 프로세스 트리를 강제 종료한다.
  > ⚠️ 실측 확인 결과(2026-08-05, 개발 머신 RAM 8GB/여유 1GB대, Unity 6000.3.6f1): 새로 만든 프로젝트에서 배치 모드 최초 실행 시 Unity 자체의 Search 인덱싱 단계("Start Indexing on Editor startup")에서 CPU를 계속 소모하며 10분 넘게 진행되지 않는 현상을 독립적으로 2회 재현했다. 프로브 스크립트나 도메인 리로드 로직 자체의 결함인지, 이 머신의 RAM 부족(스와핑)이 원인인지, Unity 6 Search 인덱싱의 배치 모드 이슈인지는 이번 세션에서 확정하지 못했다 — 더 사양이 좋은 머신 또는 이미 한 번 Editor GUI로 연 적 있는(인덱스가 준비된) 프로젝트로 재검증 필요. 첫 실행 예상 소요 시간을 5분보다 넉넉히 잡거나, 사전에 Editor GUI로 프로젝트를 한 번 열어 인덱싱을 끝내둔 뒤 CLI를 실행하는 것을 권장한다.

### 3.3 경로 기반 드라이브 진단
- 사용자가 지정하거나 자동 감지한 다음 경로들의 드라이브 타입 판별:
  - Unity 프로젝트 루트 (특히 `Library/`, `Temp/`)
  - Unity Hub / Editor 설치 경로
  - Rider 설치 경로
  - Rider 캐시 경로 (`.idea`, ReSharper 캐시)
  - OS 드라이브(C:) 여유 공간
- 자동 감지 소스: Windows 레지스트리(`RecentlyUsedProjectPaths` 등), 실행 중인 프로세스 경로 스캔, 사용자 수동 입력 옵션 병행

### 3.4 기준치 비교 및 등급화
- Unity 공식 최소/권장 사양, Rider 권장 사양을 내장 기준표로 보유
- 실측값 대비 퍼센트 또는 등급(양호/주의/경고)으로 산출
- 항목별 "부족한 정도" + "개선 시 기대 효과" 코멘트 자동 생성

### 3.5 리포트 출력
- 콘솔 요약 리포트 (등급별 색상 표시)
- 상세 리포트 파일 저장 (`--output report.md` 또는 `.json`)
- 경로별 경고 예시:
  ```
  [프로젝트 경로] D:\MyGame\Library  →  HDD 감지 (경고: SSD 이전 권장)
  [Rider 캐시]     C:\Users\...\.idea  →  NVMe SSD (양호)
  [C 드라이브 여유공간] 12GB (경고: 20GB 이상 권장)
  ```

---

## 4. 아키텍처 개요

```
UnityRiderBench/
├── src/
│   ├── Program.cs                # CLI 진입점, 명령어 파싱
│   ├── SpecCollector/
│   │   ├── CpuInfo.cs
│   │   ├── RamInfo.cs
│   │   ├── GpuInfo.cs
│   │   └── DiskInfo.cs           # WMI 기반 드라이브 타입 판별
│   ├── Benchmark/
│   │   ├── CpuBenchmark.cs
│   │   ├── DiskIoBenchmark.cs
│   │   └── RamBenchmark.cs
│   ├── PathAnalysis/
│   │   ├── PathResolver.cs       # 프로젝트/Rider 경로 자동 감지
│   │   └── DriveMatcher.cs       # 경로 → 드라이브 매핑
│   ├── Rules/
│   │   └── BaselineRules.cs      # Unity/Rider 권장 사양 기준표
│   ├── Report/
│   │   ├── ConsoleReporter.cs
│   │   ├── MarkdownReporter.cs
│   │   └── JsonReporter.cs
│   ├── UnityBatchRunner/
│   │   ├── UnityInstallLocator.cs   # Unity Hub editors.json/레지스트리에서 프로젝트 버전과 일치하는 Editor 실행파일 탐색
│   │   ├── ProbeInjector.cs         # ProbeScript~ 템플릿을 Assets/Editor에 임시 복사, 종료 후(예외 포함) 삭제 보장
│   │   └── BatchProcessRunner.cs    # -batchmode -quit -executeMethod 프로세스 실행 및 결과 JSON 대기/파싱
│   ├── ProbeScript~/
│   │   └── DomainReloadProbe.cs     # 대상 프로젝트에 주입되는 UnityEditor 스크립트 원본 (CLI 자체 빌드에는 미포함, 템플릿으로만 보관)
│   └── Models/                   # 데이터 모델 (SpecResult, BenchResult 등)
├── tests/
├── .github/
│   └── workflows/
│       └── release.yml           # 태그 푸시 시 self-contained 빌드 + GitHub Release 첨부
├── install/
│   └── install.ps1               # 최신 Release 다운로드 → 로컬 배치 → PATH 등록
└── Plan.md
```

---

## 5. 개발 단계 (Milestones)

### Phase 0 — 프로젝트 셋업
- [ ] .NET CLI 프로젝트 생성, `System.CommandLine` 도입
- [ ] 데이터 모델(Spec, Benchmark, PathAnalysis 결과 클래스) 정의

### Phase 1 — 정적 스펙 조회
- [ ] WMI 기반 CPU/RAM/GPU 정보 수집
- [ ] 드라이브 목록 및 타입(HDD/SATA SSD/NVMe) 판별
- [ ] 콘솔 출력으로 1차 확인

### Phase 2 — 실측 벤치마크
- [ ] CPU 벤치마크 구현 및 스코어 산출
- [ ] 디스크 I/O 벤치마크(임시 파일 쓰기/읽기) 구현
- [ ] (선택) RAM 대역폭 벤치마크

### Phase 3 — 경로 기반 드라이브 진단
- [ ] Unity/Rider 경로 자동 감지 로직 (레지스트리, 프로세스 스캔)
- [ ] 수동 경로 입력 옵션 (`--project-path`, `--rider-path` 등)
- [ ] 경로 → 드라이브 매핑 및 등급화

### Phase 4 — 기준치 비교 및 리포트
- [ ] Unity/Rider 권장 사양 기준표 내장
- [ ] 항목별 등급 산출 로직
- [ ] 콘솔/Markdown/JSON 리포터 구현

### Phase 5 — (확장) Unity 프로젝트 연동 (헤드리스 실행)
- [x] Unity Hub 설치 목록(editors.json/레지스트리)에서 프로젝트의 `ProjectVersion.txt`와 일치하는 Editor 실행파일 자동 탐색
- [x] 도메인 리로드 측정용 Editor probe 스크립트 작성 (`AssemblyReloadEvents` + 배치 모드 종료 훅으로 결과를 JSON 파일에 기록)
- [x] CLI 실행 시 probe 스크립트를 대상 프로젝트 `Assets/Editor/`에 임시 복사 → `-batchmode -nographics -executeMethod`로 헤드리스 실행 → 결과 JSON 파싱 → 임시 스크립트 자동 삭제(예외/타임아웃 시에도 삭제 보장)
- [ ] 라이선스 미활성화 환경 대응 범위 확인 (에디터에서 실측 필요 — 열린 질문에 등록)
- [ ] **미해결**: 실제 Unity 6000.3.6f1로 End-to-End 검증 시도 중 Search 인덱싱 단계에서 10분 이상 진행되지 않는 현상 2회 재현(2026-08-05). 더 사양 좋은 머신 및 인덱싱이 이미 끝난 프로젝트로 재검증 필요 — 열린 질문에 등록

### Phase 6 — 테스트
- [x] BaselineRules/DriveMatcher/ReportFormatting 등 순수 로직 단위 테스트 작성(xUnit, 20건)
- [ ] 다양한 사양의 실제 머신에서 검증 — 현재 개발 머신(1대) 외 미검증
- [x] README 및 사용법 문서화

### Phase 7 — 배포 (GitHub Releases)
- [x] `dotnet publish -r win-x64 --self-contained -p:PublishSingleFile=true`로 단일 실행파일 빌드 스크립트 작성(GitHub Actions에 포함, 로컬 실행은 미검증)
- [x] GitHub Actions 워크플로(`release.yml`) 작성: `v*` 태그 푸시 시 자동 빌드 → GitHub Release 생성 → 실행파일 첨부
- [x] PowerShell 설치 스크립트(`install.ps1`) 작성: 최신 Release 다운로드 → `%LOCALAPPDATA%\UnityRiderBench`에 배치 → 사용자 PATH 환경변수 등록
- [x] README에 원라이너 설치 안내 추가
- [ ] 버전 확인/업데이트 정책 결정 (CLI 실행 시 최신 버전 체크 여부 — MVP 포함 여부는 열린 질문으로 이관)
- [x] `v0.1.0` 태그 푸시로 `release.yml` 실제 실행 검증 완료(2026-08-05) — 모든 스텝 성공, `unityrider-bench-win-x64.zip` 정상 게시 확인

---

## 6. CLI 사용 예시 (설계 초안)

```bash
# 전체 진단 실행 (스펙 + 벤치마크 + 경로 분석)
unityrider-bench scan --project-path "D:\MyGame" --rider-path "C:\Program Files\JetBrains\Rider"

# 스펙 조회만
unityrider-bench spec

# 벤치마크만 (시간이 걸리는 항목이므로 분리 옵션 제공)
unityrider-bench bench --cpu --disk

# 리포트 파일로 저장
unityrider-bench scan --output report.md
```

---

## 7. 열린 질문 (진행 중 결정 필요)

- Python 프로토타입 후 C# 포팅 vs 처음부터 C# — 개발 속도 우선이면 Python 우선 검토
- macOS 지원 범위 (초기엔 Windows 전용으로 좁힐지)
- 라이선스 미활성화 환경(CI 서버 등)에서의 배치 모드 지원 범위 — MVP 제외 여부
- CLI 자체 업데이트 확인 기능(버전 체크) MVP 포함 여부
- macOS/Linux용 배포 스크립트는 Windows 우선 출시 이후로 미룰지
- Phase 5 배치 모드 실행이 Unity Search 인덱싱 단계에서 장시간(10분+) 멈추는 현상의 원인 규명 — 인덱싱이 끝난 프로젝트/더 사양 좋은 머신에서 재검증, 필요 시 인덱싱 비활성화 옵션 조사
