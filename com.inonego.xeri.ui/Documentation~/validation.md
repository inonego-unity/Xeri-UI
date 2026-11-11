# Validation Desktop

Validation Desktop은 Xeri UI의 **실제 기능을 자유롭게 조작하는 통합 검증 Scene**입니다. 순서가 정해진 체크리스트나 자동 테스트 실행 UI가 아니며, Window·Screen·Modal·Tray·Input 등 기능을 사용자가 원하는 순서대로 확인할 수 있는 작은 데스크톱 환경입니다.

## Scene 위치와 실행

```text
Packages/com.inonego.xeri.ui/Validation/Scenes/XeriUIValidation.unity
```

1. [설치](getting-started/installation.md)에 따라 패키지, 기반 Xeri와 DOTween 의존성을 준비합니다.
2. Unity 프로젝트의 [Bootstrapper 시작 설정](getting-started/setup.md#4-unixeri-bootstrapper-연결)에서 **Bootstrapper Scene**과 **최초 Application Scene**을 지정합니다. 이 Scene으로 검증하려면 최초 Application Scene에 위 Validation Scene을 등록합니다.
3. Unity Editor에서 Validation Scene을 열어 Play Mode로 진입합니다.
4. Desktop의 앱과 Lab을 원하는 순서대로 열어 직접 조작합니다.

이 Scene에는 자체 `UIRuntime` 조립이 포함돼 있습니다. 최초 Scene에 Scene-owned Runtime이 있으면 `UIBootstrapperModuleAsset`은 별도의 Host를 중복 생성하지 않습니다. **패키지 Scene만 열려 있다고 해서 소비 Unity 프로젝트의 Bootstrapper Settings와 Build Scenes까지 자동 구성되는 것은 아닙니다.**

## 데스크톱에서 사용할 수 있는 항목

| 항목 | 사용할 수 있는 동작 |
| --- | --- |
| **Task Board** | 작업 항목 UI의 일반적인 입력·상태 변화 |
| **Preferences** | 설정 UI와 Window 간 상호작용 |
| **Application A/B** | 서로 독립된 Child Screen·Modal·Focus 상태 |
| **Core Lab** | Root Screen, transient Presentation, Spotlight, Scene Fade, UITK Splash |
| **Window Lab** | 이동·Resize·최소화·복원·최대화·닫기와 Tray 상태별 표시 |
| **Modal Lab** | Root Modal 중첩, 열기·닫기와 Focus 복귀 |
| **System Monitor** | Screen·Modal·Window·Input 상태 관찰 |

Desktop은 임의의 순서로 사용해도 됩니다. 예를 들어 Application A와 B를 동시에 열고 각각 Screen을 조작하거나, 창을 최소화한 뒤 Tray에서 복원하거나, Core Lab에서 Fade·Splash를 반복 실행할 수 있습니다.

## 화면과 상태 관찰 계약

- **Window**: 제목 표시줄 이동, 가장자리 Resize, 활성화, 최소화·최대화·복원·종료가 Workspace와 실제 Window Controller를 거칩니다.
- **Tray/Taskbar**: `XeriWindowTraySource → XeriTrayController → XeriTrayPanel` 경로로 Window 상태를 표시합니다. `Normal`·`Maximized`는 보이는 창, `Minimized`는 숨겨진 창 상태로 반영됩니다.
- **Application A/B**: 같은 Child Layout과 Screen ID를 사용할 수 있지만 서로 다른 `UIContext`와 `PresentationSession`을 소유합니다.
- **Core Lab**: Root Context의 Overlay·System 등 Destination을 사용하며, Splash는 Skip/Cancel/완료 수명을 확인할 수 있습니다.
- **System Monitor**: 변경 이벤트로 현재 상태를 보여줍니다. 접힌 상태에서는 표시 갱신을 생략하며 주기적으로 모든 Tray를 조회하지 않습니다.

`Explored` 표시나 UI에 보이는 성공 상태는 **사용한 동작의 기록**입니다. 자동 테스트 전체 통과, 모든 플랫폼 입력 차단이나 실제 GPU 합성의 증명이 아닙니다.

## Presentation 구성

| Layer / 경계 | 구성 |
| --- | --- |
| Desktop | Scene-authored UITK Layer Host |
| Window / Overlay / Modal | generated UITK Layer |
| System Fade | generated UGUI Layer, `System` Destination |
| Application A/B 콘텐츠 | Window ContentRoot 아래 Child PresentationSession |
| Core Lab의 Splash | 명시적으로 매핑된 UITK Splash Destination |

Application Window의 Screen/Modal은 Child **local Layer**를, Root Core Lab의 글로벌 Overlay는 Root Context의 **Host Destination**을 사용합니다. Child Context의 글로벌 Host 조합, 실패 주입에 따른 rollback과 borrowed native state 반환은 별도 자동 테스트가 다루는 계약입니다.

## 외관과 레이아웃

Validation PanelSettings는 1920×1080을 기준으로 중앙 16:9 작업 공간을 구성합니다. 상단 시스템 바와 하단 Taskbar 사이가 Window 작업 영역이며 최대화 창은 이 영역을 사용합니다.

Window와 Tray는 자체 Validation용 복제품이 아니라 Xeri UI의 실제 런타임 경로를 사용합니다. 프로젝트에서 Window·UITK control의 공통 외관을 재사용하려면 `Runtime/Resources/XeriUI/Theme/XeriDesktopTheme.uss`와 `xeri-desktop-theme` 클래스를 사용할 수 있습니다. `--light`와 `--compact` 변형은 각각 밝은 외관과 조밀한 간격을 위한 modifier입니다.

배경 Gradient는 Unity Native 렌더링을 사용하며 브라우저 CSS와 픽셀 단위의 색상 일치를 보장하지 않습니다.

## 검증 범위의 한계

Validation Desktop은 **수동 통합 사용 환경**입니다. 생성·해제 실패를 강제로 주입하는 테스트, Headless 환경, UITK-only Bootstrapper, GPU 픽셀 차이, 실기기 Pointer 및 모든 해상도 조합을 대체하지 않습니다. 관련 구현의 자동 테스트는 패키지의 `Runtime/**/TEST/EDIT` 및 `Runtime/**/TEST/PLAY`에 있습니다.

Play 진입 오류가 나면 [문제 해결](core/troubleshooting.md)을 참고합니다.
