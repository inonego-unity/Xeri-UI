# 설정과 시작

Xeri UI Runtime은 `PresentationLayout`, `UISettingsAsset`, Runtime Host와 Bootstrapper를 조합해 시작합니다.

## 1. PresentationLayout

`Assets > Create > Xeri > UI > Presentation Layout`에서 Root Layout을 만듭니다.

Layer는 top-level presentation domain을 정의합니다.

- `LayerID`: Session 안의 stable ID
- `DisplayName`: authoring 이름
- `LayerOrder`: 같은 Layout 안 Layer ordering
- `Backend`: Layer를 materialize할 UI backend

Placement는 Scene-authored/static placement root처럼 **Session topology에 고정되어야 하는** 위치만 정의합니다.

- `PresentationID`: Session-local authored placement ID
- `LayerID`: 소속 Layer
- `LocalOrder`: 같은 Layer 안 authored placement 순서

일반 Screen/Modal/Window/Fade 같은 transient UI는 Placement를 미리 만들지 않고 `PresentationTarget`으로 Layer 또는 Host destination을 resolve합니다.

예:

```text
Layers
├─ Application    order 100   UITK
├─ Overlay        order 300   UITK
├─ Modal          order 400   UITK
└─ SystemFade     order 10000 UGUI

Static Placements
└─ Desktop.Taskbar → Application / local 30
```

## 2. UISettingsAsset

`Assets > Create > Xeri > UI > Settings`에서 Settings를 만듭니다.

| 필드 | 의미 |
|---|---|
| `DefaultLayout` | Runtime lifetime 동안 사용할 Root Layout |
| `UITKPanelSettingsTemplate` | generated UITK Layer의 PanelSettings template |
| `UGUIOutputTemplate` | generated UGUI output template |
| `Presentation Destinations` (`DestinationDefinitions`) | app-wide semantic destination → Root Layer 매핑 |
| `BackendSupport` | Runtime bootstrap에서 지원할 UITK/UGUI capability |
| `DefaultFadeColor` | 기본 Fade 색 |
| `DefaultFadeDuration` | 기본 Fade 시간 |
| `UIActionsAsset` / `UIActionMap` | UI input action source |
| `GameplayActionsAsset` / `GameplayActionMap` | gameplay input policy source |
| `ReleaseActionNames` | Screen 종료 뒤 release barrier가 관찰할 action |

UI Action Map과 Gameplay Action Map은 서로 다른 map을 사용합니다.

## 3. Runtime Host

기본 Host prefab:

```text
Runtime/Core/Assets/Host/UIHost.prefab
```

`UIRuntime`은 Host 안에서 다음 경계를 사용합니다.

- Presentation parent root
- Focus Driver
- Screen Input Driver
- Scene Fade Source
- optional explicit `PresentationLayerHost[]`

### UITK-only

`BackendSupport = UITK`이면 Scene `EventSystem`과 `InputSystemUIInputModule`을 필수로 요구하지 않습니다.

### UGUI mixed

`BackendSupport`에 UGUI가 포함되면 같은 Host의 `EventSystem + InputSystemUIInputModule`을 mixed input path로 검증합니다.

지원 capability는 Runtime bootstrap에서 고정되며 현재 active Layout의 Layer만 보고 추론하지 않습니다.

## 4. Scene-authored Layer

Scene hierarchy 자체를 presentation output으로 사용할 Layer만 explicit Host를 연결합니다.

- UGUI: `UGUIPresentationLayerHost`
- UITK: `UITKPresentationLayerHost` + `PanelRenderer` + `VisualElementReference`

`PresentationLayerHost`는 `LayerID`, Native Output, `LayerRoot`, `ManagedRoot`의 physical boundary를 제공합니다.

`ManagedRoot`는 LayerRoot 아래의 distinct descendant subtree입니다. authored static content는 ManagedRoot 밖에 둘 수 있습니다.

특정 placement root까지 Scene에서 authoring할 때만 `PresentationPlacementHost`를 사용합니다. PlacementHost는 `PresentationID`와 physical root만 제공하며 LayerID/LocalOrder를 변경하지 않습니다.

Host는 `UIRuntime` composition에서 명시적으로 연결합니다. Scene 전체를 ID로 검색하지 않습니다.

## 5. Bootstrapper

`Assets > Create > Xeri > Bootstrapper > UI Module`에서 `UIBootstrapperModuleAsset`을 만듭니다.

1. `Host Prefab`을 지정합니다.
2. `Settings`에 `UISettingsAsset`을 지정합니다.
3. Module을 Xeri Bootstrapper Settings에 등록합니다.

Module은 Host를 생성하고 `UIRuntime.Initialize(settings)`를 호출합니다.

## 6. Runtime 사용

```csharp
UIRuntime runtime = UIRuntime.Current;
UIContext context = runtime.Main;
```

이후 일반 기능은 `UIContext`의 Tier 1 Feature API에서 시작합니다.

```csharp
context.RegisterScreen(options, target, source);
context.Screens.Open(...);
context.AcquirePresentation(target, source);
UITKModal.Open(context, target, root);
```

세부 구조는 [Core 개요](../core/index.md)와 [Presentation Architecture](../architecture/presentation.md)를 참고합니다.
