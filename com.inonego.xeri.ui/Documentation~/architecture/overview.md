# Xeri UI Architecture

Xeri UI는 Unity UI Toolkit/UGUI를 대체하는 renderer가 아닙니다. Unity의 rendering, event dispatch, native focus와 authoring을 사용하면서 application-level topology, lifecycle과 authority를 관리합니다.

## 핵심 구조

```text
PresentationLayout
→ PresentationPlan
→ PresentationSession

UIRuntime
└─ UIContext
   ├─ Screen
   ├─ Modal
   ├─ Focus
   └─ Input authority
```

각 책임은 다음처럼 나뉩니다.

| 책임 | 의미 |
|---|---|
| Definition | 무엇이 존재하고 어떤 순서인지 정의 |
| Host | Unity hierarchy 어디에 materialize되는지 제공 |
| Source | View를 어디서 acquire/release하는지 소유 |
| Session | 활성 topology와 runtime lifetime 소유 |
| UIContext | navigation, modality, focus, input authority 소유 |

이 책임을 서로 추론하거나 하나의 manager로 합치지 않습니다.

## Presentation

`PresentationLayout`은 Layer와 Placement topology의 source of truth입니다.

```text
Layer
├─ LayerID
├─ LayerOrder
└─ backend/output requirement

Placement
├─ PresentationID
├─ LayerID
└─ LocalOrder
```

`PresentationLayoutResolver`가 immutable `PresentationPlan`을 만들고, `PresentationSession`이 Plan을 실제 Unity hierarchy에 materialize합니다.

Scene-authored hierarchy가 필요한 Layer는 `PresentationLayerHost`를 사용합니다. 특정 placement mount root까지 authoring할 때만 `PresentationPlacementHost`를 사용합니다.

Host는 physical materialization만 제공합니다. LayerID/PresentationID/LayerOrder/LocalOrder의 권한은 항상 Layout/Plan에 있습니다.

## UIContext

`UIContext`는 Presentation topology와 별개의 state domain입니다.

```text
UIContext
├─ ScreenRegistry
├─ ScreenController
├─ ModalController
├─ FocusController
└─ Input policy contribution
```

Main Context 외에 독립 navigation/focus/input state가 필요하면 Child Context를 사용합니다.

Application Window는 Window의 `ContentRoot` 안에 Child `PresentationSession`을 만들고 그 Session을 사용하는 Child `UIContext`를 소유합니다.

## Ordering domain

다음 값은 서로 다른 의미입니다.

```text
Presentation LayerOrder
!= Presentation LocalOrder
!= Window instance z-order
!= Window StackLayer
!= Focus precedence
!= Context authority
```

`LayerOrder`와 `LocalOrder`는 Presentation topology만 설명합니다. Window 앞뒤 순서나 Focus 우선순위를 숫자로 대체하지 않습니다.

## Focus와 Input

Focus precedence는 numeric priority가 아니라 ownership입니다.

```text
Override.Top
→ Primary
→ Base
```

Screen의 default focus는 Driver/`IFocusScope.DefaultFocus`가 제공합니다.

Gameplay Input과 Cursor policy는 effective Context path와 Screen ownership을 기준으로 합성합니다. inactive sibling Context나 Covered Screen을 숨은 priority source로 사용하지 않습니다.

## Window

Window는 Presentation 위에 별도의 application-level window semantics를 제공합니다.

- move / resize
- minimize / maximize / restore
- focus
- close
- instance z-order
- optional `AlwaysOnTop` stack group
- persistence record
- Simple Window
- Application Window

Window ordering은 Presentation ordering과 독립적입니다.

## Public API 계층

Xeri UI는 progressive disclosure를 사용합니다.

```text
Common / Golden Path
        ↓
Project Composition
        ↓
Backend Extension
```

일반 사용자는 `UIRuntime`, `UIContext`, Screen/Presentation/Modal façade와 `XeriWindowWorkspace`에서 시작합니다.

프로젝트 composition 작성자는 Registry, Focus/Authority, Child Session/Context, Host를 직접 사용할 수 있습니다.

backend 확장자는 Layer Driver, native output/focus/input/interaction adapter까지 내려갈 수 있습니다.

Convenience API는 기존 primitive를 조합하는 얇은 façade이며 별도 state/order/lifetime source of truth를 만들지 않습니다.

## 더 자세히

- [Presentation Architecture](presentation.md)
- [Core 개요](../core/index.md)
- [Window](../window/index.md)
