# Core

Xeri UI Core는 **Screen·Modal·Presentation의 표시 수명, Focus, Gameplay Input과 Cursor 정책**을 함께 관리합니다. 일반 기능은 `UIRuntime.Current`에서 얻은 `UIContext`의 API를 사용합니다.

## 시작점

`UIRuntime`이 초기화된 뒤 다음 객체를 가져옵니다.

```csharp
UIRuntime runtime = UIRuntime.Current;
UIContext context = runtime.Main;
```

`Main`은 애플리케이션의 Root Context이고, Application Window는 자체 Child Context를 가질 수 있습니다. Runtime과 Main Context의 전체 수명은 Bootstrapper 또는 Scene의 composition owner가 관리합니다.

## 기능별 진입점

| 기능 | 일반 진입점 | 반환·종료 경계 |
| --- | --- | --- |
| Screen 등록 | `context.RegisterScreen(options, target, source)` | `ScreenRegistrationHandle` |
| Screen 열기 | `context.Screens.Open(id)` | `ScreenSession` 또는 Controller 명령 |
| Modal 표시 | `UITKModal.Open(context, target, root)` | `ModalSession` |
| 임시 Presentation | `context.AcquirePresentation(target, source)` | 반환된 `Lease` |
| Scene Fade | `runtime.SceneFader.Cover/Reveal(...)` | Runtime-owned `SceneFader` |
| Splash | `UITKSplash.Show(...)` / `UGUISplash.Show(...)` | `SplashSession` |
| Spotlight | `UITKSpotlight.Show(...)` / `UGUISpotlight.Show(...)` | 표시 요청 `Lease` |
| Window | `new XeriWindowWorkspace(context, target)` | Workspace/Window Session |

## Screen

Screen을 등록할 때 **고유한 Screen ID**와 **표시 Target**을 별도로 지정합니다.

```csharp
ScreenRegistrationHandle registration = context.RegisterScreen(
    new ScreenOptions("Settings"),
    PresentationTarget.Host(PresentationDestinationID.Application),
    source);

ScreenOpenResponse response = context.Screens.Open("Settings");
```

`source`는 프로젝트가 구현한 `IScreenSource`입니다. Registration Handle은 신규 Open 가능 기간을, Screen Session은 실제 열린 UI의 수명을 가집니다. 세부 내용은 [Screen과 입력](screens.md)을 참고합니다.

## Transient Presentation과 Modal

일반 Presentation은 Source의 생성/반환과 Layer 사용 수명을 결합합니다.

```csharp
Lease<VisualElement> lease = context.AcquirePresentation(
    PresentationTarget.Host(PresentationDestinationID.Overlay),
    source);
```

Modal에는 같은 방식으로 표시 Target을 지정하되, 별도의 Modal 수명과 입력·Focus 정책을 사용합니다.

```csharp
ModalSession modal = UITKModal.Open(
    context,
    PresentationTarget.Host(PresentationDestinationID.Modal),
    modalRoot);
```

`source`와 `modalRoot`는 프로젝트에서 준비합니다. Focus trap이 필요하면 `UITKModal.OpenWithFocus(...)`를 사용합니다. 세부 내용은 [Presentation](presentation.md)에 있습니다.

## Target, Session과 소유권

```text
UIRuntime
├─ Root PresentationSession
├─ PresentationHost (semantic destination → Root Layer)
├─ Main UIContext (Screen / Modal / Focus / Input)
└─ SceneFader

UIContext
└─ PresentationTarget.Host(...) 또는 Local(...)
   └─ Layer Lease 획득
      └─ Source/View 사용
```

- `PresentationTarget.Host(id)`: `UISettingsAsset.DestinationDefinitions`에 있는 app-wide Destination을 Root Layer로 해석합니다.
- `PresentationTarget.Local(layerID)`: 현재 Context의 Presentation Session 안에서 Layer를 찾습니다.
- `PresentationLayerLease`: 찾은 Layer의 **소비자 사용 수명**을 관리합니다.
- `PresentationSession`: immutable Plan을 materialize한 Local UI World의 수명을 소유합니다.

Presentation Target은 표시 위치의 **요청 값**이지 실제 Layer나 소유권 객체가 아닙니다. `AcquireLayer`로 얻은 Lease를 반환하지 않으면 해당 Layer Session을 안전하게 종료할 수 없습니다.

## 직접 조립이 필요한 경우

기능 API만으로 충분하지 않은 프로젝트는 다음 Composition API를 사용할 수 있습니다.

- `ScreenRegistry`, `ModalController`
- `UIContext.AcquireLayer(PresentationTarget)`
- `PresentationSession.AcquireLayer(layerID)`
- `PresentationHost.AcquireLayer(destinationID)`
- `PresentationSession.CreateChild`
- `PresentationLayerHost`, `PresentationPlacementHost`
- Focus Scope와 Context authority composition

이 API들은 활성 Plan을 변경하거나 Runtime-owned Root Session을 직접 Dispose하는 경로가 아닙니다. 프로젝트가 **자신이 획득·생성한 소비자 수명**만 반환합니다.

## 관련 문서

- [Screen과 입력](screens.md)
- [Presentation과 Modal](presentation.md)
- [Splash](splash.md)
- [Spotlight](spotlight.md)
- [문제 해결](troubleshooting.md)
- [Presentation Architecture](../architecture/presentation.md)
- [Presentation Composition Contract](../architecture/presentation-composition.md)
