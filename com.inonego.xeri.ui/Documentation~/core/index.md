# Xeri UI Core

Core는 Xeri UI의 application-level Presentation, Screen, Modal, Focus와 Input lifecycle을 제공합니다.

## 시작점

```csharp
UIRuntime runtime = UIRuntime.Current;
UIContext context = runtime.Main;
```

일반 기능 코드는 `UIContext`에서 시작합니다.

## Common API

### Screen

```csharp
ScreenRegistrationHandle registration =
    context.RegisterScreen(
        new ScreenOptions("Settings"),
        source);

ScreenOpenResponse response =
    context.Screens.Open("Settings");
```

### Transient Presentation

```csharp
Lease<VisualElement> lease =
    context.AcquirePresentation(
        "Gameplay.Tooltip",
        source);
```

### UITK Modal

```csharp
ModalSession modal = UITKModal.Open(
    context,
    "Global.Modal",
    modalRoot);
```

Focus trap이 필요하면 `UITKModal.OpenWithFocus(...)`를 사용합니다.

## Runtime 구조

```text
UIRuntime
├─ Root PresentationSession
├─ Main UIContext
├─ SceneFader
├─ Focus backend
└─ Input backend
```

`UISettingsAsset.DefaultLayout`이 Root Presentation topology를 정의합니다.

Top-level Layer는 generated output 또는 explicit `PresentationLayerHost`로 materialize됩니다.

## 책임

Core가 소유하는 것:

- Layout/Plan 기반 Presentation topology
- LayerOrder / LocalOrder
- generated/Scene-authored Layer와 Placement lifetime
- Screen Stack과 `ScreenSession`
- Modal Stack과 `ModalSession`
- Focus memory와 Context authority
- Gameplay Input/Cursor policy
- Presentation transition과 Scene Fade
- Child `PresentationSession` / Child `UIContext`

Core가 소유하지 않는 것:

- 프로젝트 도메인 데이터
- Unity renderer/event system 자체
- 프로젝트별 화면 디자인
- Window instance z-order
- Scene global implicit discovery

## Composition API

일반 façade 아래의 primitive도 supported API입니다.

- `ScreenRegistry`
- `PresentationLease`
- `ModalController`
- Focus Scope / Context Authority
- `PresentationSession.CreateChild`
- `PresentationLayerHost`
- `PresentationPlacementHost`

세밀한 제어는 이 primitive를 직접 조립하는 방식으로 제공하며, Layout/Plan topology나 Session lifetime을 우회하는 mutable backdoor는 제공하지 않습니다.

## 문서

- [Screen과 입력](screens.md)
- [Presentation](presentation.md)
- [문제 해결](troubleshooting.md)
- [Presentation Architecture](../architecture/presentation.md)
