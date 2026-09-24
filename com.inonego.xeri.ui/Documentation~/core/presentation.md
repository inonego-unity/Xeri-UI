# Presentation

Presentation은 **local topology**와 **표시 target lifetime**을 분리합니다.

- `PresentationSession` / immutable `PresentationPlan`: 한 local UI world의 Layer와 authored Placement topology
- `PresentationHost`: Root Layer를 app-wide semantic destination으로 노출
- `PresentationTarget`: `Local(layerID)` 또는 `Host(destinationID)`
- `PresentationLayerLease`: resolved Layer consumer lifetime

## Transient Presentation

일반 transient View는 `PresentationTarget`을 명시해 획득합니다.

```csharp
Lease<VisualElement> tooltip =
    context.AcquirePresentation(
        PresentationTarget.Host(PresentationDestinationID.Overlay),
        source);
```

Lease는 Source View와 내부 `PresentationLayerLease`를 함께 소유합니다. Dispose 시 Source를 반환한 뒤 Layer consumer lifetime을 반환합니다.

현재 Session 안의 Layer를 직접 대상으로 삼을 수도 있습니다.

```csharp
Lease<VisualElement> localView =
    context.AcquirePresentation(
        PresentationTarget.Local("Application.Overlay"),
        source);
```

Scene-authored/static Placement가 필요한 경우에만 별도 `AcquirePlacement` 경로를 사용합니다.

```csharp
Lease<VisualElement> authored =
    context.AcquirePlacement(
        "Desktop.Taskbar",
        source);
```

일반 transient UI는 `PresentationTarget`을 사용하며, 직접 Layer를 조립할 때만 `PresentationLayerLease`를 다룹니다.

## Direct Layer acquisition

Project Composition에서 Layer Root가 직접 필요하면 `AcquireLayer`를 사용합니다.

```csharp
using PresentationLayerLease layerLease =
    context.AcquireLayer(PresentationTarget.Host(PresentationDestinationID.Overlay));

IPresentationLayerDriver layer = layerLease.Layer;
```

`UIContext.AcquireLayer(...)`, `PresentationSession.AcquireLayer(...)`, `PresentationHost.AcquireLayer(...)`가 반환한
`PresentationLayerLease`는 호출자가 소유하며 `Dispose()`로 반환합니다. `PresentationHost` 자체의 lifetime은 `UIRuntime`이 소유합니다.

`UIRuntime.RootPresentation`과 `UIRuntime.Main.Presentation`이 가리키는 Root `PresentationSession`도 Runtime-owned입니다. 직접 `Dispose()`할 수 없으며 `UIRuntime.Shutdown()`에서 종료합니다. 직접 `CreateChild(...)`로 조립한 Child Session은 필요하면 먼저 종료할 수 있고, 남아 있으면 Parent Session 종료 시 함께 정리됩니다. Window 같은 상위 composition이 Child Session을 소유권으로 가져간 뒤에는 해당 owner가 종료를 담당합니다.

## IPresentationSource

`IPresentationSource<TView>`는 View acquire/release origin입니다.

Source는 다음 중 어떤 방식도 사용할 수 있습니다.

- asset clone/instantiate
- object pool rent/return
- domain owner가 가진 existing View의 explicit reference

View origin은 Layout/Plan/Host가 추론하지 않습니다.

## Scene-authored Layer

Scene hierarchy 자체를 Layer output으로 사용할 때 `PresentationLayerHost`를 제공합니다.

```text
PresentationLayerHost
├─ LayerID
├─ Native Output
├─ LayerRoot
└─ ManagedRoot
```

`LayerRoot`는 authored hierarchy 전체 영역이고 `ManagedRoot`는 Xeri가 placement ordering/lifetime을 관리하는 subtree입니다.

authoring decoration/background처럼 lifecycle 관리가 필요 없는 content는 ManagedRoot 밖에 둘 수 있습니다.

Session은 borrowed Host의 GameObject/Canvas/PanelRenderer/LayerRoot를 파괴하지 않습니다.

## Scene-authored Placement

특정 placement root까지 Scene에서 authoring할 때 `PresentationPlacementHost`를 사용합니다.

```text
PresentationPlacementHost
├─ PresentationID
└─ physical root
```

PlacementHost는 LayerID, LocalOrder, backend를 변경하지 않습니다.

Plan의 `LocalOrder`가 authored sibling/z-index ordering보다 우선하며 Session 종료 시 authored ordering state를 복원합니다.

## UITK output

Generated UITK Layer는 `UISettingsAsset.UITKPanelSettingsTemplate`에서 Runtime `PanelSettings`를 clone하고 Plan `LayerOrder`를 `sortingOrder`에 적용합니다.

Scene-authored UITK Host는 authored `PanelSettings`와 `VisualTreeAsset` identity를 유지합니다. Session 동안 `sortingOrder = LayerOrder`를 적용하고 종료 시 원래 값을 복원합니다.

UI Toolkit Scene Host는 `PanelRenderer + VisualElementReference`를 사용합니다.

## UGUI output

UGUI Layer는 Canvas를 native output boundary로 사용합니다.

`LayerOrder`는 top-level Canvas ordering에, `LocalOrder`는 같은 Layer의 placement sibling ordering에 사용합니다.

## Modal

Modal은 Presentation과 interaction stack을 조립합니다.

Tier 1 helper는 `PresentationTarget`을 `UIContext.AcquireLayer(...)`로 resolve한 뒤 root를 attach합니다.
`ModalController`의 stack/interaction/focus 책임은 그대로 유지됩니다.

App-wide Modal:

```csharp
ModalSession modal = UITKModal.Open(
    context,
    PresentationTarget.Host(PresentationDestinationID.Modal),
    modalRoot);
```

Window Child Session 내부 Modal처럼 local Layer를 사용할 수도 있습니다.

```csharp
ModalSession modal = UITKModal.OpenWithFocus(
    context,
    PresentationTarget.Local("Application.Modal"),
    modalRoot,
    focusScope);
```

`UITKModal`은 Layer Lease, hierarchy attach/detach, `UITKPresentation`, `UITKModalInteractionDriver`를 `ModalController` 위에 조립합니다.

borrowed root의 `enabledSelf`와 `pickingMode`는 종료/실패 시 원래 상태로 복원합니다.

이미 hierarchy와 lifetime을 직접 조립한 composition에서는 root overload를 사용할 수 있습니다.

```csharp
ModalSession modal =
    UITKModal.Open(context, attachedRoot, ownedLifetime);
```

backend/custom interaction까지 직접 구성하려면:

```csharp
ModalSession modal = context.Modals.Open(
    presentation,
    interactionDriver,
    ownedLifetime);
```

`context.Modals`의 lifetime은 `UIContext`가 소유합니다. 개별 `ModalSession`은 호출자가 닫을 수 있지만 Context-owned `ModalController` 자체는 직접 `Dispose()`하지 않습니다.

`UIRuntime.SceneFader`도 Runtime-owned 서비스입니다. Fade 명령은 호출할 수 있지만 해당 인스턴스의 lifetime은 `UIRuntime.Shutdown()`이 종료합니다. 직접 생성한 standalone `SceneFader`만 caller-owned입니다.

## Child PresentationSession

하나의 **local child root 안에** 독립 topology가 필요하면 Child Session을 만듭니다.
Child Session은 Window content처럼 physical containment가 실제 ownership/lifetime과 일치하는 경우의 primitive입니다.

```csharp
PresentationSession child =
    parentSession.CreateChild(childLayout, contentRoot);
```

Child Session은 별도 Plan과 Session-local ID namespace를 가지며 parent Plan을 변경하지 않습니다.

Application Window는 이 구조를 사용해 Window `ContentRoot` 안에 local UI world를 만듭니다.

부모 hierarchy 밖의 Overlay/Modal/System destination에 표시해야 하는 요구를
Child Session의 containment 규칙을 느슨하게 해서 해결하지 않습니다.
그 경우는 `PresentationTarget.Host(...)`를 resolve해 별도 Layer Lease를 획득합니다.

## Ordering

Presentation ordering은 두 값입니다.

- `LayerOrder`: 같은 Layout의 Layer 간 ordering
- `LocalOrder`: 같은 Layer의 placement 간 ordering

다음과 혼동하지 않습니다.

```text
LayerOrder / LocalOrder
!= Host destination identity
!= Window z-order
!= Window StackLayer
!= Focus precedence
!= Context authority
```

## Presentation state

`IPresentation`은 Alpha/Visibility state를 표현합니다.

`PresentationAlpha`와 `PresentationVisibility`는 `IMValue<T>`를 구현합니다. Base와 순서 있는 Modifier 목록은 local state이며 backend 적용 실패와 별도로 관리합니다.

- `Set`과 Modifier 내부 변경은 값을 평가한 뒤 backend를 한 번 적용합니다. Backend 실패 시 원하는 논리 값은 유지하며 예외를 전달합니다. 같은 Base를 다시 `Set`하거나 `Refresh()`하면 재평가·재적용합니다.
- Group 합성 출력의 적용이 성공하면 이전 로컬 적용 실패 대기는 종료됩니다. 이후 같은 로컬 값을 설정해도 성공한 합성 출력을 덮어쓰지 않습니다. `Refresh()`는 로컬 출력의 명시적 강제 적용이며, 합성 재적용은 `group.Apply()`를 사용합니다.
- 외부 Modifier가 같은 값 대입을 무시하면 변경 이벤트도 발생하지 않습니다. 이 경우 backend 재시도에는 `Refresh()`를 사용합니다. 숨은 자동 재시도는 없습니다.
- `AddModifier`의 등록·구독·평가·backend 적용이 실패하면 그 호출이 추가한 등록만 제거합니다. Local 값과 작업 직전의 실제 backend 출력을 복원하므로 이미 적용된 Composite 합성 결과도 유지합니다. 원래 실패와 정리 실패를 함께 전달합니다.
- `RemoveModifier`와 `ClearModifiers`는 해제 콜백 실패에도 논리 소유권을 종료합니다. 분리 실패로 남은 콜백은 상태를 갱신하지 못하며, 다른 구독의 해제도 끝까지 시도합니다.
- 값 변경 알림은 commit 이후 전달합니다. Observer 실패는 다른 observer를 막거나 완료된 등록을 롤백하지 않습니다. 재진입으로 값이 다시 바뀌면 이전 값의 남은 알림은 중단합니다.
- Modifier 평가·구독·backend 적용 중 같은 값 객체의 중첩 변경은 거부합니다. Modifier의 `Modify`는 순수 변환이어야 합니다.

값 계약은 `IMValue<T>`/`IReadOnlyMValue<T>` 인터페이스를 기준으로 사용합니다. runtime state는 concrete `MValue<T>`/`Value<T>` 캐스팅이나 Unity 직렬화 콜백에 의존하지 않습니다.

Source의 `Acquire`가 반환 전에 실패하면 아직 반환하지 않은 자원과 구독의 rollback은 해당 Source가 책임집니다. 호출자는 실제로 반환받은 View/Instance만 `Release`할 수 있습니다.


`PresentationGroup`은 snapshot/composite apply/group transition 같은 one-shot evaluation에 사용합니다.

`Apply()`는 전체 대상과 합성 값을 확정하고 모든 Target을 검증한 뒤 backend에 적용합니다. 적용 callback의 topology·값 변경은 다음 `Apply()`에서 반영합니다. 한 backend가 실패해도 나머지 Alpha·Visibility 적용을 시도하고 오류를 함께 전달합니다.

지속적인 suppression은 leaf modifier Lease로 표현하고 매 frame `PresentationGroup.Apply()`를 반복하지 않습니다.

## Architecture contract

topology/Host/Source/Session의 상세 invariant는 [Presentation Architecture](../architecture/presentation.md)를 참고합니다.
