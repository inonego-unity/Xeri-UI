# Presentation Composition Contract

> 상태: Current Architecture Contract
> 기준일: 2026-10-07
> 범위: Core Presentation / UIContext / Screen / Modal / Focus / Input / Window / Scene-authored Host / Validation
>
> 이 문서는 Target 해석, Layer Lease, destination, static authored Placement의 현재 책임과 검증 기준을 정의한다.

## 1. 핵심 모델

Core는 PresentationLayout → immutable PresentationPlan → PresentationSession 모델을 유지한다.
이 모델은 local topology, deterministic materialization, rollback과 lifetime 관리를 담당한다.

local composition과 app-wide presentation destination은 분리한다.
transient UI는 `PresentationTarget`을 사용하고, 직접 Layer를 조립할 때 `PresentationLayerLease`를 사용한다.
Placement는 Scene-authored/static topology를 표현한다.

## 2. 핵심 원칙

현재 Composition 계약은 다음 원칙을 따른다.

1. PresentationPlan은 immutable하다.
2. PresentationSession은 한 local UI world의 topology/lifetime을 소유한다.
3. UIContext logical ownership과 physical presentation 위치를 분리한다.
4. Runtime은 안정적인 PresentationHost를 소유한다.
5. transient/global UI는 PresentationTarget을 resolve해 Layer consumer lifetime을 획득·반환한다.
6. 일반 기능은 raw numeric global order를 직접 다루지 않는다.
7. backend capability는 Runtime bootstrap에서 검증한다.
8. 활성 presentation composition만 런타임에 바뀐다.
9. Unity native rendering/event/focus primitive를 재구현하지 않는다.

핵심 문장:

> Plan은 local topology를 정의하고, Context는 logical ownership을 정의하며,
> Target은 요청할 표시 대상을 정의한다. Layer Lease는 resolve된 Layer의 사용 수명을 소유하고,
> Host는 app-wide semantic destination을 Root Session Layer에 매핑한다.

## 3. 계약 밖

- 활성 Plan Layer/Placement collection의 public mutation
- session.Layers.Add/Remove/Reorder 같은 Golden Path
- Root Layout hot-swap을 navigation primitive로 사용
- custom renderer/event system/focus navigation 구현
- UITK hierarchy 재배치가 React Portal과 같은 ancestry semantic을 보장한다고 가정하지 않는다.

## 4. 런타임 구조

    UIRuntime
    ├─ RuntimeCapabilities
    │  ├─ UITK
    │  └─ UGUI
    ├─ PresentationHost
    │  ├─ Application
    │  ├─ Overlay
    │  ├─ Modal
    │  └─ System
    └─ Main UIContext
       └─ Screen / Modal / Focus / Input authority

관계는 다음처럼 분리한다.

    UIContext
        uses
    PresentationTarget
        ↓ resolve
    PresentationSession / PresentationHost
        ↓ acquire
    PresentationLayerLease
        owns
    resolved Layer consumer lifetime

    PresentationSession
        owns
    immutable PresentationPlan

PresentationSession.CreateChild(...)는 local embedded composition 전용으로 유지한다.

    Window ContentRoot
    └─ Child PresentationSession
       └─ Child UIContext

## 5. 핵심 semantic 분리

### Logical ownership

UIContext.Parent는 navigation/focus/input/lifetime authority 관계다.
physical hierarchy나 native panel 관계를 의미하지 않는다.

### Local presentation topology

resolved Plan의 `LayerOrder`와 `LocalOrder`는 한 Session scope 안의 ordering intent다.
global Host ordering과 별개다.

### Presentation destination

Built-in destination:

- Application
- Overlay
- Modal
- System

Project Composition에서는 custom destination을 정의할 수 있지만,
일반 feature가 임의 Layer topology를 직접 mutate하지 않는다.

### Presentation Layer Lease

`PresentationLayerLease`는 다음만 소유한다.

- resolved `IPresentationLayerDriver`
- 해당 Layer의 consumer lifetime

`PresentationTarget`은 Lease의 상태가 아니라 획득 요청이다. `UIContext.AcquireLayer(...)`가 Local/Host target을
resolve하며, `PresentationSession.AcquireLayer(...)`와 `PresentationHost.AcquireLayer(...)`가 실제 Layer Lease를 발급한다.
Lease 자체는 destination mapping, native surface 또는 Child `PresentationSession`을 소유하지 않는다.

## 6. Ordering 모델

ordering domain은 명시적으로 분리한다.

    Host destination identity
    != LayerOrder
    != LocalOrder
    != Window instance z-order
    != Focus precedence
    != Context authority

현재 `PresentationHost`는 destination을 Root Session Layer ID로 resolve할 뿐
별도 destination ordering graph를 소유하지 않는다.
따라서 실제 backend order는 매핑된 Root Layer의 `LayerOrder`가 결정한다.

destination 간 `Before/After` constraint와 별도 compositor ordering은 현재 계약에 포함하지 않는다.

## 7. UIContext

Child Context에 explicit PresentationSession을 연결할 때는 parent Presentation의
direct Child Session이어야 한다.

Context parent 관계 자체는 physical Presentation containment를 의미하지 않는다.

- Context parent = logical authority parent
- PresentationSession parent = local embedded presentation parent
- PresentationTarget = requested local/global destination
- PresentationLayerLease = resolved Layer usage lifetime

이 관계들은 동일한 tree나 identity를 요구하지 않는다.

## 8. Screen

Screen identity와 표시 target은 분리되어 있다.

    Screen identity
    != Presentation target
    != Presentation destination
    != Layer identity

Screen registration은 별도의 `PresentationTarget`을 저장한다.

`ScreenController.Open`은 Context를 통해 Target을 resolve하고 Layer Lease를 획득한다.
`IScreenSource`에는 최종 `IPresentationLayerDriver`만 전달한다.

    Screen registration
    └─ PresentationTarget

    ScreenController.Open
    → UIContext.AcquireLayer(target)
    → PresentationLayerLease.Layer
    → ScreenViewScope.Layer
    → IScreenSource.Acquire

`ScreenSession`은 Layer Lease를 Source/Input/child lifetime과 함께 소유한다.

## 9. Modal

ModalController의 Presentation / Interaction / Focus 분리는 유지한다.

Tier 1 Modal helper는 `PresentationTarget`을 resolve해 Layer Lease를 획득한다.

    PresentationTarget.Host(Modal)
    → UIContext.AcquireLayer(...)
    → attach backend root
    → ModalController.Open(...)
    → ModalSession owns PresentationLayerLease

## 10. Tooltip / Drag / transient overlay

Drag Visual은 현재 placement root로 target을 reparent하는
수동 Portal에 가깝다.

구성 흐름:

    logical owner
    → PresentationTarget.Host(Overlay)
    → AcquireLayer
    → backend-specific attach/reparent
    → Dispose restores/removes

UITK는 hierarchy/panel 이동 시 style ancestry, event propagation,
focus domain이 달라질 수 있다.

`PresentationLayerLease`는 hierarchy semantics를 추상화하지 않는다.
same-panel reparent와 cross-panel materialization은 backend capability로 구분한다.

## 11. Scene Fade

Scene Fade는 Root Layout의 일반 placement를 사용하지 않는다.
`SceneFader`는 Runtime-owned `PresentationHost.AcquireLayer(System)`으로 Layer Lease를 획득한다.

Runtime initialization은 System destination과 Fade Source/materializer capability를 검증한다.

## 12. Backend capability / Input bootstrap

mixed EventSystem 요구 여부는 `UISettingsAsset.BackendSupport`로 결정한다.

    Runtime infrastructure capability = bootstrap에서 고정
    active presentation composition    = runtime에서 dynamic

`PresentationBackendSupport`는 UITK/UGUI 지원을 명시하고,
UGUI capability가 켜진 Runtime은 시작 시
EventSystem/InputSystemUIInputModule을 검증한다.

첫 UGUI Layer 획득 순간 infrastructure를 임의 생성하는 방식은 Golden Path로 두지 않는다.

## 13. Focus

UIFocusDriver.BindLayer(...)의 lease 기반 등록/해제 구조는 유지한다.

Root `PresentationSession`이 Layer를 materialize할 때 Focus driver에 bind하고,
Session 종료 시 대칭적으로 unbind한다.
`PresentationHost`는 이미 materialize된 Root Layer의 Lease를 발급하고,
`PresentationLayerLease`가 해당 consumer lifetime을 소유한다.

Context authority와 physical destination order를 서로 추론하지 않는다.

## 14. Scene-authored Host

`PresentationLayerHost`는 Session-local Plan Layer materialization을 담당한다.
현재 `PresentationHost` destination도 이 Root Session Layer를 직접 가리킨다.

즉 현재 구현은:

    PresentationHost destination
    -> Root PresentationSession Layer
    -> generated 또는 scene-authored PresentationLayerHost

`PresentationHost`는 Root Session Layer만 destination으로 노출한다.
Root Session 바깥의 별도 app-wide native surface registry는 현재 계약에 포함하지 않는다.

## 15. Render space 범위

현재 Core Plan은 `PresentationBackend`만 표현한다.
다음 capability는 현재 Presentation 계약에 포함하지 않는다.

- ScreenOverlay / ScreenCamera / World render space
- camera/depth requirement
- coordinate conversion capability

지원하지 않는 render-space 의미를 LayerOrder나 destination에서 암묵적으로 추론하지 않는다.

## 16. Failure / lifetime 원칙

rollback/terminal ownership은 다음 규칙을 따른다.

Layer 획득 순서:

    PresentationTarget 또는 직접 Layer ID 입력
    → resolve local Layer 또는 Host destination
    → acquire resolved Layer consumer usage
    → return PresentationLayerLease

Screen/Modal/Drag/`AcquirePresentation` 같은 feature가 그 뒤 Source/View를 획득하며,
feature 조립이 실패하면 Source/View와 Layer Lease를 역순으로 rollback한다.
Root Layer의 native materialization과 Focus binding은 Root `PresentationSession` 조립 책임이다.

Dispose 원칙:

- 새 acquisition을 먼저 차단
- owned child/session/view를 역순 반환
- backend binding 해제
- native borrowed state 복원
- generated object 제거
- 독립 cleanup 실패를 가능한 끝까지 수집
- terminal 처리된 lifetime을 재시도 소유권으로 다시 보관하지 않음

## 17. Public API 3-tier

### Tier 1 — Feature / Golden Path

일반 기능 코드는 기능 의도와 표시 대상만 다룬다.

- `UIRuntime` / `UIContext`
- Screen / Modal facade
- `PresentationTarget` / `PresentationDestinationID`
- `UIContext.AcquirePresentation(...)`
- 반환되는 Screen/Modal/View lifetime

Tier 1은 `PresentationLayerLease`나 native Layer Root를 직접 관리하지 않는다.

### Tier 2 — Project Composition

프로젝트 조립 코드는 resolved Layer와 그 사용 수명을 직접 다룰 수 있다.

- `UIContext.AcquireLayer(PresentationTarget)`
- `PresentationSession.AcquireLayer(layerID)`
- `PresentationHost.AcquireLayer(destinationID)`
- `PresentationLayerLease`
- `PresentationSession` / Child Session / Child Context
- `ScreenRegistry` / `ModalController`
- Source 구현과 scene-authored Layer/Placement Host
- Focus Scope / Context Authority composition

Tier 2는 immutable Plan과 owner lifetime invariant 안에서 조립하며 별도 topology source를 만들지 않는다.
`UIRuntime.RootPresentation`과 Main Context가 참조하는 Root Session은 Runtime-owned이므로 직접 종료할 수 없다. 상위 composition이 Child Session을 owner-controlled lifetime으로 채택한 경우도 해당 owner 경로에서만 종료한다.

### Tier 3 — Backend Extension

backend/platform 확장은 Layer가 Unity에서 어떻게 materialize되고 동작하는지를 구현한다.

- `IPresentationLayerDriver` / `IPresentationLayerDriver<TRoot>` 구현
- native output materializer/provider
- UGUI/UITK Layer와 Output 구현
- native focus/input binding
- backend-specific interaction/transition adapter

Tier 2의 Source가 Layer Driver 계약을 소비할 수는 있지만, Driver의 backend 의미와 materialization을 구현하는 책임은 Tier 3에 있다.

## 18. Composition 계약

- ownership, local topology, requested destination, resolved Layer usage는 서로 다른 책임이다.
- Screen identity는 `PresentationTarget`이나 Layer identity가 아니다.
- Root Layout Placement는 Scene-authored/static topology만 표현한다.
- `PresentationHost`는 stable destination registry를 제공한다.
- `PresentationLayerLease`는 resolved Layer consumer lifetime만 소유한다.
- Screen registration이 `PresentationTarget`을 소유하고 ScreenController가 Context를 통해 Layer를 획득한다.
- UITK Modal, DragVisual, SceneFader는 Target → `AcquireLayer` 경로를 사용한다.
- Child Context는 logical ownership을 표현한다.
- Window embedded content는 direct Child PresentationSession containment를 유지한다.
- Host target으로 획득한 Layer Lease는 해당 feature/session lifetime에 종속된다.
- BackendSupport는 Runtime bootstrap infrastructure 계약이다.
- Scene validation은 지원 가능한 infrastructure를 검증한다.
- project-defined destination은 Settings의 명시적 mapping으로 제공한다.
- arbitrary mutable Layer graph는 public composition contract에 포함하지 않는다.

## 19. 검증 기준

자동 테스트 최소 경계:

- Layer Lease 획득·반환이 immutable Plan을 변경하지 않음
- Context parent와 physical destination이 달라도 authority 유지
- Screen Layer 획득 실패가 stack/session/source를 공개하지 않고 rollback
- Modal Layer Lease가 ModalSession과 함께 종료
- Overlay 종료 후 필요한 backend state/hierarchy 복원
- Root Session 종료는 live `PresentationLayerLease` consumer가 있으면 거부
- Focus layer binding이 materialization과 대칭
- mixed backend capability가 bootstrap contract와 일치
- Context/Runtime shutdown이 획득한 Layer Lease를 누락 없이 정리
- callback 재진입 중 topology snapshot/terminal ownership 유지
- borrowed scene surface authored state 복원

Validation Desktop 추가 시나리오:

- runtime Layer 획득 기반 overlay/modal
- Window child가 global overlay 사용
- Scene Fade가 Root Layout placement 없이 동작
- UITK-only composition과 mixed UGUI capability 구성
- sibling Context ownership + global destination에서도 Focus/Input authority 유지

## 20. 구조 원칙

PresentationSession.CreateChild(...)는 local embedded composition을 위해 유지한다.

local fixed placement가 필요한 authored/static composition은 public `UIContext.AcquirePlacement(...)`를 사용한다.
Session 내부에서는 resolved Plan placement를 통해 같은 usage lifetime을 획득한다.

Tier 1 Feature API의 Screen/Modal/Transient 경로는 `PresentationTarget`을 입력으로 받고 내부에서 Layer를 획득한다.
`PresentationLayerLease`를 직접 다루는 것은 Project Composition 경계다.
Screen identity와 Root transient slot의 암묵적 결합은 지원하지 않는다.

`UISettingsAsset`은 현재 계약만 직렬화한다.
`PresentationDestinationID`와 `BackendSupport`는 명시적으로 설정되어야 하며,
다른 설정이나 Placement 이름에서 암묵적으로 추론하지 않는다.

## 21. 보장 상태

현재 계약은 다음 상태를 보장한다.

- Root Layout의 transient Placement 없이 Overlay/Modal/System UI가 runtime Layer를 획득 가능
- local Plan topology는 immutable/deterministic 유지
- Window embedded child world semantic 유지
- Screen identity와 표시 destination 분리
- Context ownership과 physical destination 분리
- backend capability와 active composition 분리
- scene-authored/generated surface가 같은 lifetime/failure 계약 사용
- 문서, Validation, EditMode/PlayMode 테스트가 같은 구조 설명

## 22. 고정 계약

- destination identity: stable string ID
- target identity: `PresentationTargetScope.Local | Host` + ID
- Layer Lease ownership: resolved Layer driver + consumer lifetime
- Source/View ownership: `UIContext.AcquirePresentation` 또는 각 feature Session이 소유
- Root Host destination: Settings의 destination → Root Layer 명시 매핑
- local Window application: Child PresentationSession + Local Layer target 유지
- backend capability: `UISettingsAsset.BackendSupport`
- app-wide custom destination ordering은 현재 별도 mutable graph로 확장하지 않음

이 결정들은 immutable local Plan과 terminal lifetime invariant를 유지한다.
