# 손님 대사 로컬 AI

이 버전은 **Windows 64비트**에서 Qwen3-0.6B를 CPU로 실행합니다. 게임과 함께 모델·실행 엔진이 포함되므로 플레이어가 Ollama, Python, 계정, API 키를 준비할 필요가 없습니다. 플레이 중 외부 서버에 접속하지 않으며 AI 호출료나 AI 서버 운영비가 없습니다.

게임이 먼저 정답 번호를 정합니다. `1 = 담백`, `2 = 달콤`, `3 = 고소`입니다. AI는 조건에 맞는 손님 대사만 생성하고 성공 판정을 하지 않습니다. 작은 모델의 한국어 문장은 어색하거나 조건에서 벗어날 수 있습니다. 사용자가 요청한 규칙대로 비어 있지 않은 생성 대사는 의미 검사 없이 표시합니다. 파일 누락, 실행 불가, 시간 초과, 빈 응답일 때는 호출자가 기본 대사를 표시합니다.

## 포함 파일

- `Game/Assets/Shop/Scripts/AI/LocalCustomerDialogue.cs`: 숨겨진 로컬 실행 엔진의 시작·대사 생성·종료.
- `Game/Assets/StreamingAssets/LocalAi/Models/model.gguf`: 공식 Qwen3-0.6B Q8_0, 639,446,688 bytes.
- `Game/Assets/StreamingAssets/LocalAi/Runtime/win-x64/`: 공식 llama.cpp CPU 실행 파일·DLL·LLVM OpenMP 라이선스와 변경 없는 Microsoft release CRT DLL. CRT도 앱 폴더에 들어 있으므로 플레이어가 Visual C++ 재배포 패키지를 별도로 설치할 필요가 없습니다.
- `Game/Assets/StreamingAssets/LocalAi/Licenses/`: llama.cpp MIT, Qwen3 Apache-2.0, Microsoft 재배포 목록·Community 라이선스·CRT 파일별 SHA256.
- `Game/Assets/StreamingAssets/LocalAi/provenance.json`: 고정 다운로드 URL, 버전, SHA256.

Unity가 `StreamingAssets`를 Windows 빌드에 그대로 복사합니다. 배포할 때 `exe`만 보내지 말고 빌드 폴더 전체를 보내야 합니다. 모델이 빠진 빌드는 실제 AI 대신 기본 대사를 사용합니다. 현재 다른 OS용 실행 파일은 포함하지 않았습니다.

## 팀원이 GitHub에서 받은 경우

639 MB 모델은 일반 GitHub 파일 크기 제한을 넘으므로 Git에 넣지 않습니다. 이 PC의 전달된 프로젝트와 게임 빌드에는 모델이 이미 있습니다. 팀원이 새로 클론하면 저장소 루트에서 한 번 실행합니다.

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\FetchLocalAi.ps1
```

스크립트는 공식 고정 버전에서 파일을 받으며 모델·실행 엔진 ZIP SHA256을 확인합니다. 이 다운로드에만 인터넷이 필요합니다. 이후 게임에서는 자동 다운로드하지 않습니다.

## 코드 연결

`DdukDduk.Shop.AI.LocalCustomerDialogue` 컴포넌트를 생성하고 배치 준비 중 `WarmupAsync(cancellationToken)`를 호출할 수 있습니다. `GenerateAsync(answer, day, cancellationToken)`는 `DialogueGenerationResult`의 `Text`, `UsedAi`, `Diagnostic`을 반환합니다. 호출자의 취소는 `OperationCanceledException`으로 전달합니다. 시작 최대 대기 60초, 생성 최대 대기 45초이며 더 짧은 호출자 취소도 지원합니다.

엔진은 `127.0.0.1`의 빈 포트로 실행하며 게임 종료·컴포넌트 제거 때 자신이 시작한 프로세스만 종료합니다. 다른 로컬 AI 프로그램은 건드리지 않습니다. 콘솔 창은 열지 않습니다. 진단 로그는 Unity `Application.persistentDataPath/local-ai.log`에 기록합니다.

Microsoft CRT는 이 PC에 설치된 Visual Studio 2026 Community의 `VC/Redist/MSVC/14.51.36231/x64/Microsoft.VC145.CRT`에서 가져온 release 파일입니다. 디버그 또는 preview 파일을 사용하지 않았습니다. 파일 버전은 `14.51.36247.0`이며 `[VisualStudioFolder]/VC/redist`의 변경 없는 파일을 프로그램과 함께 배포할 수 있는 공식 재배포 목록을 확인했습니다. Microsoft 파일은 MIT/Apache로 재라이선스하지 않으며 동봉한 Microsoft 조건이 적용됩니다. 배포자는 Microsoft Distributable Code 보호 조건을 게임 배포 약관에 유지해야 합니다. 공식 라이선스의 Distributable Code 절을 함께 보관합니다.

## 실제 추론 검증

llama.cpp `b11344`, `0.5.0-dev (build 11344, commit ec7630a64)`, CPU 4 threads, 문맥 2048, thinking off, 출력 90 tokens. 이 PC에서 모델 준비 약 1.06초, 한국어 세 조건 출력 각각 약 1.73초 / 1.72초 / 1.01초를 확인했습니다. 실제 결과 원본은 작업 폴더의 `local-ai-real-inference.json`에 저장했습니다. 다른 PC의 생성 속도는 CPU·메모리·저장 장치에 따라 달라집니다. 모델 자체 용량은 약 610 MiB이고 실행 엔진·문맥 메모리가 추가로 필요합니다.

추가로 숨겨진 프로세스의 로드된 모듈을 조사해 `MSVCP140.dll`, `VCRUNTIME140.dll`, `VCRUNTIME140_1.dll`이 `Windows/System32` 대신 동봉한 `Runtime/win-x64`에서 로드되는 것을 확인하고 실제 한국어 응답을 다시 생성했습니다. 원본은 `local-ai-portable-modules.json`입니다. 별도의 깨끗한 PC 실물 테스트는 수행하지 않았습니다.

최종 Unity 플레이어에서 프롬프트 수정 후 실제 생성한 예:

- 담백: “점심 뒤에 먹으려면 쌀맛이 은은한 떡을 추천해 주세요.”
- 달콤: “달달한 간식이 먹고 싶어요. 이 꿀처럼 입안이 달달해지는 떡은 점심 뒤에 맛을 내세요!”
- 고소: “볶은 콩의 향이 귀여운 분위기를 선사하는 떡이 좋아요. 가족과 함께 즐기고 싶다면, 하나 골라 드리겠습니다.”

한 요청에 다른 맛의 예시를 섞지 않고 해당 맛의 예시 하나를 제공합니다. 최근 대사도 맛별로 기억합니다. 위 예시처럼 문법·화자 역할이 어색할 수 있으며 생성된 문장은 그대로 표시합니다. 게임 실행 검증 25개와 세 맛의 실제 응답은 `Docs/Verification`에 기록했습니다.

## 출처와 라이선스

- [Qwen 공식 GGUF 모델](https://huggingface.co/Qwen/Qwen3-0.6B-GGUF), 고정 revision `23749fefcc72300e3a2ad315e1317431b06b590a`, Apache-2.0.
- [llama.cpp 공식 b11344 릴리스](https://github.com/ggml-org/llama.cpp/releases/tag/b11344), MIT.
- [llama.cpp b11344 서버 API](https://github.com/ggml-org/llama.cpp/blob/b11344/tools/server/README.md). 사용: `/health`, `/v1/chat/completions`, `chat_template_kwargs.enable_thinking=false`, `--reasoning-budget 0`, CPU/loopback/offline 옵션.
- [Microsoft 공식 Visual Studio 2026 재배포 목록](https://learn.microsoft.com/en-us/visualstudio/releases/2026/redistribution), [Community 라이선스](https://visualstudio.microsoft.com/license-terms/vs2026-ga-community/), [앱 로컬 C++ 배포 문서](https://learn.microsoft.com/en-us/cpp/windows/deployment-in-visual-cpp).

모델 SHA256: `9465e63a22add5354d9bb4b99e90117043c7124007664907259bd16d043bb031`.

실행 엔진 ZIP SHA256: `e604411ce3d01de6e5ba28241d7565f74eec83d3844e4ce8317b084d451e46ce`.
