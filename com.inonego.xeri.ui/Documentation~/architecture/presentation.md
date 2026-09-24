# Presentation Architecture

> 상태: Current Architecture Contract
> 갱신일: 2026-10-07
> 범위: Xeri UI Core Presentation / Screen / Focus / Input / Window / Scene-authored UI / Editor authoring
>
> 이 문서는 **현재 구현이 지켜야 하는 구조와 계약의 source of truth**다.
> PresentationTarget → AcquireLayer → PresentationLayerLease composition도 현재 구현 계약에 포함된다.
> 세부 composition 책임과 검증 기준은
> [Presentation Composition Contract](presentation-composition.md)에 정리한다.

## 0. 문서 역할

이 문서는 **현재 구현이 보장하는 시스템 상태를 현재형으로 정의**한다.

다음을 architecture source of truth로 둔다.

- 책임과 소유권
- runtime topology
- ordering 의미
- lifetime과 authority
- 공개 API가 표현해야 하는 semantic
- 허용/금지하는 설계
- public / composition / backend extension 경계

구체적인 타입 이름과 구현 세부는 최종 계약 자체에 포함되는 경우에만 언급한다.

## 1. 역할

Xeri UI는 Unity UI Toolkit/UGUI를 다시 구현하는 UI 프레임워크가 아니다.

Xeri UI의 역할은 다음이다.

- Presentation topology와 placement를 선언한다.
- 활성 topology를 Session lifetime으로 관리한다.
- Screen / Modal / Focus / Input의 application-level state를 관리한다.
- Window를 독립적인 runtime UI world로 구성할 수 있다.
- Scene-authored UI와 runtime-generated UI를 같은 lifetime 계약으로 사용할 수 있다.
- Unity가 이미 제공하는 rendering / event dispatch / native focus / in-scene authoring은 그대로 사용한다.

핵심 문장:

> **Layout/Plan은 논리 topology와 ordering을 정하고, generated output 또는 PresentationLayerHost는 physical materialization을 제공하며, PresentationHost는 app-wide destination을 Root Layer로 resolve한다. Source는 View origin을 정하고, Session은 활성 materialization/lifetime을 소유하며, UIContext는 UI authority/state를 소유한다.**

## 2. 범위 밖

다음은 기본 책임이 아니다.

- 별도 UI renderer 구현
- package-owned full-screen offscreen compositor
- UI Toolkit focus navigation 재구현
- UI Toolkit pointer/navigation event system 재구현
- 독자적인 UXML/Prefab visual authoring stage
- Scene 전체를 검색하는 implicit binding
- tracking / projection / billboard 기능
- World와 ScreenOverlay를 임의의 정수 하나로 항상 pixel-interleave하는 compositor
- 명시된 ownership/lifetime 계약이 없는 generic cross-backend embedded composition

## 3. 전체 구조

```text
PresentationLayout
├─ Layers
│  ├─ LayerID
│  ├─ LayerOrder
│  └─ Backend
└─ Placements
   ├─ PresentationID
   ├─ LayerID
   └─ LocalOrder
        ↓
PresentationLayoutResolver
        ↓
immutable PresentationPlan
        ↓
PresentationSession
├─ Session-local Runtime Layers
│  ├─ Generated Layer materialization
│  └─ Scene-authored PresentationLayerHost
├─ Placement Roots
│  ├─ Generated Placement Root
│  └─ optional PresentationPlacementHost
├─ active Presentation instances
└─ dynamic Child PresentationSessions
```

```text
UIContext
├─ ScreenRegistry
├─ ScreenController
├─ ModalController
├─ FocusController
└─ Input policy contribution
```

PresentationSession과 UIContext는 개념적으로 서로 다른 축이다.

- PresentationSession = local render topology / placement / lifetime
- UIContext = navigation / modality / focus / input authority와 logical ownership

Child Context와 Child PresentationSession은 local embedded composition에서 함께 사용할 수 있다.
app-wide/non-local presentation은 Child Session을 확장하지 않고 PresentationTarget.Host와
PresentationHost/PresentationLayerLease 축으로 분리한다.

## 4. Ordering 모델

Xeri Presentation ordering은 두 값만 사용한다.

### LayerOrder

같은 PresentationLayout scope 안에서 Layer ↔ Layer의 logical ordering intent다.

### LocalOrder

같은 Layer 안에서 Presentation placement ↔ Presentation placement의 순서다.

### 별도 ordering domain

다음은 LayerOrder/LocalOrder와 다른 의미다.

- 같은 Window workspace 안 Window instance z-order
- Window coarse z-group / AlwaysOnTop 정책
- 동일 Presentation placement 안 dynamic instance order
- Focus precedence
- Context authority

서로 추론하지 않는다.

```text
LayerOrder
!= LocalOrder
!= Window z-order
!= Focus precedence
!= Context authority
```

## 5. ID namespace

LayerID와 PresentationID uniqueness는 **PresentationSession scope**다.

따라서 부모/자식/형제 Session은 같은 ID를 재사용할 수 있다.

```text
Root Session
├─ Screen
├─ Overlay
└─ Modal

Application Window A Session
├─ Screen
├─ Overlay
└─ Modal

Application Window B Session
├─ Screen
├─ Overlay
└─ Modal
```

각 Session은 내용과 runtime state가 독립적이다.

동일 이름의 Layer/Presentation이 sibling Session 사이에서 직접 충돌하거나 ordering 비교되지 않는다.

## 6. PresentationLayout / Plan

PresentationLayout은 authoring configuration이다.

현재 Layer가 가지는 정보:

- LayerID
- DisplayName
- LayerOrder
- PresentationBackend

Layer의 backend는 serialized `backend` field로 직접 저장한다.

Placement가 가져야 하는 정보:

- PresentationID
- LayerID
- LocalOrder

PresentationLayout은 다음을 저장하지 않는다.

- runtime View instance
- Scene GameObject / PanelRenderer reference
- Focus target
- tracking target
- Presenter instance
- Window instance
- Child Session instance
- PresentationLayerHost / PresentationPlacementHost reference

PresentationLayoutResolver는 Layout을 검증해 immutable PresentationPlan으로 만든다.

Resolver는 Unity Object를 생성하지 않는다.

Plan이 활성화된 뒤 topology/placement를 live mutation하지 않는다.

## 7. Native Output

Native Output은 Unity의 실제 render boundary다.

현재 Presentation Core의 top-level materialization primitive는 다음이다.

- UGUI: Canvas 기반 generated/provided output
- UITK: PanelRenderer + PanelSettings 기반 generated/provided output

UITK Native Output boundary는 `PanelRenderer + PanelSettings`로 표현한다.

현재 top-level correctness 기본 경로는 Layer마다 독립적으로
ordering/lifetime을 제어 가능한 Native Output boundary를 갖는 것이다.

```text
Layer A -> Native Output A
Layer B -> Native Output B
Layer C -> Native Output C
```

Layer merge는 현재 correctness contract에 포함하지 않는 implementation optimization이다.

한 Layer가 사용하는 native object 수나 batching 형태는 public topology semantic이 아니다.
공개 계약은 LayerOrder / LocalOrder, Session lifetime, backend identity와
generated/scene-authored ownership 경계를 우선한다.

## 8. Backend requirement와 RenderSpace 확장

현재 `PresentationLayerDefinition` / `PresentationPlanLayer`가 표현하는
backend 계약은 `PresentationBackend` 하나다.

```text
PresentationBackend
- UITK
- UGUI
```

serialized field는 `backend`이며, runtime Plan에는 ScreenOverlay / ScreenCamera / World 또는
DepthMode가 별도 semantic으로 존재하지 않는다.

다음 개념은 현재 Presentation API에 포함하지 않는다.

- PresentationRenderSpace
- ScreenOverlay / ScreenCamera / World capability
- SceneDepth requirement
- camera/depth/coordinate conversion capability

render-space 의미를 Target/Layer Lease나 LayerOrder에서 암묵적으로 추론하지 않는다.

## 9. Layer materialization / PresentationLayerHost

Layer definition과 Unity Scene의 물리적 Layer hierarchy는 같은 객체가 아니다.

```text
PresentationPlanLayer
= Layer의 의미 / backend requirement / LayerOrder

PresentationLayerHost
= 그 Layer를 Unity hierarchy에 materialize할 Scene-authored host
```

Layer materialization은 두 origin을 지원한다.

### Generated Layer materialization

Scene에 대응 `PresentationLayerHost`가 없으면 Session activation 중 Xeri가 필요한 native 구조를 생성한다.

```text
Plan Layer
→ Native Output 생성
→ LayerRoot 생성
→ ManagedRoot 생성
→ RuntimeLayer
```

- Session이 생성 lifetime 전체를 소유한다.
- Session dispose 시 Xeri가 생성한 native object/root를 제거한다.

### Scene-authored PresentationLayerHost

Scene에서 Layer 자체의 물리적 배치와 authoring이 필요하면 `PresentationLayerHost`를 명시적으로 제공한다.

`PresentationLayerHost`는 다음 경계를 제공한다.

```text
PresentationLayerHost
├─ LayerID
├─ Native Output boundary
├─ LayerRoot
└─ ManagedRoot
```

- `LayerID`는 resolved Plan Layer와 결합하기 위한 stable key다.
- Native Output은 Layer의 backend requirement를 만족해야 한다.
- `LayerRoot`는 Scene authoring 전체 영역이다.
- `ManagedRoot`는 Xeri가 Placement Root ordering/lifetime을 관리하는 전용 subtree다.
- Scene-authored background/frame/decoration처럼 Xeri lifecycle이 필요 없는 child는 LayerRoot의 unmanaged authored content로 둘 수 있다.
- Xeri가 managed placement ordering을 적용할 때 unmanaged authored child의 sibling order를 변경하지 않는다.
- provided `PresentationLayerHost`의 외부 GameObject/PanelRenderer/Canvas/LayerRoot lifetime은 Scene/composition owner가 소유한다.
- borrowed native output의 authored configuration identity도 Scene/composition owner가 소유하며, Session은 필요 이상으로 교체하지 않는다.
- UITK Scene Host는 연결된 `PanelSettings` / `VisualTreeAsset` identity를 유지하고 Plan LayerOrder를 `sortingOrder` scoped mutation + restore로 적용한다.
- Session은 자신이 추가한 runtime child와 임시 native state만 반환/복원한다.

Activation:

```text
PresentationLayout
→ immutable PresentationPlan
→ Plan Layer마다 explicit LayerHost 조회
   ├─ LayerHost 있음
   │  → LayerID/backend/root capability 검증
   │  → Scene-authored LayerHost borrow
   │
   └─ LayerHost 없음
      → Generated Layer materialization
→ RuntimeLayer
```

Scene 전체를 LayerID로 자동 검색하지 않는다.
`PresentationLayerHost`는 UIRuntime/composition edge에서 명시적으로 공급한다.

### Optional PresentationPlacementHost

Scene에서 Layer뿐 아니라 **특정 Placement의 geometry/root까지 authoring**해야 하는 경우
`PresentationPlacementHost`를 optional physical anchor로 지원한다.

```text
Plan Placement
├─ PresentationID
├─ LayerID
└─ LocalOrder

PresentationPlacementHost
├─ PresentationID
└─ authored Placement Root
```

PlacementHost가 결정할 수 있는 것은 **물리적 placement root**뿐이다.

PlacementHost는 다음을 정의하거나 override할 수 없다.

- Presentation 존재 여부
- LayerID
- LocalOrder
- backend requirement

이 값들의 source of truth는 항상 PresentationLayout/Plan이다.

Activation:

```text
Plan Placement
→ 같은 LayerHost ManagedRoot 안의 explicit PlacementHost 조회
   ├─ 있음 → authored Placement Root 사용
   └─ 없음 → ManagedRoot 아래 generated Placement Root 생성
```

LocalOrder는 authored sibling order보다 우선한다.
Provided PlacementHost도 RuntimeLayer의 ordering 규칙을 따라야 한다.

이 구조를 통해 한 Layer 안에서 다음을 동시에 사용할 수 있다.

```text
LayerRoot
├─ authored static content
└─ ManagedRoot
   ├─ Health PlacementHost      // authored
   ├─ Objective Root            // generated
   └─ Dialogue PlacementHost    // authored
```

### Definition / Materialization / View 경계

```text
Definition
= 무엇이 존재하고 어떤 순서인가

Materialization
= generated output 또는 Scene-authored Host가 Unity hierarchy를 어떻게 제공하는가

View
= 실제 content instance와 acquire/release lifetime
```

이 세 책임을 서로 추론하거나 합치지 않는다.

금지:

- Layout/Plan에 Scene Object reference 저장
- Scene Host가 LayerID/LocalOrder 등 topology를 새로 정의
- Scene hierarchy sibling order를 logical ordering source로 사용
- LayerID/PresentationID 기반 Scene global implicit discovery
- 활성 Session 중 generated/provided materialization origin 교체
- missing Host를 silent Scene search로 복구

## 10. Presentation Source / externally-owned View

`IPresentationSource<TView>`는 생성 방식이 아니라 **View acquire/release origin**이다.

```text
Asset-backed Source
Acquire = clone / instantiate
Release = detach / destroy

Pool-backed Source
Acquire = rent
Release = return

Externally-owned View를 사용하는 domain Source
Acquire = explicit reference에서 existing View 사용
Release = domain ownership contract에 맞게 detach/restore
```

Scene-authored View도 별도 topology가 아니다.

규칙:

- View origin은 Layout/Plan/LayerHost/PlacementHost에 저장하지 않는다.
- LayerHost/PlacementHost 아래에 authored View가 이미 존재해도 Core가 자동으로 Presentation으로 인식하지 않는다.
- lifecycle이 필요 없는 authored static content는 LayerRoot의 unmanaged content로 남길 수 있다.
- Screen/Presentation lifecycle이 필요한 View는 해당 domain Source가 명시적 reference를 통해 acquire/release한다.
- Core는 ownership을 추론하는 generic Scene/Borrowed View Source를 제공하지 않는다. externally-owned View는 해당 domain Source가 명시적 reference와 acquire/release 계약으로 소유한다.
- Scene sibling order를 LocalOrder로 해석하지 않는다.
- ordering authority는 항상 Plan의 LayerOrder/LocalOrder다.
- Scene에 있다는 이유만으로 전역 검색하지 않는다.
- Unity in-scene UI authoring과 VisualElementReference 같은 native reference 수단을 우선 사용한다.

## 11. Child PresentationSession

Nested UI world는 runtime Child PresentationSession으로 표현한다.

```text
Parent Session
└─ owned child root
   └─ Child Session
      ├─ local Layer namespace
      ├─ local placements
      └─ local runtime state
```

Child Session:

- 별도 immutable Child Plan을 가진다.
- 부모 Plan을 수정하지 않는다.
- sibling Child Session과 state를 공유하지 않는다.
- 부모가 직접 소유하는 child root 아래에만 attach한다.
- parent와 같은 backend의 embedded composition만 지원한다.
- 부모 Native Output을 공유하고 local Layer/placement root를 생성한다.

현재 Child Session API는 local embedded composition 전용이며,
parent가 직접 소유하는 child root 밖으로 탈출하는 generic relocation API를 제공하지 않는다.

이 제한은 현재 구현 계약이다. non-local Overlay/Modal/System presentation은
Child Session을 느슨하게 확장하지 않고 `PresentationTarget`을 resolve해 Layer Lease를 획득한다.

## 12. Root Layout lifetime

현재 UIRuntime은 초기화 시 하나의 Root PresentationLayout을 활성화하고 Runtime lifetime 동안 유지한다.

Public Root Layout hot-swap은 Presentation 계약에 포함하지 않는다.
local nested topology는 다음 경로로 표현한다.

- Child PresentationSession
- Application Window Child Session
- UIRuntime 재구성 / scene lifecycle

Overlay / Modal / System 같은 non-local presentation은 Root Layout의 고정 Placement로
사전 선언하지 않는다. `PresentationTarget.Host(...)`를 resolve해 Layer Lease를 획득하는 방식으로 표현한다.

## 13. Screen semantic

Screen은 full-screen geometry가 아니다.

Screen은 UIContext 안의 navigation/lifecycle stack state다.

```text
Screen
= Open / Active / Covered / Closing / Closed
+ interaction ownership
+ Focus memory
+ input/cursor policy
+ transition lifetime
```

View root는:

- full-screen일 수도 있고
- 일부 영역일 수도 있다.

새 Screen이 열리면 이전 top은 Covered 상태로 살아 있을 수 있지만 기본 interaction/cursor owner는 아니다.

## 14. ScreenOptions

`ScreenOptions`는 Screen registration이 소유하는 navigation/input/cursor/transition policy만 표현한다.

```text
ScreenOptions
├─ ID
├─ DuplicatePolicy
├─ BlocksGameplayInput
├─ ShowsCursor
├─ CursorLockMode
├─ OpenDuration
└─ CloseDuration
```

표시 위치는 registration이 소유한 별도 `PresentationTarget`으로 resolve한다.
Screen ID, local Layer ID와 Host destination ID는 서로 다른 identity다.

기본 Focus는 Screen Driver/IFocusScope가 제공하며,
ScreenController는 `UIContext.AcquireLayer(Target)`으로 Layer Lease를 획득한 뒤 IScreenSource에 최종 Layer Driver를 전달한다.

### DuplicatePolicy

동일 Screen ID의 live instance 중복 허용 여부다.

- Reject
- Allow

이 값은 실제 Screen stack semantic이므로 유지한다.

### Transition time

Screen navigation transition은 항상 unscaled time을 사용한다.

Pause / timeScale=0 상태에서도 Screen navigation이 정지하지 않는 것이 기본 계약이다.

Generic Presentation Transition은 별도 API에서 scaled/unscaled 선택을 지원할 수 있다.

## 15. Focus policy

Focus의 logical priority는 숫자 priority가 아니라 ownership precedence다.

```text
Effective Scope
= Override.Top
  ?? Primary
  ?? Base
```

- Base = current Screen
- Primary = active Simple Window 같은 peer owner
- Override = Modal 같은 transient owner

Application Window는 Primary Scope가 아니라 독립 Child UIContext다.

### Screen DefaultFocus

Screen의 default focus source는 실제 View/Driver가 제공하는 `IFocusScope.DefaultFocus` 하나로 둔다.

복원 순서:

```text
LastFocus
→ Scope.DefaultFocus
→ null
```

Open 시점의 임시 Focus override가 필요하면 Screen registration과 분리된 명시적 lifetime으로 표현한다.

## 16. Native Focus

Unity native FocusController가 다음을 담당한다.

- focus navigation
- focus ring
- Focus()/Blur()
- focus event dispatch
- focused element state

Xeri는 다음 logical policy만 추가한다.

- LastFocus memory
- Screen/Window/Modal ownership
- Context authority
- scope containment
- invalid/out-of-scope focus 복원 요청

기본 경로에서 다음을 재구현하지 않는다.

- panel별 custom focus navigation
- global blur graph
- LateUpdate focus reconciliation

실제 재현 가능한 Unity event ordering 문제에만 최소 workaround를 둔다.

## 17. Input policy

Xeri input 책임:

- active Context path 계산
- Gameplay Action Map block contribution
- Cursor visible / lock policy
- input-release barrier
- 필요 시 explicit scoped Cursor override

Unity UI event system 책임:

- pointer events
- navigation events
- submit / cancel
- panel event dispatch

UITK-only Runtime은 Scene EventSystem + InputSystemUIInputModule을 필수 dependency로 두지 않는다.

Runtime은 active Layout만 보고 backend infrastructure를 추론하지 않는다.
`UISettingsAsset.BackendSupport`를 기준으로 UITK/UGUI infrastructure capability를 bootstrap에서 검증한다.

## 18. Gameplay input 합성

`BlocksGameplayInput`은 합성 가능한 policy다.

active Context path의 유효한 Screen contribution을 OR할 수 있다.

```text
any active contribution blocks gameplay
→ gameplay blocked
```

inactive sibling Context는 contribution하지 않는다.

## 19. Cursor policy

Cursor는 exclusive state이므로 숫자 priority로 합성하지 않는다.

Resolution:

```text
1. ScreenController가 명시적으로 유지한 input-release barrier Cursor policy
2. Effective Context의 Active Top Screen
3. 가장 가까운 active ancestor owner의 policy
4. captured baseline
```

규칙:

- Covered Screen은 기본 Cursor owner가 아니다.
- inactive sibling Context는 Cursor policy에 기여하지 않는다.
- Cursor precedence는 Context/Screen ownership과 explicit scoped override로만 결정한다.
- acquisition sequence/최근 획득 순서를 숨은 Cursor priority로 사용하지 않는다.
- release-barrier Cursor owner는 Screen lifecycle owner가 명시적으로 정한다.
- transient 강제가 필요하면 scoped Cursor override lease를 사용한다.

## 20. Modal

Modal은 interaction stack과 optional Focus override를 함께 조립할 수 있지만 두 개념은 동일하지 않다.

```text
Modal interaction
!= Focus Scope
```

Modal top만 interaction 가능하게 하는 정책은 ModalController가 소유한다.

Focus trap이 필요하면 별도 Focus Scope / Override를 획득한다.

Tier 1 Modal API는 `PresentationTarget`과 View Root를 받는다.
UITK helper는 `UIContext.AcquireLayer(target)`으로 Layer Lease를 획득하고 root를 attach한 뒤,
`UITKPresentation` + `UITKModalInteractionDriver`를 `ModalController.Open(...)` 위에 조립한다.
Modal stack, Focus state, ordering은 별도 source of truth로 복제하지 않는다.

`ModalController`의 Presentation / Interaction / optional Focus 분리는 그대로 유지한다.

고급 사용자는 `ModalController.Open(IPresentation, IModalInteractionDriver, ...)`을 직접 사용해
custom Presentation, Interaction backend, Focus Scope와 owned lifetime을 조립할 수 있다.

Workspace-global modal이 필요하면 Context Override와 Focus Override를 함께 사용한다.

## 21. Presentation state

Presentation state:

- Alpha

- Visibility

leaf local state는 reactive modifier 체계를 사용할 수 있다.

### PresentationGroup

PresentationGroup은 one-shot composite tree evaluation 도구다.

적합한 용도:

- snapshot
- 일회성 composite apply
- group transition
- 특정 시점의 capture preparation

부적합한 용도:

- persistent suppression ownership
- 매 frame parent state 재적용
- reactive parent graph

## 22. Persistent suppression

지속 suppression은 leaf scoped Modifier Lease로 표현한다.

```text
Acquire suppression
→ target leaf에 modifier 등록
→ leaf local state가 변경돼도 modifier와 자동 합성
→ Dispose
→ modifier 제거
→ 최신 local state 복원
```

dynamic target이 늦게 생성/교체되면 polling하지 않고 explicit target-changed event로 active lease membership을 갱신한다.

persistent suppression 때문에 LateUpdate에서 PresentationGroup.Apply()를 반복하지 않는다.

## 23. Window

Window subsystem은 Presentation ordering과 별도 application-level window semantics를 제공한다.

지원 의미:

- move
- resize
- minimize
- maximize / restore
- close
- focus
- z-order
- persistence record
- optional coarse z-group / AlwaysOnTop
- Simple Window
- Application Window

Window z-order를 LayerOrder나 Focus precedence로 추론하지 않는다.

## 24. ContentRoot

Window chrome과 application content의 경계 이름은 `ContentRoot`다.

```text
XeriWindowPanel
├─ TitleBar / controls / resize handles
└─ ContentRoot
   ├─ Simple Window View
   └─ 또는 Child PresentationSession
```

ContentRoot는 Core generic abstraction이 아니라 Window가 제공하는 concrete content root다.

## 25. Simple Window

Simple Window:

- 별도 Child UIContext를 만들지 않는다.
- Workspace UIContext에 persistent Focus Scope record를 등록한다.
- inactive 상태에서도 LastFocus memory를 유지한다.
- Window activation은 Primary Focus Scope selection을 바꾼다.

## 26. Application Window

Application Window는 embedded local UI world다.

```text
Application Window
├─ Window chrome
├─ ContentRoot
├─ Child PresentationSession
└─ Child UIContext
   ├─ Screens
   ├─ Modals
   ├─ Focus
   └─ Input contribution
```

각 Application Window의 내부 LayerOrder/LocalOrder/Screen/Modal/Focus/Input state는 sibling Window와 독립적이다.

Window 전체 순서는 parent Window subsystem z-order가 결정한다.

## 27. Window Workspace API

Window Workspace가 내부적으로 Screen을 활용할지는 implementation detail이다.

일반 Window 소비자에게 raw ScreenOptions를 요구하지 않는다.

일반 API는 Window 전용 workspace configuration으로 다음 정도만 표현한다.

- Workspace `PresentationTarget`
- 필요한 Window-specific behavior/options

Core Screen policy를 직접 조정해야 하는 Tier 2 Project Composition만 하위 composition API를 사용한다.

## 28. Window View Source

Window View Source도 acquire/release lifetime 언어를 따른다.

View Source naming:

```text
AcquireView(...)
ReleaseView(...)
```

Scene-authored borrowed View도 자연스럽게 표현할 수 있어야 한다.

다만 Window persistence/UI session이라는 추가 책임이 있으므로 IPresentationSource와 하나의 거대 generic interface로 합치지 않는다.

## 29. Window Stack Group

Window coarse z-group은 Window-specific ordering domain이다.

예:

- Normal
- AlwaysOnTop

같은 group 안에서는 BringToFront / SendToBack으로 instance order를 관리한다.

이 기능은 Presentation LayerOrder와 독립이다.

`Normal`과 `AlwaysOnTop`은 Window-specific coarse z-group으로 지원한다.

## 30. Editor authoring

Unity 6000.7 in-scene UI authoring / UI staging / Scene hierarchy / Inspector를 기본 visual authoring surface로 사용한다.

Xeri Editor는 Unity가 모르는 semantic만 보완한다.

- Layout tree
- LayerOrder
- LocalOrder
- backend capability validation
- PresentationID → Layer placement 탐색
- generated/Scene-authored LayerHost materialization 진단
- optional PlacementHost와 Plan placement의 일치 여부 진단
- LayerRoot / ManagedRoot containment 진단
- resolved Plan debug
- 관련 authored UI로 navigation

별도 full UXML/Prefab Preview Stage는 public authoring contract에 포함하지 않는다.

## 31. Tray / Bar native-first

Tray:

- Unity ListView가 요구를 만족하는 generic list/reorder는 native 기능 우선
- horizontal taskbar/window tray처럼 실제 차이가 있는 기능만 Xeri가 소유

Bar:

- 단순 progress는 Unity ProgressBar 우선
- 4-direction fill, delta/damage-lag 등 실제 추가 semantic만 Xeri custom Bar가 소유

## 32. Public API 레벨과 progressive disclosure

Public API는 progressive disclosure를 따른다.
기본 사용자가 알아야 하는 표면은 작게 유지하고, 고급 사용자는 같은 책임 경계 안에서
composition/backend primitive까지 직접 조작할 수 있어야 한다.

핵심 원칙:

> **Simple by default, explicit when advanced, impossible when invalid.**

Convenience API는 Composition primitive를 조합하는 얇은 facade다.
별도 상태 저장소, 별도 ordering, 별도 lifetime source of truth를 만들지 않는다.
고급 API를 사용한다고 해서 Layout/Plan의 topology 권한이나 Session의 lifetime invariant를
우회할 수는 없다.

### Tier 1 — Feature / Golden Path

일반 UI 개발자는 기능 의도와 표시 대상만 다룬다.

- `UIRuntime` / `UIContext`
- `UIContext.Screens`
- Screen registration / Presentation acquisition facade
- `ScreenOptions` / `IScreenSource` / `ScreenSession`
- `PresentationTarget` / built-in `PresentationDestinationID`
- `UITKModal` / `SceneFader`
- `XeriWindowWorkspace` / `XeriWindowSession`
- `XeriTrayController` / Bar

Golden Path의 Screen/Modal/Transient UI는 semantic destination 또는 local Layer target을 입력으로 사용한다.
`PresentationLayerLease`와 native Layer Root는 직접 관리하지 않는다.
static authored Placement만 별도 `AcquirePlacement` 경로로 유지한다.

### Tier 2 — Project Composition

프로젝트 UI architecture 작성자는 필요하면 Layer 사용 수명과 조립 primitive를 직접 사용한다.
Runtime-owned Root `PresentationSession`과 Main `UIContext`의 종료 권한은 `UIRuntime`에 남는다. Window 같은 상위 composition이 Child Session/Context를 소유한 경우도 해당 owner가 종료 권한을 가진다. Project Composition API를 사용해도 이미 owner-controlled인 lifetime을 직접 종료할 수 없다.

- `UIContext.AcquireLayer(PresentationTarget)`
- `PresentationSession.AcquireLayer(layerID)`
- `PresentationHost.AcquireLayer(destinationID)`
- `PresentationLayerLease`
- `ScreenRegistry` / Source 구현
- backend helper의 Composition overload
- `ModalController`의 저수준 Open
- Focus Scope registration / Primary / Override
- Context Base Authority / Authority Override
- `PresentationSession` / Child Session / Child UIContext
- `PresentationLayerHost` / optional `PresentationPlacementHost`
- Window Controller / Registry 등 대체 composition 진입점

이 레벨은 supported Tier 2 API다. 표준 facade 내부 state를 수정하는 escape hatch가 아니라,
같은 topology/lifetime invariant 안에서 대체 composition을 명시적으로 구성하기 위한 계약이다.

### Tier 3 — Backend Extension

backend/platform 확장자는 Unity backend 구현 경계를 소유한다.

- `IPresentationLayerDriver` / `IPresentationLayerDriver<TRoot>` 구현
- Native Output materializer/provider
- UGUI/UITK Layer와 Output 구현
- native Focus/Input adapter
- backend별 interaction/transition adapter

Tier 2의 Source가 Layer Driver 계약을 소비할 수는 있지만 backend 의미와 materialization을 구현하는 책임은 Tier 3에 있다.
일반 소비자가 Registry/Surface/native output 생성 세부를 직접 알아야 하는 Golden Path는 만들지 않는다.

### Tier 2/3 API에서도 금지하는 조작

상세 제어가 필요해도 다음은 public escape hatch로 열지 않는다.

- 활성 Plan의 LayerID / PresentationID / LayerOrder / LocalOrder mutation
- Host가 Plan topology를 override하는 API
- Scene global implicit discovery
- numeric Focus/Input priority로 ownership precedence 우회
- 표준 owner가 가진 state를 외부에서 병렬 소유하는 mutable backdoor
- borrowed external object lifetime을 Session이 임의로 Destroy하는 API

더 상세한 제어는 **같은 invariant 안에서 더 낮은 primitive를 직접 조립**하는 방식으로 제공한다.

## 33. Source naming 원칙

Source는 공통적으로 "획득하고 반환한다"는 정신모델을 가진다.

다만 domain responsibility가 다르면 interface를 억지로 합치지 않는다.

- IPresentationSource = transient presentation view lifetime
- IScreenSource = ScreenInstance acquisition
- IXeriUIViewSource = Window view + persistence/session lifecycle
- IXeriTraySource = Tray data source

Acquire/Release 언어를 우선한다.

## 34. Unity native-first 원칙

새 Xeri abstraction을 추가하기 전에 다음을 확인한다.

1. Unity가 이미 같은 invariant를 제공하는가.
2. Xeri의 정해진 책임으로 표현 가능한가.
3. 실제 supported entrypoint에서 도달하는 요구인가.
4. 새 타입이 독립 lifetime/failure/ownership boundary를 실제로 소유하는가.

근거가 없으면 추가하지 않는다.

특히 다음은 native 우선이다.

- PanelRenderer
- PanelSettings
- UITK FocusController
- UI Toolkit runtime event system
- in-scene UI authoring
- VisualElementReference
- ListView
- ProgressBar

## 35. 핵심 Invariants

1. LayerOrder는 같은 Layout scope의 Layer ordering intent다.
2. LocalOrder는 같은 Layer 안의 placement 순서다.
3. Session-local LayerID/PresentationID namespace를 사용한다.
4. 활성 Plan topology는 immutable이다.
5. Child Session 생성/해제가 parent Plan을 바꾸지 않는다.
6. Layer materialization origin은 Generated 또는 explicit Scene-authored PresentationLayerHost다.
7. PresentationLayerHost는 Native Output + LayerRoot + ManagedRoot의 물리적 materialization만 제공한다.
8. optional PresentationPlacementHost는 physical placement root만 제공하며 topology를 정의하지 않는다.
9. LayerID/PresentationID/LayerOrder/LocalOrder source of truth는 항상 Layout/Plan이다.
10. Scene Object reference는 Layout/Plan에 저장하지 않는다.
11. Scene global implicit discovery를 하지 않는다.
12. Scene-authored Layer/Placement Host의 외부 lifetime을 Session이 파괴하지 않는다.
13. Xeri-managed placement ordering은 ManagedRoot subtree에만 적용한다.
14. authored static content를 managed placement ordering 대상으로 취급하지 않는다.
15. UITK native output은 PanelRenderer/PanelSettings를 기본 primitive로 사용한다.
16. 현재 지원하지 않는 backend/materialization capability를 자동 우회하거나 다른 ordering 값에서 추론하지 않는다.
17. Screen은 navigation state이며 geometry 크기와 무관하다.
18. 현재 ScreenOptions는 ID/DuplicatePolicy/BlocksGameplayInput/ShowsCursor/CursorLockMode/OpenDuration/CloseDuration만 소유한다. Screen identity와 presentation destination을 동일 semantic으로 고정하지 않는다.
19. Screen transition은 unscaled time을 사용한다.
20. Screen default focus는 IFocusScope/Driver가 단일 source다.
21. Cursor policy는 numeric priority가 아니라 Context/Screen ownership으로 결정한다.
22. Covered Screen은 기본 Cursor owner가 아니다.
23. persistent suppression은 scoped modifier lifetime으로 표현한다.
24. PresentationGroup을 persistent reactive graph로 사용하지 않는다.
25. Window application content root 이름은 ContentRoot다.
26. Application Window는 독립 Child Session + Child UIContext를 가진다.
27. Window z-order와 Presentation ordering을 서로 추론하지 않는다.
28. Unity native 기능으로 동일 contract를 만족하면 중복 subsystem을 만들지 않는다.
29. Public Root Layout hot-swap은 Presentation 계약에 포함하지 않는다.
30. Tier 1 Feature API는 Composition primitive의 lossless facade이며 별도 state source of truth를 만들지 않는다.
31. Tier 2 Project Composition / Tier 3 Backend primitive는 supported path로 유지한다.
32. Tier 2/3 API도 Layout/Plan topology, Session lifetime, Context authority invariant를 우회하지 않는다.
33. Screen identity와 PresentationTarget identity를 동일하게 추론하지 않는다.
34. Host target은 Settings의 destination → Root Layer mapping으로 resolve한다.
35. Local target은 현재 PresentationSession의 Layer ID로 resolve한다.
36. transient Screen/Modal/Window/Fade는 `PresentationTarget`을 통해 Layer를 획득하며, 직접 조립 시 `PresentationLayerLease`가 그 사용 수명을 소유한다.

이 목록은 현재 구현 invariant다.
Plan immutability, ownership, lifetime, authority separation은 Target/Layer Lease 경로에서도 유지한다.

## 36. 금지 설계

Architecture contract는 다음 구조를 허용하지 않는다.

- Context/Screen ownership을 우회하는 numeric Focus/Cursor priority
- Screen Driver/Focus Scope와 별도로 유지되는 registration-level default Focus source
- Screen navigation마다 별도 transition clock을 선택하는 정책
- 같은 Window ContentRoot를 둘 이상의 public 이름으로 노출하는 구조
- implicit Scene LayerID/PresentationID discovery
- Layout의 Scene Object reference
- Output만 연결하고 LayerRoot/ManagedRoot ownership을 표현하지 못하는 output-only binding
- Scene Host가 LayerID/LocalOrder를 override하거나 별도 topology source가 되는 구조
- ownership을 추론하는 Core generic Scene/Borrowed View Source
- full custom UITK event system
- full custom UITK focus navigation
- full custom Editor visual authoring stage
- persistent suppression용 LateUpdate + PresentationGroup.Apply()
- public Root Layout hot-swap transition API
- 동일 semantic에 둘 이상의 source of truth를 유지하는 중복 경로
- Tier 1 API 단순화를 이유로 supported Tier 2/3 primitive 자체를 제거하는 구조
- convenience facade가 별도 registry/state/order/lifetime owner가 되는 구조
- non-local presentation 요구를 해결하기 위해 Child Session의 physical containment invariant를 암묵적으로 우회하는 구조
- Target/Layer Lease composition을 이유로 활성 PresentationPlan 자체를 mutable graph로 바꾸는 구조
- Host destination identity를 Focus/Input/Window의 numeric priority로 해석하는 구조

Architecture contract에 명시되지 않은 batching, helper concrete type, internal host 구현 방식, backend별 authoring serialization shape는 implementation detail이다.
이 implementation detail은 위의 source of truth, ownership, ordering, lifetime, authority invariant를 변경할 수 없다.
