# Presentation

Presentation은 View가 어디에 표시되고 어떤 lifetime으로 사용되는지를 `PresentationID`와 `PresentationSession`으로 표현합니다.

## Placement 획득

일반 transient View는 `UIContext.AcquirePresentation`을 사용합니다.

```csharp
Lease<VisualElement> tooltip =
    context.AcquirePresentation(
        "Gameplay.Tooltip",
        source);
```

Lease가 살아 있는 동안 placement Layer usage와 View lifetime이 유지됩니다.

advanced composition에서는 같은 primitive를 직접 사용할 수 있습니다.

```csharp
Lease<VisualElement> tooltip =
    PresentationLease.Acquire(
        context.Presentation,
        "Gameplay.Tooltip",
        source);
```

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

특정 placement mount root까지 Scene에서 authoring할 때 `PresentationPlacementHost`를 사용합니다.

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

Common UITK 경로:

```csharp
ModalSession modal = UITKModal.Open(
    context,
    "Global.Modal",
    modalRoot);
```

Focus Scope까지 함께 소유하려면:

```csharp
ModalSession modal = UITKModal.OpenWithFocus(
    context,
    "Global.Modal",
    modalRoot,
    focusScope);
```

`UITKModal`은 placement usage, hierarchy attach/detach, `UITKPresentation`, `UITKModalInteractionDriver`를 기존 `ModalController` 위에 조립합니다.

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

## Child PresentationSession

하나의 mount root 안에 독립 topology가 필요하면 Child Session을 만듭니다.

```csharp
PresentationSession child =
    parentSession.CreateChild(childLayout, contentRoot);
```

Child Session은 별도 Plan과 Session-local ID namespace를 가지며 parent Plan을 변경하지 않습니다.

Application Window는 이 구조를 사용해 Window `ContentRoot` 안에 local UI world를 만듭니다.

## Ordering

Presentation ordering은 두 값입니다.

- `LayerOrder`: 같은 Layout의 Layer 간 ordering
- `LocalOrder`: 같은 Layer의 placement 간 ordering

다음과 혼동하지 않습니다.

```text
LayerOrder / LocalOrder
!= Window z-order
!= Window StackLayer
!= Focus precedence
!= Context authority
```

## Presentation state

`IPresentation`은 Alpha/Visibility state를 표현합니다.

`PresentationGroup`은 snapshot/composite apply/group transition 같은 one-shot evaluation에 사용합니다.

지속적인 suppression은 leaf modifier Lease로 표현하고 매 frame `PresentationGroup.Apply()`를 반복하지 않습니다.

## Architecture contract

topology/Host/Source/Session의 상세 invariant는 [Presentation Architecture](../architecture/presentation.md)를 참고합니다.
