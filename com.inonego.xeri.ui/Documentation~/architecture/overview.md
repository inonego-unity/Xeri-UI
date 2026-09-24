# Xeri UI Architecture

Xeri UI는 Unity UI Toolkit/UGUI를 대체하는 renderer가 아니다.
Unity의 rendering, event dispatch, native focus와 authoring을 사용하면서
application-level topology, lifecycle과 authority를 관리한다.

## 문서 상태

현재 구현의 상세 계약은 [Presentation Architecture](presentation.md)가 source of truth다.

[Presentation Composition Contract](presentation-composition.md)는
Presentation Target 해석과 Layer Lease 수명의 책임 경계와 검증 기준을 현재형으로 정리한다.

## 현재 핵심 구조

    PresentationLayout
    → immutable PresentationPlan
    → PresentationSession

    UIRuntime
    ├─ PresentationHost
    └─ UIContext
       ├─ Screen
       ├─ Modal
       ├─ Focus
       └─ Input authority

현재 PresentationSession은 local topology와 placement materialization을 소유하고,
Child PresentationSession으로 nested UI world를 구성한다.

UIContext는 navigation/modality/focus/input authority를 소유한다.

Window local child composition에서는 UIContext ownership tree와 PresentationSession containment를 함께 사용한다.
non-local Overlay/Modal은 Host target으로 resolve하므로 이 physical containment를 요구하지 않는다.

## Composition 축

Target/Layer Lease composition에서도 활성 Plan은 mutable graph가 아니다.

    logical ownership
        UIContext

    local topology
        PresentationSession + immutable Plan

    requested destination
        PresentationTarget

    resolved Layer usage
        PresentationLayerLease

    app-wide destination resolver
        PresentationHost

기본 semantic destination은 다음과 같다.

- Application
- Overlay
- Modal
- System

transient UI를 위해 Root Layout에 fixed placement를 미리 예약하지 않는다.
필요한 UI는 `PresentationTarget`을 resolve해 해당 Layer의 사용 수명을 획득한다.

## 책임 경계

| 책임 | 의미 |
|---|---|
| Definition | local UI world에 무엇이 존재하고 어떤 순서인지 정의 |
| Session | immutable Plan의 활성 materialization/lifetime 소유 |
| Context | navigation, modality, focus, input과 logical ownership 소유 |
| Target | local Layer 또는 app-wide destination을 요청하는 논리적 표시 대상 |
| Layer Lease | resolve된 Layer의 consumer lifetime 소유 |
| Host | app-wide destination을 Root Session Layer로 resolve |
| Source | View를 어디서 acquire/release하는지 소유 |
| Backend | UITK/UGUI native output, focus/input adapter 제공 |

`PresentationHost`와 `PresentationLayerLease`는 Project Composition API다.

## Presentation ordering

ordering domain을 서로 추론하지 않는다.

    Host destination identity
    != Presentation LayerOrder
    != Presentation LocalOrder
    != Window instance z-order
    != Focus precedence
    != Context authority

LayerOrder와 LocalOrder는 Session-local Presentation topology의 값이다.

Host destination 자체에는 별도 ordering graph가 없다. 실제 backend order는 destination이 가리키는 Root Layer의 `LayerOrder`가 결정한다.

## Scene-authored UI

현재 PresentationLayerHost와 PresentationPlacementHost는
Session Plan의 physical materialization을 명시적으로 제공한다.

Scene global implicit discovery는 하지 않는다.

app-wide Host destination mapping은 `UISettingsAsset`의
`PresentationDestinationDefinition`이 소유하며 Session-local Scene Host와 분리한다.

    PresentationHost destination
    != PresentationPlan Layer identity

## Screen / Modal / transient UI

Screen identity와 표시 target은 분리되어 있다.

    Screen identity
    != PresentationTarget
    != Presentation destination
    != Layer identity

ModalController는 Presentation/Interaction/Focus 책임을 분리하고,
Tier 1 helper는 `PresentationTarget`을 받아 내부에서 `UIContext.AcquireLayer`로 실제 Layer를 획득한다.

Drag Visual, Modal, Scene Fade 같은 transient/system UI는
고정 Root placement 대신 명시적 Local/Host target을 사용한다.

## Focus와 Input

Focus precedence는 numeric priority가 아니라 ownership이다.

    Override.Top
    → Primary
    → Base

Gameplay Input과 Cursor policy는 effective Context path와 Screen ownership을 기준으로 합성한다.

presentation destination의 앞뒤 순서를 Focus/Input priority로 사용하지 않는다.

backend infrastructure capability는 active composition과 분리되어 있다.

Runtime은 `UISettingsAsset.BackendSupport`로 지원 backend를 bootstrap에서 검증하고,
active Layout만 보고 mixed EventSystem 필요 여부를 추론하지 않는다.

## Window

Window는 Presentation 위에 별도의 application-level window semantics를 제공한다.

- move / resize
- minimize / maximize / restore
- focus / close
- instance z-order
- optional AlwaysOnTop stack group
- persistence record
- Simple Window
- Application Window

Application Window는 ContentRoot 안에서 Child PresentationSession + Child UIContext 모델을 사용한다.

이 local embedded composition은 Host target 기반의 non-local Layer 획득으로 대체할 대상이 아니다.

## Public API 방향

Golden Path는 UIRuntime, UIContext, Screen/Presentation/Modal facade와
XeriWindowWorkspace에서 시작한다.

다음 progressive disclosure를 따른다.

    Tier 1 — Feature / Golden Path
            ↓
    Tier 2 — Project Composition
            ↓
    Tier 3 — Backend Extension

`PresentationTarget`은 Golden Path의 표시 대상 vocabulary다.
Project Composition에는 `PresentationHost`, `PresentationLayerLease`, `AcquireLayer` 계열 API가 포함된다.
일반 사용자가 native Panel/Canvas ordering을 직접 관리하는 Golden Path는 만들지 않는다.

Convenience API는 하위 primitive를 조합하는 얇은 facade이며
별도 state/order/lifetime source of truth를 만들지 않는다.

## 설계 원칙

- immutable local Plan을 유지한다.
- local Child Session과 Host target 기반의 non-local Layer 획득을 구분한다.
- logical ownership과 physical placement를 같은 tree로 강제하지 않는다.
- Unity native 기능으로 동일 contract를 만족하면 중복 subsystem을 만들지 않는다.
- rollback/terminal lifetime semantics를 composition 확장에서도 유지한다.
- Tier 1 API와 Project Composition/static authored Placement 경계를 문서에서 명확히 구분한다.

## 더 자세히

- [Presentation Architecture](presentation.md)
- [Presentation Composition Contract](presentation-composition.md)
- [Core 개요](../core/index.md)
- [Window](../window/index.md)
