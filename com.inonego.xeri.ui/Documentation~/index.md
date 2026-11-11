# Xeri UI

Xeri UI는 **Unity 7의 UI Toolkit·UGUI를 사용해 애플리케이션 단위 UI를 구성하는 패키지**입니다. Unity의 렌더러를 대체하지 않고 Screen, Modal, Window, Tray, Presentation, Focus와 Input의 표시 위치·상태·수명을 관리합니다.

프로젝트는 `UIRuntime`을 초기화한 다음 `UIContext`와 필요한 기능을 조합합니다. UI Toolkit 전용 구성과 UGUI를 포함한 mixed 구성을 지원합니다.

## 처음 사용하는 경우

1. [설치와 의존성](getting-started/installation.md)에서 Unity 버전과 패키지를 준비합니다.
2. [프로젝트 설정과 시작](getting-started/setup.md)에서 Presentation Layout, UI Settings, Bootstrapper 또는 Scene-owned Runtime을 연결합니다.
3. [Core API](core/index.md)에서 Screen·Modal·Presentation 사용 경로를 선택합니다.
4. 결과를 직접 조작해 보려면 [Validation Desktop](validation.md)을 실행합니다.

## 하고 싶은 작업별 안내

| 작업 | 문서 |
| --- | --- |
| Screen 열기·닫기, Gameplay Input 차단, Focus | [Screen과 입력](core/screens.md) |
| Layer 선택, transient UI 표시, Modal, Scene Fade | [Presentation](core/presentation.md) |
| 일시적인 로고·안내 화면 | [Splash](core/splash.md) |
| 특정 UI 요소 강조·주변 입력 차단 | [Spotlight](core/spotlight.md) |
| 이동·최소화·최대화 가능한 Window | [Window](window/index.md) |
| Window View 생성, UI 상태 저장·복원 | [Window View Source](window/view-source.md) |
| Window/작업 항목 선택·닫기·재정렬 | [Tray](tray.md) |
| 체력·진행 값과 증감 표시 | [Bar](bar.md) |
| Unity 씬에서 기능을 자유롭게 실험 | [Validation Desktop](validation.md) |
| 시작·표시·입력 오류 해결 | [문제 해결](core/troubleshooting.md) |

## 구성 방식

- **UIRuntime**: Root Presentation, Main Context와 공통 Focus/Input/Transition 서비스 소유
- **UIContext**: Screen/Modal 상태, Focus 및 Gameplay Input 권한 소유
- **PresentationTarget**: 현재 Context의 Layer 또는 프로젝트 공통 Host destination 지정
- **Window Workspace**: 같은 표시 Layer에서 여러 Window의 상태·수명 관리
- **Source**: Screen/View를 생성하고 반환하는 프로젝트 코드

일반 기능은 `UIContext`의 상위 API에서 시작합니다. Child UI World, Scene-authored Layer, backend 확장처럼 조립 제어가 필요한 경우에만 하위 계약을 사용합니다.

## 상세 명세

- [전체 구조](architecture/overview.md): Runtime/Context/Presentation/Window 책임 경계
- [Presentation Architecture](architecture/presentation.md): topology, 순서, backend materialization, lifetime
- [Presentation Composition Contract](architecture/presentation-composition.md): Target·Layer Lease·Host·Scene-authored 조립 계약

게임 데이터, Window View 디자인, 디스크 직렬화와 Unity 자체 렌더링은 Xeri UI가 대신 소유하지 않습니다. 범용 Drag/Drop·Picker 기능은 기반 패키지 `com.inonego.xeri`의 영역입니다.
