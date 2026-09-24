# Xeri UI Core

Core는 Xeri UI의 application-level Presentation, Screen, Modal, Focus와 Input lifecycle을 제공합니다.

## 시작점

```csharp
UIRuntime runtime = UIRuntime.Current;
UIContext context = runtime.Main;
```

일반 기능 코드는 `UIContext`에서 시작합니다.

## Tier 1 — Feature / Golden Path

### Screen

```csharp
ScreenRegistrationHandle registration =
    context.RegisterScreen(
        new ScreenOptions("Settings"),
        PresentationTarget.Host(PresentationDestinationID.Application),
        source);

ScreenOpenResponse response =
    context.Screens.Open("Settings");
```

### Transient Presentation

```csharp
Lease<VisualElement> lease =
    context.AcquirePresentation(
        PresentationTarget.Host(PresentationDestinationID.Overlay),
        source);
```

### UITK Modal

```csharp
ModalSession modal = UITKModal.Open(
    context,
    PresentationTarget.Host(PresentationDestinationID.Modal),
    modalRoot);
```

Focus trap이 필요하면 `UITKModal.OpenWithFocus(...)`를 사용합니다.

## Runtime 구조

```text
UIRuntime
├─ Root PresentationSession          // immutable local topology
├─ PresentationHost                  // app-wide destination resolver
│  ├─ Application
│  ├─ Overlay
│  ├─ Modal
│  └─ System
├─ Main UIContext
├─ SceneFader
├─ Focus backend
└─ Input backend

UIContext
└─ PresentationTarget
   └─ AcquirePresentation(...)        // Golden Path
      └─ internal Layer acquisition

Project Composition
└─ AcquireLayer(...)
   └─ PresentationLayerLease
      └─ resolved Layer consumer
```

현재 `UISettingsAsset.DefaultLayout`이 Root Presentation topology를 정의합니다.

Top-level Layer는 generated output 또는 explicit `PresentationLayerHost`로 materialize됩니다.

DefaultLayout은 local Layer/authoring topology를 정의하고, transient/system 표시 위치는
`UISettingsAsset.DestinationDefinitions`의 semantic destination mapping으로 분리합니다.
Target 해석과 Layer Lease의 세부 책임은 [Presentation Composition Contract](../architecture/presentation-composition.md)을 참고합니다.

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

## Tier 2 — Project Composition

일반 façade 아래의 primitive도 supported API입니다.

- `ScreenRegistry`
- `UIContext.AcquireLayer(PresentationTarget)`
- `PresentationSession.AcquireLayer(layerID)`
- `PresentationHost.AcquireLayer(destinationID)`
- `PresentationLayerLease`
- static authored `UIContext.AcquirePlacement`
- `ModalController`
- Focus Scope / Context Authority
- `PresentationSession.CreateChild`
- `PresentationLayerHost`
- `PresentationPlacementHost`

세밀한 제어는 이 primitive를 직접 조립하는 방식으로 제공하며, Layout/Plan topology나 Session lifetime을 우회하는 mutable backdoor는 제공하지 않습니다. `UIRuntime`/`UIContext`가 노출하는 Root Session, SceneFader, ScreenRegistry, ModalController는 조회·명령 surface일 뿐 owner lifetime은 Runtime/Context에 남습니다.

Layer 획득은 활성 Plan을 mutate하지 않습니다. `PresentationTarget.Host(...)`는 Root Host destination을,
`PresentationTarget.Local(...)`은 현재 Session Layer를 가리키며 `AcquireLayer(...)`가 반환한
`PresentationLayerLease`가 resolved Layer consumer lifetime을 소유합니다.

## 문서

- [Screen과 입력](screens.md)
- [Presentation](presentation.md)
- [문제 해결](troubleshooting.md)
- [Presentation Architecture](../architecture/presentation.md)
- [Presentation Composition Contract](../architecture/presentation-composition.md)
