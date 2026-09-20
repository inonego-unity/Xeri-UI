# Xeri UI Core

Xeri UI Core는 UGUI와 UI Toolkit으로 만든 게임 화면을 같은 Runtime에서 운용하기 위한 UI 프레임워크입니다. Layer, Screen Stack, Modal, Focus, Input, Transition과 표시 객체의 수명을 공통 계약으로 관리합니다.

## 왜 필요한가

화면마다 직접 Canvas/PanelRenderer를 열고 닫으면서 Focus, Gameplay Input 차단, Modal, Transition과 View 반환까지 따로 처리하면 화면 전환 규칙이 분산됩니다. UI Core는 표시 backend와 무관하게 Screen/Modal/Presentation의 수명과 입력·포커스 경계를 공통 Runtime에서 관리합니다.

## 언제 사용하는가

- 여러 Screen을 Stack으로 열고 닫거나 교체해야 할 때
- Modal 또는 transient Presentation이 Screen과 다른 수명을 가져야 할 때
- UGUI와 UI Toolkit을 같은 Layer/Profile 체계에서 운용할 때
- UI가 열릴 때 Gameplay Input과 Focus를 일관되게 전환해야 할 때
- Scene Fade, Transition, world projection 같은 표시 기능을 공통 서비스로 사용할 때

단순한 고정 HUD 하나만 표시하고 별도 화면 전환 수명이 없다면 전체 UI Core Runtime을 사용하지 않아도 됩니다.

## 기본 사용

Bootstrapper 설정이 끝난 뒤 일반 진입점은 `UIRuntime.Current`와 Main Context입니다.

```csharp
UIRuntime runtime = UIRuntime.Current;
UIContext context = runtime.Main;
```

이후 실제 화면은 Profile을 획득하고 `IScreenSource`를 등록한 다음 Context의 Screen Controller를 통해 엽니다. 설정 절차는 [설정과 시작](../../Documentation~/modules/core/setup.md), Screen 구현은 [Screen과 입력](../../Documentation~/modules/core/screens.md)을 참고합니다.

## 개요

일반적인 사용 흐름은 `UIRuntime`과 `UIContext`를 중심으로 구성됩니다.

```text
UISettingsAsset
    ↓
UIBootstrapperModuleAsset
    ↓
UIRuntime.Current
    ↓
UIRuntime.Main
    ↓
Profile / Screen Registration / Session
```

## 책임 범위

### 담당하는 것

- UGUI와 UI Toolkit이 공유하는 Layer order와 profile 수명
- Screen Stack과 Screen Session lifecycle
- Modal, Overlay, Visibility와 Scene Fade
- Context별 Focus 기록과 UI/Gameplay Input 상태 합성
- Transition, world projection, placement, spotlight와 interaction blocker

### 담당하지 않는 것

- 프로젝트 화면의 도메인 데이터와 명령
- 프로젝트별 화면 디자인과 navigation 의미
- Gameplay input action 자체의 정의
- World Space/Camera UI 전체를 하나의 공통 Layer 정책으로 강제하는 것

## 핵심 개념

| 개념 | 역할 |
|---|---|
| `UIRuntime` | App 범위 UI composition root와 전역 서비스 |
| `UIContext` | Layer, Screen, Modal, Focus를 묶는 표시 범위 |
| `UIProfileHandle` | Profile Layer의 획득 수명 |
| `ScreenRegistrationHandle` | Screen 등록 수명 |
| `ScreenSession` | 열린 Screen의 View, Input, Layer usage와 자식 수명 |

## 상세 문서

- [설정과 시작](../../Documentation~/modules/core/setup.md)
- [구조와 수명](../../Documentation~/modules/core/architecture.md)
- [Screen과 입력](../../Documentation~/modules/core/screens.md)
- [표시와 배치](../../Documentation~/modules/core/presentation.md)
- [구현과 문제 해결](../../Documentation~/modules/core/troubleshooting.md)

## 확장 지점

대표적인 공개 계약은 `IPresentationLayerDriver<TRoot>`, `IScreenSource`, `IScreenDriver`, `IScreenStateHandler`, `IFocusDriver`, `IScreenInputDriver`, `IPresentationTransitioner`, `IPresentationSource<TView>`입니다.

새 구현을 추가하기 전에 기존 계약으로 책임을 표현할 수 있는지 먼저 확인합니다.

## 관련 문서

- [Xeri UI Manual](../../Documentation~/index.md)
- [Xeri 소유권과 수명](https://inonego-unity.github.io/Xeri/docs/concepts/ownership-and-lifetime.html)
- [Xeri 확장 계약](https://inonego-unity.github.io/Xeri/docs/concepts/extension-contracts.html)
