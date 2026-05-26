# StemSplitter 작업 계획 (PLAN)

> PRD.md의 REQ-001~005를 구현하기 위한 단계별 작업 계획.
> 각 단계에는 작업 내용, 테스트 계획, 실행 프롬프트가 포함됨.

---

## 구현 순서 및 근거

```
STEP 1  BS-RoFormer/MelBand-RoFormer 모델 추가       (REQ-001 일부)   ~1시간
STEP 2  GPU 자동 감지                                 (REQ-001 일부)   ~1.5시간
STEP 3  앱 시작 시 하드웨어 환경 변경 감지 및 재구성  (REQ-004)        ~2~3시간
STEP 4  Avalonia UI 마이그레이션 (Windows 검증)       (REQ-002)        ~5~8시간
STEP 5  macOS 빌드 및 검증                            (REQ-002)        ~2~4시간
STEP 6  Windows 인스톨러 제작                         (REQ-003-WIN)    ~4~6시간
STEP 7  macOS 인스톨러 제작                           (REQ-003-MAC)    ~3~5시간
STEP 8  설치 가이드 및 사용 설명서 작성               (REQ-005)        ~2~3시간
                                                      총 예상           ~21~31시간
```

> **예상 시간 산정 기준 (Claude Code 바이브 코딩 기준)**
> - 프롬프트 입력 → Claude 생성 → 코드 검토 → 빌드/실행 → 오류 수정의 1회 사이클을 약 20~30분으로 산정
> - 각 단계의 복잡도에 따라 필요한 사이클 수가 다름
> - 테스트 환경 준비 및 실제 실행 확인 시간 포함
> - Mac이 물리적으로 다른 장소에 있는 경우 STEP 5, 7은 이동 시간 별도

**순서 근거:**
- STEP 1~3은 코어 로직 변경으로 UI와 독립적으로 먼저 진행
- STEP 4~5(Avalonia 마이그레이션)는 인스톨러(STEP 6~7) 전에 완료 필요 — 인스톨러는 최종 앱을 패키징하기 때문
- STEP 8(문서)은 모든 기능이 확정된 후 작성해야 정확한 내용 반영 가능

---

## STEP 1. BS-RoFormer / MelBand-RoFormer 모델 추가

**관련 요구사항:** REQ-001 항목 3
**예상 소요 시간:** 약 1시간
> 기존 모델 추가 패턴이 잘 정의되어 있어 반복적인 수정이 적음. 2~3 사이클로 완료 예상.

**작업 내용:**
- `StemSeparator.cs` — `GetExpectedStems()`에 bs_roformer, melband_roformer 추가 (2-stem: vocals, instrumental)
- `StemSeparator.cs` — `DemucsToStem` 매핑에 `instrumental` 항목 추가
- `StemType.cs` — `Instrumental` enum 값 추가
- `Program.cs` — `--model` 옵션 description에 새 모델 추가
- `MainWindow.xaml` — 모델 선택 ComboBox에 bs_roformer, melband_roformer 항목 추가
- `MainWindow.xaml.cs` — 2-stem 모델 선택 시 스템 수(2개) UI에 반영

**테스트 계획:**
- [ ] `bs_roformer` 모델로 CLI 실행 → vocals.wav, instrumental.wav 2개 파일 생성 확인
- [ ] `melband_roformer` 모델로 CLI 실행 → 동일 확인
- [ ] GUI에서 bs_roformer 선택 후 분리 실행 → 진행률 및 결과 정상 표시 확인
- [ ] 기존 htdemucs_6s 모델 동작 이상 없는지 회귀 테스트

**프롬프트:**
```
CLAUDE.md와 PRD.md를 참고해서 아래 작업을 진행해줘.

BS-RoFormer(bs_roformer)와 MelBand-RoFormer(melband_roformer) 모델을 CLI와 GUI에서 선택할 수 있도록 추가해줘.

주요 특징:
- 이 모델들은 2-stem 분리 모델이야 (vocals, instrumental 2개만 출력됨)
- 기존 htdemucs 계열 모델들(4~6 stem)과 함께 선택 가능해야 해
- GUI에서 2-stem 모델을 선택했을 때 결과가 2개임을 UI에서 명확히 표시해줘

수정이 필요한 파일:
- StemSplitter/Models/StemType.cs
- StemSplitter/Services/StemSeparator.cs
- StemSplitter/Program.cs
- StemSplitter.GUI/MainWindow.xaml
- StemSplitter.GUI/MainWindow.xaml.cs
```

---

## STEP 2. GPU 자동 감지

**관련 요구사항:** REQ-001 항목 2, REQ-004 기반 작업
**예상 소요 시간:** 약 1.5시간
> 신규 서비스 파일 생성 + Python subprocess 연동 + StemSeparator 수정. GPU 있는 환경과 없는 환경 모두 테스트 필요. 3~4 사이클 예상.

**작업 내용:**
- `EnvironmentDetector.cs` 신규 생성 — Python subprocess로 `torch.cuda.is_available()` 호출하여 GPU 감지
- `StemSeparator.cs` — Demucs 실행 시 `--cpu` 플래그 자동 결정 (GPU 있으면 생략, 없으면 추가)
- `SeparationOptions.cs` — `CpuOnly` 기본값을 `false`로 유지하되, 실제 GPU 감지 결과로 오버라이드되도록 처리
- GUI: Demucs 체크 시 GPU 감지 결과를 로그에 표시 ("GPU detected: NVIDIA RTX 4090" 또는 "No GPU, using CPU")

**테스트 계획:**
- [ ] GPU 있는 환경에서 실행 → GPU 모드로 자동 실행 확인 (로그에 GPU 감지 메시지)
- [ ] `--cpu` 옵션 명시 시 GPU 있어도 CPU 모드 강제 적용 확인
- [ ] GPU 없는 환경에서 실행 → CPU 모드로 자동 폴백 확인
- [ ] GPU 감지 실패 시(Python/torch 미설치 등) 오류 없이 CPU 모드로 폴백 확인

**프롬프트:**
```
CLAUDE.md와 PRD.md REQ-001을 참고해서 아래 작업을 진행해줘.

앱 실행 시 GPU(CUDA) 환경을 자동으로 감지하고, GPU가 있으면 자동으로 GPU 모드로, 없으면 CPU 모드로 실행되도록 해줘.

구현 방식:
- StemSplitter/Services/EnvironmentDetector.cs 파일을 새로 만들어줘
- Python subprocess로 `python -c "import torch; print(torch.cuda.is_available())"` 를 실행해서 GPU 여부를 감지
- StemSeparator.cs에서 Demucs 실행 시 감지 결과를 바탕으로 --cpu 플래그를 자동 결정
- 사용자가 --cpu 옵션을 명시하면 GPU가 있어도 CPU 모드 강제 적용 (기존 동작 유지)
- GUI 시작 시 로그창에 GPU 감지 결과 표시
```

---

## STEP 3. 앱 시작 시 하드웨어 환경 변경 감지 및 재구성

**관련 요구사항:** REQ-004
**예상 소요 시간:** 약 2~3시간
> JSON 읽기/쓰기, 다이얼로그 UI, PyTorch 재설치 로직이 복합적으로 얽혀있음. 특히 pip 재설치 과정의 오류 처리가 까다로워 반복 수정 가능성 있음. 5~7 사이클 예상.

**작업 내용:**
- `EnvironmentDetector.cs` 확장 — `env-config.json` 읽기/쓰기, 현재 환경과 저장된 환경 비교
- `env-config.json` 스키마 정의: `{ "gpuAvailable": bool, "torchVariant": "cuda|cpu", "recordedAt": datetime }`
- `MainWindow.xaml.cs` — 앱 시작 시 환경 비교 → 변경 감지 시 다이얼로그 표시
- 재구성 로직: pip uninstall/install을 subprocess로 실행하여 PyTorch 버전 전환
- "다음 실행 시 다시 묻지 않음" 옵션 → `env-config.json`에 `suppressPrompt` 플래그 저장

**테스트 계획:**
- [ ] `env-config.json` 없는 상태(최초 실행)에서 앱 시작 → 파일 생성 확인, 다이얼로그 없이 정상 시작
- [ ] `env-config.json`의 gpuAvailable을 수동으로 반대 값으로 변경 후 시작 → 재구성 다이얼로그 표시 확인
- [ ] 다이얼로그에서 "예" 선택 → PyTorch 재설치 진행 및 완료 확인
- [ ] 다이얼로그에서 "아니오 + 다시 묻지 않음" 선택 → 다음 실행 시 다이얼로그 미표시 확인

**프롬프트:**
```
CLAUDE.md와 PRD.md REQ-004를 참고해서 아래 작업을 진행해줘.

앱 시작 시마다 현재 GPU 환경을 감지하고, 이전 실행 환경과 비교해서 변경이 있으면 사용자에게 알리고 재구성할 수 있도록 해줘.

구현 내용:
1. STEP 2에서 만든 EnvironmentDetector.cs를 확장해서 env-config.json 읽기/쓰기 기능 추가
   - 저장 내용: GPU 유무, PyTorch 버전(cuda/cpu), 기록 시각
2. 앱 시작 시 현재 환경과 env-config.json 비교
   - GPU 추가됨: "CUDA GPU가 감지되었습니다. GPU 버전으로 재구성하시겠습니까?" 다이얼로그
   - GPU 제거됨: "GPU가 감지되지 않습니다. CPU 버전으로 재구성하시겠습니까?" 다이얼로그
   - 변화 없음: 그냥 시작
3. 재구성 동의 시 pip를 통해 PyTorch를 적절한 버전으로 재설치
4. "다음 실행 시 다시 묻지 않음" 체크박스 제공
5. env-config.json이 없으면 최초 실행으로 간주하고 파일 생성 후 정상 시작

수정/생성 파일:
- StemSplitter/Services/EnvironmentDetector.cs (STEP 2 결과물 확장)
- StemSplitter.GUI/MainWindow.xaml.cs
- env-config.json (런타임 생성)
```

---

## STEP 4. Avalonia UI 마이그레이션 (Windows 검증)

**관련 요구사항:** REQ-002
**예상 소요 시간:** 약 5~8시간
> 전체 단계 중 가장 복잡하고 리스크가 높음. WPF와 Avalonia의 API 차이(네임스페이스, 스타일링, DragDrop, StorageProvider 등)로 인해 빌드 오류와 런타임 오류가 반복적으로 발생할 수 있음. 10~15 사이클 예상. 진행 중 막히면 단계를 나눠서 (csproj 교체 → App 교체 → MainWindow 교체) 순으로 진행 권장.

**작업 내용:**
- `StemSplitter.GUI.csproj` — Avalonia 패키지 추가, 타겟 `net8.0-windows` → `net8.0` 변경, WPF/WinForms 참조 제거
- `App.xaml` + `App.xaml.cs` — Avalonia 앱 진입점으로 교체
- `MainWindow.xaml` — WPF XAML → Avalonia XAML 마이그레이션 (네임스페이스, 컨트롤명 차이 반영)
- `MainWindow.xaml.cs` — WinForms `FolderBrowserDialog` → Avalonia `StorageProvider` API로 교체
- 다크 테마 — WPF StaticResource 방식 → Avalonia styles/themes로 교체
- 드래그 앤 드롭 — Avalonia DragDrop API로 교체

**테스트 계획:**
- [ ] Windows에서 빌드 성공 확인 (`dotnet build`)
- [ ] 파일 Browse 버튼 동작 확인
- [ ] 드래그 앤 드롭으로 파일 로드 확인
- [ ] 모델/포맷/Shifts 콤보박스 선택 동작 확인
- [ ] 스템 분리 실행 → 전체/스템별 진행률 표시 확인
- [ ] 출력 폴더 선택 동작 확인
- [ ] "Open Output Folder" 버튼 동작 확인
- [ ] 다크 테마 렌더링 이상 없는지 확인
- [ ] STEP 1~3 기능들이 Avalonia GUI에서도 정상 동작하는지 회귀 테스트

**프롬프트:**
```
CLAUDE.md와 PRD.md REQ-002를 참고해서 아래 작업을 진행해줘.

StemSplitter.GUI 프로젝트의 UI 프레임워크를 WPF에서 Avalonia UI로 교체해줘. Windows와 macOS 양쪽에서 동작하는 것이 목표야.

주요 작업:
1. StemSplitter.GUI.csproj 수정
   - Avalonia 패키지 추가 (Avalonia, Avalonia.Desktop, Avalonia.Themes.Fluent)
   - 타겟 프레임워크: net8.0-windows → net8.0
   - UseWPF, UseWindowsForms 제거

2. App.xaml/App.xaml.cs → Avalonia 앱 진입점으로 교체

3. MainWindow.xaml → Avalonia XAML로 마이그레이션
   - 기존 레이아웃과 다크 테마 최대한 유지
   - WPF 전용 네임스페이스/컨트롤을 Avalonia 대응 항목으로 교체

4. MainWindow.xaml.cs → Avalonia 코드비하인드로 마이그레이션
   - WinForms FolderBrowserDialog → Avalonia StorageProvider API
   - 드래그앤드롭 → Avalonia DragDrop API

기존 기능(STEP 1~3에서 추가된 기능 포함)이 모두 동작해야 하고, Windows에서 먼저 정상 동작을 확인해줘.
```

---

## STEP 5. macOS 빌드 및 검증

**관련 요구사항:** REQ-002
**예상 소요 시간:** 약 2~4시간
> 크로스컴파일 빌드 자체는 빠름(10~15분). 시간의 대부분은 Mac에서 직접 실행하며 UI/기능 확인하는 과정. Mac-specific 문제(폰트, 다이얼로그, 권한 등)가 발견되면 Windows로 돌아가서 수정 후 재빌드하는 왕복 과정이 추가됨. Mac이 물리적으로 다른 장소에 있다면 이동 시간 별도 산정 필요.

**작업 내용:**
- Windows에서 macOS용 크로스컴파일 빌드
- Mac에서 실제 실행 및 UI/기능 검증
- Mac에서 발견된 문제 수정 (Windows에서 코드 수정 → Mac 재검증)

**테스트 계획 (Mac에서 수행):**
- [ ] `dotnet publish -r osx-x64` / `osx-arm64` 빌드 성공 확인
- [ ] macOS에서 앱 실행 확인
- [ ] 파일 다이얼로그 (macOS 스타일) 정상 동작 확인
- [ ] 드래그 앤 드롭 동작 확인
- [ ] 스템 분리 실행 → 결과 확인
- [ ] 폰트/UI 렌더링 이상 없는지 확인
- [ ] STEP 1~3 기능(모델, GPU 감지, 환경 변경 감지) 동작 확인

**프롬프트:**
```
CLAUDE.md와 PRD.md REQ-002를 참고해서 아래 작업을 진행해줘.

STEP 4에서 완성된 Avalonia UI 앱을 macOS용으로 빌드하고, Mac에서 테스트한 결과를 바탕으로 발견된 문제를 수정해줘.

빌드 명령:
- dotnet publish StemSplitter.GUI -r osx-x64 -c Release
- dotnet publish StemSplitter.GUI -r osx-arm64 -c Release

Mac에서 확인할 항목:
- 앱 실행 여부
- 파일 선택 다이얼로그 (macOS 네이티브 스타일인지)
- 드래그앤드롭
- 스템 분리 실행 및 결과
- 전체 UI 렌더링

[Mac 테스트 후 발견된 문제를 이 프롬프트 아래에 기록하고 수정 요청]
```

---

## STEP 6. Windows 인스톨러 제작

**관련 요구사항:** REQ-003-WIN
**예상 소요 시간:** 약 4~6시간
> Inno Setup 스크립트 자체는 Claude가 빠르게 생성하지만, 클린 환경에서의 테스트가 시간을 많이 잡음. Python 미설치 환경, GPU/CPU 환경을 각각 테스트해야 하고, pip install 중 네트워크/버전 문제로 인한 디버깅이 발생할 수 있음. 8~10 사이클 예상.

**작업 내용:**
- `installer/requirements.txt` 작성 — demucs, torch(cuda/cpu), torchaudio, torchvision 버전 고정
- `installer/windows/setup.iss` 작성 — Inno Setup 스크립트
  - StemSplitter 앱 바이너리 포함
  - Python 3.11 설치 여부 확인 및 자동 설치
  - CUDA 감지 스크립트 포함
  - pip install (GPU/CPU 버전 자동 선택)
  - 시작 메뉴/바탕화면 바로가기 생성
  - 언인스톨러 포함
- Inno Setup으로 `StemSplitter-Setup-win-x64.exe` 빌드

**테스트 계획:**
- [ ] 클린 Windows 환경(Python 미설치)에서 인스톨러 실행 → Python 자동 설치 확인
- [ ] GPU 없는 환경에서 설치 → CPU PyTorch 설치 확인
- [ ] GPU 있는 환경에서 설치 → CUDA PyTorch 설치 확인
- [ ] 설치 완료 후 앱 실행 확인
- [ ] 제어판 프로그램 추가/제거에 등록 확인
- [ ] 언인스톨러 정상 동작 확인
- [ ] 재설치(이미 설치된 환경)에서 인스톨러 실행 → 문제 없이 덮어쓰기 확인

**프롬프트:**
```
CLAUDE.md와 PRD.md REQ-003-WIN을 참고해서 아래 작업을 진행해줘.

Windows용 원클릭 인스톨러를 Inno Setup으로 만들어줘.

작업 내용:
1. installer/requirements.txt 작성
   - demucs, torch, torchaudio, torchvision 버전 고정
   - GPU용과 CPU용 주석으로 구분

2. installer/windows/setup.iss 작성
   - STEP 4에서 빌드된 StemSplitter.GUI Release 바이너리 포함
   - Python 3.11 미설치 시 자동 다운로드/설치
   - CUDA 환경 감지 (nvidia-smi 명령 또는 레지스트리 확인)
   - 감지 결과에 따라 GPU용/CPU용 PyTorch 자동 설치
   - demucs 및 의존 패키지 설치 (requirements.txt 기반, 버전 고정)
   - 시작 메뉴, 바탕화면 바로가기 생성
   - 언인스톨러 포함
   - 제어판 프로그램 추가/제거 등록

산출물: StemSplitter-Setup-win-x64.exe
```

---

## STEP 7. macOS 인스톨러 제작

**관련 요구사항:** REQ-003-MAC
**예상 소요 시간:** 약 3~5시간
> pkgbuild/productbuild는 Inno Setup보다 다소 생소한 도구라 초기 셋업에 시간이 걸릴 수 있음. 단, macOS 인스톨러는 GPU 분기가 없어(CPU만 지원) Windows보다 로직이 단순함. Mac에서 직접 빌드 및 테스트해야 하므로 Mac 작업 시간 포함. 6~8 사이클 예상.

**작업 내용:**
- `installer/macos/build.sh` 작성 — pkgbuild/productbuild로 .pkg 생성
  - StemSplitter.app 번들 생성
  - Python 3.11 설치 여부 확인 및 자동 설치 (Homebrew 또는 공식 패키지)
  - PyTorch CPU 버전 + demucs 설치 (macOS는 CUDA 미지원)
  - Apple Silicon의 경우 MPS 지원 여부 확인
- Intel (`osx-x64`)용과 Apple Silicon (`osx-arm64`)용 각각 빌드
- 산출물: `StemSplitter-Setup-mac-x64.pkg`, `StemSplitter-Setup-mac-arm64.pkg`

**테스트 계획 (Mac에서 수행):**
- [ ] 클린 macOS 환경(Python 미설치)에서 .pkg 실행 → Python 자동 설치 확인
- [ ] demucs 및 의존 패키지 설치 확인
- [ ] /Applications에 앱 설치 확인
- [ ] 설치 완료 후 앱 실행 확인
- [ ] Apple Silicon Mac에서 arm64 패키지 테스트
- [ ] 언인스톨 방법 확인 (macOS 표준: /Applications에서 삭제 + 설치된 Python 패키지 정리)

**프롬프트:**
```
CLAUDE.md와 PRD.md REQ-003-MAC을 참고해서 아래 작업을 진행해줘.

macOS용 인스톨러를 pkgbuild/productbuild로 만들어줘. Mac에서 실행할 셸 스크립트로 작성해도 돼.

작업 내용:
1. installer/macos/build.sh 작성
   - STEP 5에서 빌드된 osx-x64, osx-arm64 바이너리로 .app 번들 생성
   - pkgbuild/productbuild로 .pkg 파일 생성

2. 설치 스크립트 (postinstall) 작성
   - Python 3.11 설치 여부 확인 (없으면 공식 패키지로 자동 설치)
   - venv 생성 후 PyTorch CPU 버전 설치 (macOS는 CUDA 미지원)
   - Apple Silicon 감지 시 MPS 지원 여부 확인
   - demucs 및 의존 패키지 설치 (requirements.txt 기반, 버전 고정)
   - /Applications에 앱 설치

산출물:
- StemSplitter-Setup-mac-x64.pkg
- StemSplitter-Setup-mac-arm64.pkg
```

---

## STEP 8. 설치 가이드 및 사용 설명서 작성

**관련 요구사항:** REQ-005
**예상 소요 시간:** 약 2~3시간
> 8개 문서 모두 Claude가 한 번에 초안을 생성할 수 있어 생성 자체는 빠름. 시간의 대부분은 실제 앱과 대조하며 내용 정확성을 검토하고 수정하는 과정. 한국어/영어 간 내용 일치 여부 검토 포함. 4~5 사이클 예상.

**작업 내용:**
- `docs/ko/install-guide-windows.md`
- `docs/ko/install-guide-macos.md`
- `docs/ko/user-manual-windows.md`
- `docs/ko/user-manual-macos.md`
- `docs/en/install-guide-windows.md`
- `docs/en/install-guide-macos.md`
- `docs/en/user-manual-windows.md`
- `docs/en/user-manual-macos.md`

**테스트 계획:**
- [ ] 설치 가이드를 따라 실제로 처음부터 설치해보고 누락/오류 항목 확인
- [ ] 사용 설명서의 모든 기능 설명이 실제 구현과 일치하는지 확인
- [ ] 한국어/영어 내용이 서로 일치하는지 확인
- [ ] Windows/macOS 플랫폼별 차이(경로, 단축키, 다이얼로그 등)가 정확히 반영되었는지 확인

**프롬프트:**
```
CLAUDE.md, README.md, PRD.md REQ-005를 참고해서 아래 문서들을 작성해줘.
STEP 1~7이 모두 완료된 최종 상태를 기준으로 작성해야 해.

작성할 문서 (총 8개):
- docs/ko/install-guide-windows.md   ← 설치 가이드 (한국어, Windows)
- docs/ko/install-guide-macos.md     ← 설치 가이드 (한국어, macOS)
- docs/ko/user-manual-windows.md     ← 사용 설명서 (한국어, Windows)
- docs/ko/user-manual-macos.md       ← 사용 설명서 (한국어, macOS)
- docs/en/install-guide-windows.md   ← Install Guide (English, Windows)
- docs/en/install-guide-macos.md     ← Install Guide (English, macOS)
- docs/en/user-manual-windows.md     ← User Manual (English, Windows)
- docs/en/user-manual-macos.md       ← User Manual (English, macOS)

설치 가이드 포함 내용:
- 사전 요구사항 (ffmpeg 등)
- 인스톨러 실행 단계별 설명 (스크린샷 위치 placeholder 포함)
- GPU 감지 및 Python 환경 구성 과정 안내
- 첫 실행 방법
- Troubleshooting

사용 설명서 포함 내용:
- GUI 각 UI 요소 설명
- 파일 불러오기 (Browse / 드래그앤드롭)
- 모델 선택 기준 (htdemucs_6s vs bs_roformer 등)
- 스템 분리 실행 및 결과 확인
- CLI 명령어 및 옵션 전체 목록
- 출력 파일 구조
- 플랫폼별 차이점 (경로, 단축키 등)
```

---

## 진행 상황 체크리스트

| 단계 | 작업 | 예상 시간 | 상태 |
|------|------|----------|------|
| STEP 1 | BS-RoFormer/MelBand-RoFormer 모델 추가 | ~1시간 | ⬜ 미시작 |
| STEP 2 | GPU 자동 감지 | ~1.5시간 | ⬜ 미시작 |
| STEP 3 | 앱 시작 시 환경 변경 감지 및 재구성 | ~2~3시간 | ⬜ 미시작 |
| STEP 4 | Avalonia UI 마이그레이션 (Windows) | ~5~8시간 | ⬜ 미시작 |
| STEP 5 | macOS 빌드 및 검증 | ~2~4시간 | ⬜ 미시작 |
| STEP 6 | Windows 인스톨러 제작 | ~4~6시간 | ⬜ 미시작 |
| STEP 7 | macOS 인스톨러 제작 | ~3~5시간 | ⬜ 미시작 |
| STEP 8 | 설치 가이드 및 사용 설명서 작성 | ~2~3시간 | ⬜ 미시작 |
| **합계** | | **~21~31시간** | |
