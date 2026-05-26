# StemSplitter PRD (Product Requirements Document)

## 요구사항 목록

---

### REQ-001. 모델 확장 및 GPU 자동 감지

> **상태 (항목별 상이)**
> - 항목 1 (MDX 계열 추가): **보류**
> - 항목 2 (GPU 자동 감지): **진행 예정** (REQ-004와 연계)
> - 항목 3 (BS-RoFormer/MelBand-RoFormer 추가): **진행 예정**

**배경**
현재 htdemucs 계열 3종만 지원하며, GPU 사용 여부를 사용자가 수동으로 설정해야 함.

**요구사항**

1. **모델 확장** — 아래 모델을 CLI 및 GUI에서 선택 가능하도록 추가 *(보류)*
   - `mdx` — MDX-Net 기반, 보컬 분리에 강함
   - `mdx_extra` — mdx 개선판, 더 높은 품질
   - `mdx_q` — mdx 경량(quantized) 버전
   - `mdx_extra_q` — mdx_extra 경량 버전

2. **GPU 자동 감지 및 활용** — 실행 환경에 CUDA 사용 가능한 GPU가 있으면 자동으로 GPU 모드로 실행. GPU가 없으면 CPU 모드로 폴백. 사용자가 수동으로 CPU 전용 모드를 강제할 수 있는 옵션(`--cpu`)은 유지.

3. **BS-RoFormer / MelBand-RoFormer 추가** — 아래 모델을 CLI 및 GUI에서 선택 가능하도록 추가
   - `bs_roformer` (Band-Split RoFormer) — 보컬 분리 품질이 Demucs보다 우수, 현재 무료 오픈소스 중 최상위권. 2-stem (vocals / instrumental)
   - `melband_roformer` (MelBand-RoFormer) — BS-RoFormer 변형, 특정 악기 분리에 강함. 2-stem 중심
   - 2-stem 모델의 경우 UI에서 분리 결과가 2개(vocals, instrumental)임을 명확히 표시

**영향 범위**
- `StemSplitter/Services/StemSeparator.cs` — GPU 감지 로직 추가, 모델 목록 확장
- `StemSplitter/Program.cs` — `--model` 옵션 설명 업데이트
- `StemSplitter.GUI/MainWindow.xaml` — 모델 선택 ComboBox 항목 추가
- `StemSplitter.GUI/MainWindow.xaml.cs` — GPU 감지 결과를 UI에 표시

---

### REQ-002. 크로스플랫폼 지원 (Windows + macOS)

**배경**
현재 CLI(`StemSplitter`)는 .NET 8.0 기반으로 이미 크로스플랫폼이나, GUI(`StemSplitter.GUI`)가 WPF를 사용하여 Windows 전용으로만 동작함.

**요구사항**

1. **GUI 프레임워크 교체** — WPF를 **Avalonia UI**로 교체하여 Windows/macOS 양쪽에서 동작하도록 함
   - Avalonia는 WPF와 XAML 문법이 유사하여 마이그레이션 비용이 낮음
   - 기존 다크 테마 및 UI 레이아웃은 최대한 유지
2. **CLI는 변경 없음** — 이미 .NET 8.0 크로스플랫폼으로 동작하므로 수정 불필요
3. **프로젝트 타겟 변경** — `StemSplitter.GUI.csproj`의 타겟을 `net8.0-windows` → `net8.0`으로 변경

**영향 범위**
- `StemSplitter.GUI/StemSplitter.GUI.csproj` — Avalonia 패키지 추가, 타겟 프레임워크 변경
- `StemSplitter.GUI/App.xaml` + `App.xaml.cs` — Avalonia 앱 진입점으로 교체
- `StemSplitter.GUI/MainWindow.xaml` + `MainWindow.xaml.cs` — Avalonia XAML/코드비하인드로 마이그레이션
- `StemSplitter.GUI/` 전반 — WinForms(`FolderBrowserDialog`) 의존성 제거, Avalonia 대체 API 사용

**개발 워크플로우**

| 작업 | Windows | Mac |
|------|---------|-----|
| 코드 작성 | ✅ | |
| Windows 빌드/테스트 | ✅ | |
| macOS 바이너리 생성 (크로스컴파일) | ✅ | |
| macOS에서 실제 실행/검증 | ❌ | ✅ |
| UI 렌더링/폰트/다이얼로그 확인 | ❌ | ✅ |

권장 순서:
1. Windows에서 Avalonia 마이그레이션 완료 및 Windows 동작 확인
2. `dotnet publish -r osx-x64` / `osx-arm64` 로 macOS 바이너리 생성
3. Mac에서 최종 실행 검증 → 문제 있으면 Windows에서 수정
4. (선택) GitHub Actions macOS 러너로 빌드/기본 테스트 자동화

---

### REQ-003. 배포용 인스톨러 제작 (Windows / macOS)

**배경**
현재는 실행 파일과 Python 의존성을 별도로 수동 설치해야 하며, 설치 과정에서 에러가 날 때마다 패키지를 하나씩 추가 설치해야 하는 문제가 있음. 한 번의 인스톨러 실행으로 앱과 모든 Python 의존성이 완전히 설치되도록 개선 필요.

**방식: 온라인 설치형 + GPU 자동 감지**
- 인스톨러 자체는 소용량 (50~100MB 수준)
- 설치 과정 중 Python 환경 구성 및 패키지 자동 다운로드/설치
- `requirements.txt`로 패키지 버전 고정 → 의존성 충돌 방지
- CUDA 환경 자동 감지 → GPU용/CPU용 PyTorch 자동 선택 설치

**공통 설치 흐름**
```
인스톨러 실행
  → StemSplitter 앱 설치
  → Python 설치 여부 확인 (없으면 자동 설치)
  → CUDA(GPU) 환경 감지
  → GPU 환경: torch (CUDA) + demucs 설치
    CPU 환경: torch (CPU) + demucs 설치
  → 설치 완료
```

---

#### REQ-003-WIN. Windows 인스톨러

**도구: Inno Setup**
- `.exe` 형태의 단일 인스톨러 파일 생성
- 시작 메뉴 / 바탕화면 바로가기 생성
- 제어판 프로그램 추가/제거에 등록
- 언인스톨러 포함

**설치 항목**
1. StemSplitter 앱 바이너리 (`.exe`)
2. Python 3.11 (미설치 시 자동 설치, 시스템 Python과 별도 격리된 venv 사용)
3. PyTorch — CUDA 감지 결과에 따라 자동 선택
   - CUDA 있음: `torch` (CUDA 버전)
   - CUDA 없음: `torch` (CPU 버전)
4. Demucs 및 의존 패키지 (버전 고정)

**산출물**
- `StemSplitter-Setup-win-x64.exe`

---

#### REQ-003-MAC. macOS 인스톨러

**도구: pkgbuild / productbuild**
- `.pkg` 형태의 표준 macOS 인스톨러 파일 생성
- `/Applications`에 앱 설치
- Intel (x64) / Apple Silicon (arm64) 각각 또는 Universal Binary 지원

**설치 항목**
1. StemSplitter 앱 번들 (`.app`)
2. Python 3.11 (미설치 시 자동 설치, Homebrew 또는 공식 패키지)
3. PyTorch — macOS는 CUDA 미지원이므로 CPU 버전 설치
   - Apple Silicon의 경우 MPS(Metal Performance Shaders) 활용 가능 여부 확인 후 적용
4. Demucs 및 의존 패키지 (버전 고정)

**산출물**
- `StemSplitter-Setup-mac-x64.pkg` (Intel)
- `StemSplitter-Setup-mac-arm64.pkg` (Apple Silicon)

**영향 범위 (공통)**
- `installer/` 신규 디렉토리 — 인스톨러 스크립트 및 리소스
- `installer/requirements.txt` — 버전 고정된 Python 의존성 목록
- `installer/windows/setup.iss` — Inno Setup 스크립트
- `installer/macos/build.sh` — macOS 패키지 빌드 스크립트

---

### REQ-004. 앱 시작 시 하드웨어 환경 변경 감지 및 자동 재구성

**배경**
설치 이후 하드웨어 환경이 바뀌는 경우(GPU 추가 또는 제거)가 있을 수 있음. 앱 시작 시마다 현재 환경을 감지하여 설치 당시 구성과 비교하고, 변경이 있을 경우 사용자에게 알리고 재구성할 수 있도록 함.

**감지 대상 시나리오**

| 시나리오 | 이전 환경 | 현재 환경 | 처리 |
|---------|----------|----------|------|
| GPU 새로 추가됨 | CPU 전용 | GPU(CUDA) 사용 가능 | CUDA PyTorch로 재설치 안내 |
| GPU 제거 또는 비활성화 | GPU(CUDA) | CPU 전용 | CPU PyTorch로 재설치 안내 |
| 변화 없음 | — | — | 그냥 시작 |

**처음 실행하는 하드웨어인 경우**
설치 시 기록된 환경 정보(`env-config.json`)를 기준으로 현재 환경과 비교. 파일이 없으면 최초 실행으로 간주하고 현재 환경을 기록.

**앱 시작 시 흐름**
```
앱 시작
  → 현재 GPU 환경 감지
  → env-config.json 로드 (설치 시 기록된 환경)
  → 환경 변화 없음: 정상 시작
  → GPU 추가 감지:
      "CUDA GPU가 감지되었습니다. 성능 향상을 위해 GPU 버전으로 재구성하시겠습니까?"
      → 예: PyTorch CUDA 버전으로 자동 재설치 후 재시작
      → 아니오: 기존 CPU 설정 유지 (다음 실행 시 다시 묻지 않음 옵션 제공)
  → GPU 제거 감지:
      "GPU가 감지되지 않습니다. CPU 버전으로 재구성하시겠습니까?"
      → 예: PyTorch CPU 버전으로 자동 재설치 후 재시작
      → 아니오: 기존 GPU 설정 유지 시도 (오류 발생 가능 안내)
```

**영향 범위**
- `env-config.json` — 설치/실행 환경 기록 파일 (GPU 유무, PyTorch 버전 등)
- `StemSplitter/Services/EnvironmentDetector.cs` — GPU 감지 및 환경 비교 로직 (신규)
- `StemSplitter.GUI/MainWindow.xaml.cs` — 시작 시 환경 감지 및 재구성 알림 UI

---

### REQ-005. 설치 가이드 및 사용 설명서 작성

**배경**
설치 과정과 사용법이 복잡하여 문서화가 필요함. Windows와 macOS의 설치 절차가 다르므로 플랫폼별로 별도 구성하고, 한국어/영어 두 언어로 제공.

**요구사항**

1. **설치 가이드** — 인스톨러 실행부터 첫 실행까지의 전 과정을 단계별로 설명
   - 사전 요구사항 (ffmpeg 등 별도 설치 항목)
   - 인스톨러 실행 및 설치 옵션 설명
   - 설치 중 GPU 감지 및 Python 환경 구성 과정 안내
   - 설치 완료 후 첫 실행 방법
   - 문제 발생 시 대처 방법 (Troubleshooting)

2. **사용 설명서** — GUI 및 CLI 사용법 전반을 설명
   - GUI: 각 UI 요소 설명, 파일 불러오기, 옵션 설정, 스템 분리 실행, 결과 확인
   - CLI: 명령어 및 옵션 전체 목록, 사용 예시
   - 모델별 특징 및 선택 기준
   - 출력 파일 구조 설명

3. **플랫폼별 분리 구성** — Windows / macOS 각각 별도 문서로 작성
   - 설치 경로, 단축키, 파일 다이얼로그 등 플랫폼별 차이 반영

4. **언어** — 한국어 / 영어 각각 작성

**산출물**

```
docs/
├── ko/
│   ├── install-guide-windows.md   ← 설치 가이드 (한국어, Windows)
│   ├── install-guide-macos.md     ← 설치 가이드 (한국어, macOS)
│   ├── user-manual-windows.md     ← 사용 설명서 (한국어, Windows)
│   └── user-manual-macos.md       ← 사용 설명서 (한국어, macOS)
└── en/
    ├── install-guide-windows.md   ← 설치 가이드 (영어, Windows)
    ├── install-guide-macos.md     ← 설치 가이드 (영어, macOS)
    ├── user-manual-windows.md     ← 사용 설명서 (영어, Windows)
    └── user-manual-macos.md       ← 사용 설명서 (영어, macOS)
```
