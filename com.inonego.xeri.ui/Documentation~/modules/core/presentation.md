# UI Core 표시와 배치

## 목적

Screen Stack 바깥에서 사용하는 Scene Fade, Overlay, Modal, Visibility, Tracking, Projection, Spotlight와 UI Toolkit 표현 기능을 어떤 소유권으로 조립해야 하는지 설명합니다.

## 언제 읽는가

- Screen과 독립된 Overlay/Modal을 표시할 때
- 월드 위치를 UGUI/UITK 화면 좌표로 추적할 때
- Safe Area, Spotlight, Pointer 차단을 Screen 수명에 연결할 때
- UI Toolkit Gradient/Gamma 표현을 구성할 때

## 표시 기능

### Scene Fade

`SceneFader`는 Runtime 전체 화면 전환을 담당한다. Context별 UI Fade나 Window Fade가 아니다.

```csharp
var fade = new SceneFadeParams(Color.black, 0.25f);

runtime.SceneFader.Cover(fade, HandleCovered, HandleFadeFailure);
runtime.SceneFader.Reveal(fade, HandleRevealed, HandleFadeFailure);
```

`Cover` 완료 뒤에는 불투명 View를 유지하고 `Reveal` 완료 뒤 반환한다. 새 요청은 기존 Fade를
취소하며 실패는 요청 Callback과 `LastFailure`로 확인한다.

### Overlay

Overlay는 Screen Stack과 독립적으로 Layer를 점유하는 임시 View다. Core에는 Overlay 전용 타입을 두지 않고,
`IPresentationSource<TView>`와 `PresentationLease`로 획득과 반환을 표현한다.

```csharp
Lease<ExampleView> overlay = PresentationLease.Acquire<ExampleView>
(
    context.LayerRegistry,
    "Overlay",
    source
);
```

`IPresentationSource<TView>`가 View의 획득과 반환을 소유한다. Screen에 종속되면 Session의 자식으로
등록하고, 독립 수명이면 반환된 Lease를 해당 소유자가 직접 해제한다.

### Modal

`ModalController`는 View를 생성하지 않는다. Modal Stack과 top 상호작용 상태만 관리한다.

- UGUI: `UGUIModalInteractionDriver`
- UI Toolkit: `UITKModalInteractionDriver`

Modal View의 표시 상태와 상호작용 정책은 분리한다. `IPresentation`이 표시 상태를 제공하고,
`IModalInteractionDriver`가 top 여부에 따른 상호작용만 제어한다. View 제거, Layer Usage 같은
추가 수명은 `ModalSession`에 함께 넘길 수 있다.

```csharp
var presentation = new UITKPresentation(modalRoot);
var interaction = new UITKModalInteractionDriver(modalRoot);

ModalSession modal = context.Modals.Open
(
    presentation,
    interaction,
    visualHandle
);
```

`ModalSession`을 해제하면 현재 항목을 닫고 이전 Modal을 top으로 복원한 뒤 전달받은 소유 수명을
역순으로 해제한다.

### Presentation State와 Composite Tree

`PresentationAlpha`와 `PresentationVisibility`는 기존 `MValue<T>`를 직접 사용한다.
각 State의 `Base → Modifier pipeline → Modified`는 local state이며 Tree topology를 저장하지 않는다.

```text
Local Alpha
Base → Modifiers → Modified

Local Visibility
Base → Modifiers → Modified
```

`PresentationGroup`은 State graph가 아니라 `IPresentation` Member Tree만 소유한다.
Alpha와 Visibility의 identity 값은 각각 `1.0f`, `true`다.

```csharp
var group = new PresentationGroup
(
    new[]
    {
        first,
        second,
    }
);

group.Alpha.Set(0.5f);
group.Visibility.Set(true);

group.Alpha.AddModifier
(
    "fade",
    new NumericFModifier(NumericFOperation.MUL, 0.5f)
);

group.Apply();
```

`Apply()`는 선택한 Group을 Root로 현재 Tree를 한 번 평가하는 명시적 operation이다.
Group은 reactive binding이나 active parent 관계를 만들지 않으며 parent 누적값을 child에 전달한다.

```text
nextAlpha      = parentAlpha × current.Alpha.Modified
nextVisibility = parentVisibility AND current.Visibility.Modified
```

leaf에 도착하면 누적값과 leaf local `Modified`를 합성해 backend에 기록한다.
Group은 child의 `Base`, `Modified`, Modifier 목록을 변경하지 않는다.

같은 Presentation이나 Group은 서로 다른 Composite Tree에 여러 번 포함될 수 있다.
각 `Apply()`는 호출한 Tree만 기준으로 계산하므로 backend 결과는 마지막으로 적용한 Tree에 의해 결정된다.
반대로 하나의 Apply Tree 안에서 같은 Presentation reference가 두 경로에 중복되면 Tree가 아니므로 거부한다.

`Add`, `Remove`, `Clear`는 topology만 변경하며 backend를 자동 갱신하거나 복원하지 않는다.
또한 `Apply()` 뒤 leaf의 local `Base`나 Modifier가 바뀌면 그 leaf는 자기 local `Modified`를 backend에 다시 적용할 수 있다.
이전 Tree 결과를 계속 유지해야 하는 runtime 수명은 소유자가 필요한 시점에 같은 Tree를 다시 `Apply()`한다.
복원이 필요한 runtime 수명은 identity State를 설정한 뒤 `Apply()`하고 topology를 해제한다.

Modifier 자체의 등록과 제거는 기존 `MValue<T>.AddModifier/RemoveModifier`를 그대로 사용하며
Presentation 전용 Modifier host나 registration Lease를 추가하지 않는다.

### Drag Visual

`DragVisualController`는 Xeri `Drag_Drop`의 `DraggableUI`와 연결한다.

```csharp
var dragVisuals = new DragVisualController(context.LayerRegistry);
IDisposable binding = dragVisuals.Bind
(
    draggable,
    new DragVisualParams(target, "Drag")
);
```

Drag 중 Target을 지정 Layer로 옮기고 종료 시 부모, sibling과 Transform을 복원한다. Drag 판정과
Drop 계약은 [Drag_Drop README](https://github.com/inonego-unity/Xeri/tree/main/com.inonego.xeri/Runtime/UI/Drag_Drop)를 따른다.

## 배치와 입력 보조 기능

| 역할 | UGUI | UI Toolkit | 결과 수명 |
|---|---|---|---|
| World 위치를 Root 좌표로 변환 | `UGUIWorldProjector` | `UITKWorldProjector` | 값 |
| Root 안에서 요소 배치 | `PlacementSolver` + `YUp` | `PlacementSolver` + `YDown` | 값 |
| Safe Area 적용 | `UGUISafeAreaLayout` | `UITKSafeAreaLayout` | Component |
| dim과 입력 통과 구멍 | `UGUISpotlight` | `UITKSpotlight` | `Lease` |
| 명시적 Pointer 입력 차단 | `UGUIInteractionBlocker` | `UITKInteractionBlocker` | `Lease` |

이 기능은 Runtime이 자동 생성하지 않는다. 사용하는 Screen, Presenter 또는 조립 객체가 만들고
반환된 Lease와 Component를 자신의 수명 안에서 종료한다.

### World Projection과 Placement

Projector 결과와 `PlacementSolver`의 Bounds는 같은 UI Root 로컬 좌표여야 한다. UGUI는
`RectTransform`의 Y-up, UI Toolkit은 `VisualElement`의 Y-down 좌표를 사용한다.

```csharp
var projector = new UITKWorldProjector();
ProjectionResult projected = projector.Project(worldPosition, camera, layerRoot);

if (projected.Succeeded && !projected.IsBehindCamera)
{
    var solver = new PlacementSolver();
    PlacementResult placed = solver.Place
    (
        layerRoot.contentRect,
        projected.LocalPosition,
        element.layout.size,
        Vector2.zero,
        new PlacementOptions
        (
            PlacementAlignment.Bottom,
            new Vector2(0.0f, 12.0f),
            new Vector2(16.0f, 16.0f),
            coordinateSystem: PlacementCoordinateSystem.YDown
        )
    );

    element.style.left = placed.LocalPosition.x;
    element.style.top = placed.LocalPosition.y;
}
```

`left`와 `top`을 적용할 때는 Pivot으로 `Vector2.zero`를 사용한다. UGUI는 Projector가 반환한
Root 로컬 좌표를 `RectTransform.localPosition`에 적용하고, 요소
크기 계산에는 실제 `RectTransform.pivot`과 `YUp`을 사용한다. `anchoredPosition`을 사용하려면
Marker의 고정 Anchor가 부모 Pivot과 일치해야 한다. Solver 호출만 감싸는 Backend별 Popup
타입은 제공하지 않는다.

### 연속 Tracking

매 Frame 같은 관계를 갱신해야 할 때는 `TrackingRunner`에 `TrackingBinding<T>`를 등록한다.
Binding은 다음 세 단계를 하나로 묶는다.

1. `resolve`: 현재 원하는 값을 조회한다. 대상이 없으면 `Available`을 `false`로 반환한다.
2. `transition`: 이전 실제 적용값에서 새 값으로 이동한다. 즉시 반영이면 생략한다.
3. `commit`: 값을 실제 대상에 적용하고, 적용된 최종값을 반환한다.

등록 결과는 별도 Tracking Handle이 아니라 공통 `Lease`다. Marker, Screen 또는 Presenter를
소유한 범위가 이 Lease를 보관하고 종료한다. `TrackingRunner`는 필요한 Scene 범위에만 명시적으로
배치하며 `UIRuntime`이 자동 생성하거나 소유하지 않는다.

UGUI World Marker는 Projection, Safe Rect 배치와 보간을 다음처럼 조립한다. 아래 예시는
Marker가 현재 `screenSession`에 속하며 해당 Screen이 Tracking Lease를 소유한다고 가정한다.

```csharp
var projector = new UGUIWorldProjector();
var solver = new PlacementSolver();
var velocity = Vector2.zero;

var binding = new TrackingBinding<Vector2>
(
    resolve: () =>
    {
        if (target == null) return (false, default);

        ProjectionResult projected = projector.Project
        (
            target.position,
            camera,
            layerRoot
        );

        return
        (
            projected.Succeeded && !projected.IsBehindCamera,
            projected.LocalPosition
        );
    },
    transition: (current, desired, deltaTime) => Vector2.SmoothDamp
    (
        current,
        desired,
        ref velocity,
        0.12f,
        Mathf.Infinity,
        deltaTime
    ),
    commit: position =>
    {
        PlacementResult placed = solver.Place
        (
            layerRoot.rect,
            position,
            marker.rect.size,
            marker.pivot,
            new PlacementOptions
            (
                PlacementAlignment.Bottom,
                new Vector2(0.0f, 12.0f),
                new Vector2(16.0f, 16.0f)
            )
        );

        // Clamp 이후에도 외부 SmoothDamp 속도가 화면 밖 방향으로 누적되지 않게 한다.
        if (placed.WasClamped)
        {
            velocity = Vector2.zero;
        }

        marker.gameObject.SetActive(true);
        marker.localPosition = new Vector3
        (
            placed.LocalPosition.x,
            placed.LocalPosition.y,
            marker.localPosition.z
        );
        return placed.LocalPosition;
    },
    clear: () => marker.gameObject.SetActive(false)
);

Lease trackingLease = trackingRunner.Track(binding);
screenSession.RegisterChild(trackingLease);
```

`commit`이 clamp된 실제 위치를 반환하므로 다음 Frame의 보간도 화면에 표시된 위치에서 이어진다.
화면 뒤 대상을 숨길지, 가장자리 Indicator로 바꿀지는 `IsBehindCamera`를 해석하는 사용처 정책이다.

UI Toolkit에서도 같은 Binding을 사용하고 Backend 경계만 바꾼다.

```csharp
var projector = new UITKWorldProjector();
var solver = new PlacementSolver();

var binding = new TrackingBinding<Vector2>
(
    resolve: () =>
    {
        if (target == null) return (false, default);

        ProjectionResult projected = projector.Project
        (
            target.position,
            camera,
            layerRoot
        );

        return
        (
            projected.Succeeded && !projected.IsBehindCamera,
            projected.LocalPosition
        );
    },
    commit: position =>
    {
        PlacementResult placed = solver.Place
        (
            layerRoot.contentRect,
            position,
            element.layout.size,
            Vector2.zero,
            new PlacementOptions
            (
                PlacementAlignment.Bottom,
                new Vector2(0.0f, 12.0f),
                new Vector2(16.0f, 16.0f),
                coordinateSystem: PlacementCoordinateSystem.YDown
            )
        );

        element.style.display = DisplayStyle.Flex;
        element.style.left = placed.LocalPosition.x;
        element.style.top = placed.LocalPosition.y;
        return placed.LocalPosition;
    },
    clear: () => element.style.display = DisplayStyle.None
);

Lease trackingLease = trackingRunner.Track(binding);
screenSession.RegisterChild(trackingLease);
```

일시 정지와 무관하게 UI 전이를 계속하려면 `trackingRunner.UsesUnscaledTime = true`로 설정한다.
Runner 비활성화는 갱신만 멈추고, Lease 해제 또는 Runner 파괴가 Binding과 마지막 표시 상태를 정리한다.
`RegisterChild`에 전달한 Lease는 Screen이 정상 종료를 소유한다. Screen보다 먼저 Tracking을
끝내야 하면 같은 Lease를 직접 `Dispose()`해도 이후 Screen 종료에서는 다시 처리되지 않는다.
Screen에 속하지 않는 Tracking은 해당 Marker 또는 Presenter가 Lease를 필드로 보관하고 자신의
종료 시점에 `Dispose()`한다.

### Safe Area

`UGUISafeAreaLayout`은 연결한 `RectTransform` 하나에 적용한다. `UITKSafeAreaLayout`은 같은
`UITKLayerPanel` Root의 이름 있는 `VisualElement` 하나에 적용한다. 활성화 시 첫 Layout 전에 반영하고
화면 또는 Panel 크기가 바뀌면 다시 계산한다.

### Spotlight

Spotlight는 Focus 선택 기능이 아니다. 튜토리얼처럼 화면을 dim 처리하고 지정 Target 영역의
입력만 통과시키는 표시 기능이다.

```csharp
var spotlight = new UITKSpotlight();
var spotlightElement = new UITKSpotlightElement();
layerRoot.Add(spotlightElement);

Lease spotlightLease = spotlight.Show
(
    spotlightElement,
    new UITKSpotlightParams
    (
        new[]
        {
            new UITKSpotlightTarget
            (
                target,
                new Vector4(8.0f, 8.0f, 8.0f, 8.0f)
            )
        }
    )
);

screenSession.RegisterChild(spotlightLease);
```

`UITKSpotlightElement`와 Target은 같은 Panel에 연결한다. 유효한 Target이 모두 숨겨지거나
분리되면 dim과 Picking을 함께 비워 전체 입력 잠금을 만들지 않는다. UGUI에서는
`UGUISpotlightDriver`와 `UGUISpotlightParams`를 사용한다.

### Interaction Blocker

`IInteractionBlocker.Acquire()`는 중첩 가능한 Pointer 차단 Lease를 반환한다. 마지막 Lease가
해제될 때만 Blocker가 숨겨진다. UGUI는 연결한 Root와 `CanvasGroup`, UI Toolkit은 생성자에
전달한 전용 `VisualElement`만 변경한다. Keyboard·Gamepad Focus, Gameplay Input, Spotlight의
입력 구멍이나 Modal Stack을 대신하지 않는다.

## UI Toolkit 표현

### Runtime Baseline

`UITKLayerPanel`은 Layer Root에 `UIRuntimeBaseline.uss`를 자동 연결한다. Label, Button,
BaseField와 ProgressBar의 기본 외부 간격을 정규화하되 TextField 입력부, Slider Tracker,
Popup Arrow와 Scroller처럼 동작에 필요한 Unity 내부 구조는 유지한다.

### Native Gradient

UI Core는 gradient shader/material polyfill을 제공하지 않는다. Unity 6000.7이 제공하는
`linear-gradient()`와 `radial-gradient()`는 USS에서 직접 사용한다. `conic-gradient()`처럼
현재 native로 제공되지 않는 표현은 Core가 에뮬레이션하지 않으며, 필요한 프로젝트가 별도
시각 구현을 소유한다.

```css
.example-gradient {
    background-image: linear-gradient(135deg, #5de2ff 0%, #a078ff 100%);
}

.example-glow {
    background-image: radial-gradient(
        circle farthest-corner at 50% 50%,
        rgba(93, 226, 255, 0.56) 0%,
        rgba(93, 226, 255, 0) 100%
    );
}
```

### UI Toolkit 출력 경로

`UITKLayerPanel`은 `PanelRenderer`를 UI Toolkit Screen Overlay backend로 사용한다.
`PanelRenderer.panelSettings`의 Target Texture가 비어 있고 기본 Display를 사용할 때만 공통
Layer Order에 등록하며, `PresentationLayerAsset.Order`는 `PanelRenderer.sortingOrder`에 적용한다.

`UITKLayerPanel.Root`는 `PanelRenderer`가 생성한 실제 Panel Root를 노출한다. Unity 6000.7.0a6에서는
해당 Root getter가 internal이므로 backend 내부에서만 버전 종속 Reflection을 사용한다. 상위 Core와
호출자는 `VisualElement` Root 계약만 사용한다.

Layer 활성화 시 Root에 `xeri-ui` class와 `UIRuntimeBaseline.uss`를 적용한다. Baseline의 Resource key는
`Xeri/UI/Core/UIRuntimeBaseline`이며, Unity Runtime Theme의 기본 Label/Button/BaseField 간격을
CSS 기준본과 맞도록 정규화한다.

별도 RenderTexture, 합성 Document, Gamma compositor 또는 Render Pipeline adapter는 만들지 않는다.
`PanelSettings`도 Runtime clone하지 않고 prefab/호출자가 제공한 인스턴스를 그대로 사용한다.

### 반복 Animation

UI Core는 `@keyframes` 대체용 반복 Animator를 제공하지 않는다. 반복 시각 효과가 필요하면 화면
구현이 UI Toolkit transition event, DOTween 또는 프로젝트 animation 정책으로 소유한다. Core는
Screen/Modal/Presentation lifecycle과 transition 실행 계약만 제공한다.
