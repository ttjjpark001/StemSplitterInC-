# StemSplitter - Claude Context

## 프로젝트 개요

Demucs AI 엔진을 C# CLI/GUI로 래핑한 오디오 스템 분리 도구.
Python의 Demucs를 subprocess로 실행하고, 결과 파일을 정리하는 구조.

> **진행 중인 계획 (PRD 참조)**
> - REQ-001: BS-RoFormer/MelBand-RoFormer 모델 추가 및 GPU 자동 감지 (MDX 계열 추가는 보류)
> - REQ-002: WPF → Avalonia UI 교체로 macOS 지원
> - REQ-003: Windows/macOS 배포용 인스톨러 제작
> - REQ-004: 앱 시작 시 하드웨어 환경 변경 감지 및 재구성
> - REQ-005: 설치 가이드 및 사용 설명서 작성 (한국어/영어, Windows/macOS 각각)

## 솔루션 구조

```
StemSplitterInC-/
├── StemSplitter/           ← CLI 프로젝트 (net8.0)
│   ├── Program.cs          ← System.CommandLine 기반 CLI 진입점
│   ├── Models/
│   │   ├── StemType.cs         ← enum (Drums, Bass, Vocals, ElectricGuitar, Piano, Other 등)
│   │   ├── SeparationOptions.cs ← 분리 옵션 (모델, 포맷, shifts, cpu 등)
│   │   ├── SeparationResult.cs  ← 분리 결과 (성공/실패, 출력 파일 목록, 시간)
│   │   └── StemProgress.cs      ← 진행 상황 (Info/OverallProgress/StemProgress/StemComplete)
│   └── Services/
│       ├── AudioProcessor.cs    ← NAudio 기반 파일 검증, 정보 조회, WAV 변환
│       ├── StemSeparator.cs     ← Demucs subprocess 실행 및 진행률 파싱 핵심 서비스
│       └── EnvironmentDetector.cs ← [계획] GPU/하드웨어 환경 감지 및 비교 (REQ-001, REQ-004)
└── StemSplitter.GUI/       ← GUI 프로젝트 (현재 WPF/net8.0-windows → 계획: Avalonia/net8.0)
    ├── App.xaml            ← 다크 테마 리소스 정의
    ├── MainWindow.xaml     ← UI 레이아웃
    └── MainWindow.xaml.cs  ← UI 로직 (드래그앤드롭, 진행률, 로그)

[계획] installer/           ← 배포용 인스톨러 스크립트 (REQ-003)
    ├── requirements.txt    ← 버전 고정된 Python 의존성 목록
    ├── windows/setup.iss   ← Inno Setup 스크립트
    └── macos/build.sh      ← macOS 패키지 빌드 스크립트

env-config.json             ← [계획] 설치/실행 환경 기록 파일 (REQ-004)

[계획] docs/                ← 설치 가이드 및 사용 설명서 (REQ-005)
    ├── ko/
    │   ├── install-guide-windows.md
    │   ├── install-guide-macos.md
    │   ├── user-manual-windows.md
    │   └── user-manual-macos.md
    └── en/
        ├── install-guide-windows.md
        ├── install-guide-macos.md
        ├── user-manual-windows.md
        └── user-manual-macos.md
```

## 빌드 명령

```powershell
# CLI 빌드
dotnet build StemSplitter/StemSplitter.csproj

# GUI 빌드
dotnet build StemSplitter.GUI/StemSplitter.GUI.csproj

# 전체 릴리즈 빌드
dotnet build -c Release
```

## 실행 방법

```powershell
# CLI 실행
dotnet run --project StemSplitter -- <input.mp3>
dotnet run --project StemSplitter -- check
dotnet run --project StemSplitter -- info <input.mp3>

# GUI 실행
dotnet run --project StemSplitter.GUI
```

## 의존성

- **NAudio 2.2.1** - 오디오 파일 정보 조회, WAV 변환
- **NAudio.Lame 2.1.0** - MP3 인코딩
- **System.CommandLine 2.0.0-beta4** - CLI 파서
- **Demucs** (외부, Python) - 실제 AI 스템 분리 엔진. `pip install demucs` 필요

## 핵심 플로우

1. `StemSeparator.SeparateAsync()` 호출
2. 임시 디렉토리 생성 (`%TEMP%/StemSplitter/<guid>`)
3. `demucs <input> -n <model> -o <tempDir>` subprocess 실행
4. stderr에서 `(\d+)%` 패턴으로 진행률 파싱 → `IProgress<StemProgress>` 보고
5. Demucs 출력 파일 위치 탐색 (`tempDir/model/trackName/*.wav`)
6. 최종 출력 디렉토리로 파일 복사 (`trackName_stemname.wav`)
7. 임시 디렉토리 삭제

## Demucs 출력 파일 구조

```
tempDir/
└── htdemucs_6s/
    └── <trackName>/
        ├── drums.wav
        ├── bass.wav
        ├── vocals.wav
        ├── guitar.wav
        ├── piano.wav
        └── other.wav
```

## 지원 모델

현재 지원:

| 모델 | 스템 수 | 스템 종류 |
|------|--------|-----------|
| `htdemucs` | 4 | drums, bass, vocals, other |
| `htdemucs_6s` | 6 | drums, bass, vocals, guitar, piano, other |
| `htdemucs_ft` | 4 | drums, bass, vocals, other (vocals 특화) |

추가 보류 중 (REQ-001):

| 모델 | 특징 |
|------|------|
| `mdx` | MDX-Net 기반, 보컬 분리에 강함 |
| `mdx_extra` | mdx 개선판, 더 높은 품질 |
| `mdx_q` | mdx 경량(quantized) 버전 |
| `mdx_extra_q` | mdx_extra 경량 버전 |

추가 예정 — BS-RoFormer 계열 (REQ-001):

| 모델 | 특징 | 분리 결과 |
|------|------|----------|
| `bs_roformer` | 보컬 분리 품질 최상위권, 무료 오픈소스 | 2-stem (vocals / instrumental) |
| `melband_roformer` | BS-RoFormer 변형, 특정 악기 분리에 강함 | 2-stem 중심 |

## 알려진 이슈 / 주의사항

- `StemType` enum에 `AcousticGuitar`, `Strings`가 정의되어 있으나 Demucs가 해당 스템을 생성하지 않아 실제로는 미사용
- 진행률 파싱은 Demucs stderr의 `%` 문자 기반 추정값이라 정확하지 않을 수 있음
- GUI에는 `Jobs` 옵션(`-j`) UI가 없음 (CLI에만 존재)
- `StemSeparator`에 `PostProcessStemsAsync` (string progress용)와 `PostProcessStemsWithProgressAsync` (StemProgress용) 두 버전이 있으며, 전자는 미사용 상태
