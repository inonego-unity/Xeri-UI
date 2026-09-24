# Presentation Migration Plan

> 상태: Current → Target Migration Plan
> 갱신일: 2026-10-02
> 기준 Target: `../../com.inonego.xeri.ui/Documentation~/architecture/presentation.md`
>
> 이 문서는 현재 구현, 제거할 레거시, 단계별 변경과 검증을 관리한다.
> 최종 계약 자체는 `../../com.inonego.xeri.ui/Documentation~/architecture/presentation.md`를 따른다.

## 0. 목적

이 Plan의 목적은 현재 Xeri UI를 Target Contract에 맞추되 다음을 피하는 것이다.

- old/new dual source
- compatibility shim 장기 유지
- Unity native 기능의 중복 구현
- 실제 사용처 없는 speculative public API
- 현재 구현 세부가 Target 의미를 결정하는 구조

완료 판단은 "새 코드가 추가됨"이 아니라 **Target invariant가 old path 없이 단일 경로로 성립함**이다.

## 1. 2026-10-02 현재 확인 상태

### Presentation Layout / serialization

현재 `PresentationLayout`의 public/runtime authoring model은 다음만 가진다.

- LayerID
- DisplayName
- LayerOrder
- Backend
- PresentationID
- placement LayerID
- LocalOrder

`PresentationBackendSerialization`은 기존 asset의 `outputRequirement.backend` 경로를 유지하기 위한 private serialization payload다.
public RenderSpace / DepthMode 계약은 아직 없다.

Package Validation Layout과 KnackH 프로젝트 Layout에 남아 있던 legacy `renderSpace` / `depthMode` YAML은 제거했다.
따라서 World/SceneDepth는 Phase 7의 미래 Target requirement이며 현재 구현으로 간주하지 않는다.

### Top-level materialization

Generated Layer는 `PresentationLayerMaterializer` 경로로 materialize한다.

```text
PresentationPlanLayer
→ backend별 generated Native Output
→ LayerRoot / ManagedRoot
→ RuntimeLayer
```

UITK generated output은 `PanelRenderer + runtime cloned PanelSettings`를 사용하고
`PanelSettings.sortingOrder = LayerOrder`로 native ordering을 적용한다.

Scene-authored Layer는 explicit `PresentationLayerHost`를 사용한다.

- UGUI: Canvas / `UGUIPresentationOutput`
- UITK: PanelRenderer / `UITKPresentationOutput` / `VisualElementReference`
- LayerRoot 아래 distinct descendant `ManagedRoot`

Session은 Scene Host 자체를 파괴하지 않고 자신이 추가한 child와 runtime native state만 복원한다.
UITK borrowed output은 PanelSettings / VisualTreeAsset identity를 교체하지 않고 `sortingOrder`만 scoped 적용·복원한다.
Generated UITK Layer만 template에서 Runtime PanelSettings를 clone한다.

`PresentationOutputMaterializer`, `PresentationOutputBinding` legacy symbol/path는 현재 working tree에 없다.

### PresentationSession / Host

현재 Session activation은 Plan을 먼저 기준으로 LayerHost/PlacementHost map을 검증한 뒤 materialize한다.

```text
PresentationPlanLayer
+ optional PresentationLayerHost
→ RuntimeLayer

PresentationPlanPlacement
+ optional PresentationPlacementHost
→ Runtime Placement Root
```

현재 보장되는 validation:

- Plan에 없는 Host ID 거부
- duplicate LayerHost / PlacementHost ID 거부
- backend mismatch 거부
- PlacementHost가 다른 Layer에 속하면 거부
- LayerHost ManagedRoot는 LayerRoot 아래 distinct descendant
- PlacementHost root는 ManagedRoot direct child
- generated/provided placement 혼용
- Plan LocalOrder authoritative
- activation 실패 rollback
- Scene-authored external lifetime 비파괴

`PresentationLayerRegistry`, `PresentationSurface` concrete type은 아직 내부 invariant owner로 남아 있으며 Phase 12 audit 대상이다.

### Common / Composition API

Target의 progressive disclosure 1차 적용이 들어간 상태다.

Common / Golden Path:

- `UIContext.RegisterScreen(...)`
- `UIContext.AcquirePresentation(...)`
- `UITKModal.Open(context, presentationID, root)`
- `UITKModal.OpenWithFocus(...)`
- `XeriWindowWorkspace(parentContext, presentationID, ...)`

Project Composition / advanced primitive:

- `ScreenRegistry.Register(...)`
- `PresentationLease.Acquire(...)`
- `ModalController.Open(...)`
- Focus Scope / Context Authority API
- `PresentationSession.CreateChild(...)`
- Layer/Placement Host

Common façade는 별도 registry/state/order/lifetime source를 만들지 않는다.
`UITKModal`은 placement usage와 hierarchy lifetime을 기존 `ModalController`로 이전하고,
자신이 변경한 borrowed `VisualElement.enabledSelf` / `pickingMode`도 종료·실패 시 복원한다.

### Screen / Input / Focus

`ScreenOptions`는 Target 7-field 형태다.

- ID
- DuplicatePolicy
- BlocksGameplayInput
- ShowsCursor
- CursorLockMode
- OpenDuration
- CloseDuration

DefaultFocus / InputPriority / UsesUnscaledTime legacy path는 code/test/sample에서 제거됐다.
Screen ID는 같은 ID의 Presentation placement로 resolve된다.

Input은 numeric priority나 acquisition-sequence winner를 사용하지 않는다.

- active Context path 기반 gameplay contribution
- top Screen / authority 기반 Cursor owner
- input-release barrier
- input device tracking

UITK-only Runtime은 EventSystem/InputSystemUIInputModule을 필수로 하지 않는다.
UGUI Layer가 포함된 mixed Layout에서만 같은 Host의 EventSystem/InputSystemUIInputModule을 검증한다.

Screen default focus는 Driver/`IFocusScope.DefaultFocus` 하나를 사용하며,
logical precedence는 `Override → Primary → Base`다.

### Window

현재 public Workspace constructor는 PresentationID 중심이다.

```text
XeriWindowWorkspace(parentContext, presentationID, ...)
```

Workspace는 내부 Screen policy를 숨기고 Child UIContext에 internal Workspace Screen을 등록한다.

Simple Window:

- Workspace Context에 persistent Focus Scope 등록
- 별도 Child PresentationSession/UIContext 없음

Application Window:

```text
XeriWindowPanel.ContentRoot
→ Child PresentationSession
→ Child UIContext
```

`ContentSlot`과 `IXeriUIViewSource.CreateView` legacy naming은 제거됐고
Window View Source는 `AcquireView / ReleaseView`를 사용한다.

남은 Window audit:

- internal Workspace host의 Core Screen 사용 여부
- StackLayer의 stable public requirement 여부
- open transaction/resource ownership bundle 단순화

### Public docs / API docs

Core/Window/View 공개 위키를 현재 Layout/Plan/Session/Host와 Common/Composition API 기준으로 갱신했다.

공개 문서의 old Profile/LayerAsset/old ScreenOptions/CreateView 실사용 예제는 제거됐다.
DocFX full metadata/site build와 generated API snapshot 갱신이 성공했으며,
`UIContext.RegisterScreen`, `UIContext.AcquirePresentation`, `UITKModal`이 generated API에 포함된다.

### Tests / validation

현재 추가된 Common API 계약 테스트는 다음을 고정한다.

- RegisterScreen façade와 ScreenRegistry 등록 lifetime 동등성
- AcquirePresentation façade와 PresentationLease lifetime 동등성
- UITK Modal placement usage / hierarchy ownership
- Focus Scope 재사용
- `FocusController.PushOverride`의 native Select 실패 시 override rollback과 즉시 재시도 가능
- `UITKModal.OpenWithFocus`의 Focus 적용 실패 시 Modal stack / Focus / hierarchy / placement usage 전체 rollback
- borrowed interaction state 복원
- UGUI placement를 UITK helper로 열 때 rollback / usage leak 없음
- Composition overload의 caller lifetime 이전

C# checker/review와 Unity-generated project graph 기반 Runtime/Edit/Play/Sample compile은 통과했다.
추가 확인 중 `UITKModal.cs.meta`가 처음 생성될 때 literal `\\n`을 포함한 malformed 1-line meta였던 문제를 찾아
같은 GUID를 보존한 정상 2-line `.meta`로 수정했고, package 전체 malformed meta 검색은 0건이다.

KnackH와 별도의 clean Unity 6000.7.0a6 validation project에서 local Xeri/Xeri.UI package와 동일 DOTween dependency를 연결해
실제 Unity Test Runner를 실행했다.

- EditMode: 356 total / 356 passed / 0 failed
- PlayMode: 29 total / 19 passed / 0 failed / 10 skipped
- PlayMode skip 10건은 Window Manual 9건 + 실제 GameView pixel interleave Manual 1건
- Scene-authored UITK Host targeted PlayMode: 2 / 2 passed
- Common API / Modal / Focus rollback tests 포함

초기 `-nographics` EditMode 시도에서 PanelRenderer Root가 없어 UITK 테스트가 실패했지만,
그래픽 디바이스가 있는 batchmode 재실행에서는 356/356이 통과했다.

현재 열려 있는 KnackH Unity Editor는 사용자 EditorPrefs의 Auto Refresh가 꺼져 있어
(`kAutoRefresh = 0`) 그 Editor의 generated runtime csproj / ScriptAssemblies에는 새 production source가 아직 반영되지 않았다.
Package correctness는 별도 clean Unity Test Runner로 검증 완료했으며, KnackH Editor에서 현재 변경을 사용하려면
명시적 `Assets > Refresh` 또는 동등한 AssetDatabase Refresh가 한 번 필요하다.

### Validation environment

현재 수동/integration 검증 환경은 아직 둘로 나뉘어 있다.

- `Samples~/GameUIValidation`
- `Runtime/Window/TEST/PLAY/Manual`

다음 구조 작업은 두 harness를 하나의 package 기본 OS형 Validation Scene으로 통합하고,
자동 correctness 테스트와 수동 visual/interaction 검증의 책임을 분리하는 것이다.

### KnackH visual backend

현재 KnackH Layout은 다음 LayerOrder/backend를 사용한다.

```text
200    CombatFeedback   UGUI
250    WorldOverlayUI   UITK
300    GameplayHUD      UITK
1000   MainScreen       UITK
2000   Modal            UITK
10000  SystemFade       UGUI
11000  Overlay          UITK
```

현재 Layout에는 RenderSpace/DepthMode 필드가 없다.
EntityStatus는 이 Layout 밖의 별도 World-space Canvas/UGUI 경로다.

## 2. 확정 제거 / 교체 결정

| 현재 요소 | Target | 상태 |
| --- | --- | --- |
| `ScreenOptions.InputPriority` | Context/Screen ownership 기반 Cursor resolution | **완전 제거** |
| driver InputPriority 비교 | owner topology 기반 resolution | **완전 제거** |
| cursor sequence tie-break | release-retention 외 winner selection 제거 | **완전 제거** |
| `ScreenOptions.DefaultFocus` | Driver/IFocusScope.DefaultFocus 단일 source | **완전 제거** |
| `ScreenOptions.UsesUnscaledTime` | Screen navigation 항상 unscaled | **완전 제거** |
| `ContentSlot` naming | `ContentRoot` | **완전 제거** |
| persistent Group + LateUpdate suppression | scoped leaf Modifier Lease | **경로 교체** |
| CombatFeedback Alpha polling | explicit target-changed event | **경로 교체** |
| UIDocument 기반 UITK output | PanelRenderer direct path | **교체 완료** |
| `PresentationOutputBinding` 중간 abstraction | `PresentationLayerHost` + LayerRoot + ManagedRoot | **완전 제거** |
| Placement Root 항상 runtime 생성 | optional `PresentationPlacementHost` + generated fallback | **구현 완료** |
| Runtime 필수 EventSystem/InputSystemUIInputModule | UITK-only native event system | **UITK-only hard dependency 제거 완료** |
| full custom Xeri visual preview | Unity in-scene authoring + semantic debug | **기본 목표 제거** |
| speculative Root Layout hot-swap | v1 root lifetime immutable | **public Target에서 제거** |

## 3. 유지 결정

다음은 제거 대상이 아니다.

### DuplicatePolicy.Allow

실제 Screen stack에서 동일 ID live instance를 여러 개 허용하는 독립 semantic이다.

Sample 사용처가 있고 topology 중복이 아니다.

### XeriWindowOptions.CanFocus

non-focusable floating Window라는 독립 Window behavior다.

Focus precedence를 중복 표현하지 않는다.

### Window coarse z-group

Normal / AlwaysOnTop은 instance order와 다른 coarse Window ordering domain이다.

다만 stable public requirement인지 실제 consumer 기준으로 audit한다.

### ScreenOpenParams.Payload

per-open caller data 전달이라는 독립 역할이다.

KnackH는 현재 사용하지 않지만 Validation Sample에서 실제 path가 있다.

### PresentationGroup

one-shot composite/snapshot/transition 용도로 유지한다.

persistent suppression owner로만 사용하지 않는다.

## 4. 추가 audit 대상

### PresentationLayerRegistry

확인할 것:

- RuntimeLayer dictionary/list와 실질 state 중복 여부
- consumer count / release validation을 별도 Registry 타입이 가져야 하는지
- Session 내부 state로 통합했을 때 테스트/failure boundary가 더 명확한지

결론은 concrete 타입 유지가 아니라 invariant 유지로 판단한다.

### PresentationSurface

확인할 것:

- embedded Layer root 생성/lifetime만 남는지
- Unity native hierarchy + Session이 같은 책임을 더 직접 표현하는지
- 별도 public/internal type이 독립 실패 경계를 실제로 소유하는지

### Native Focus bridge

확인할 것:

- Unity Focus events로 LastFocus/containment 관찰이 충분한지
- Panel별 callback/ref-count가 실제로 필요한지
- LateUpdate reconciliation이 재현 가능한 race를 해결하는지

### Window Workspace host

현재 Workspace는 Container placement/lifetime을 위해 internal Core Screen을 연다.

검토 질문:

- 이 Screen이 실제 navigation state인지
- 아니면 Presentation acquisition host를 Screen으로 재사용한 것뿐인지
- Simple Window cursor/input policy 때문에 Screen semantic이 실제 필요한지
- PresentationLease + explicit workspace policy가 더 단순한지

Target에서는 이 구현 세부를 public API로 노출하지 않는다.

### AlwaysOnTop

현재 production consumer보다 tests 중심이다.

desktop-like Window feature requirement가 유지되면 보존하고,
실제 supported use-case가 없으면 stable public API 전에 제거한다.

## 5. Phase 1 — Screen policy cleanup

### 1.1 InputPriority 완전 제거

제거 범위:

- `ScreenOptions.InputPriority`
- constructor `inputPriority`
- 모든 named argument caller
- driver priority branch
- cursor winner selection용 `SessionState.Sequence` / `nextSequence`
- release-barrier cursor owner도 acquisition sequence가 아니라 Screen lifecycle owner가 명시적으로 제공하도록 변경
- tests / samples / docs
- deprecated alias / compatibility fallback

Cursor resolution을 다음으로 교체한다.

```text
release-retention override
→ Effective Context Active Top Screen
→ nearest active ancestor owner
→ baseline
```

검증:

- Covered Screen은 Cursor owner가 아님
- inactive sibling Context는 기여 안 함
- Application Window authority 전환 시 Cursor owner도 같이 이동
- release barrier 동안 closing Screen cursor retention 유지

### 1.2 ScreenOptions.DefaultFocus 제거

변경:

- property/constructor parameter 제거
- FocusController.Activate 인자 단순화
- record.Default의 Screen-specific duplicate source 제거
- Driver/IFocusScope.DefaultFocus만 사용

KnackH는 이미 Presenter → Driver DefaultFocus 경로를 사용하므로 production migration 영향이 작다.

검증:

- first open → Driver default
- covered → restore → LastFocus
- invalid LastFocus → Driver default
- no default → null 허용
- Window/Application Screen parity

### 1.3 UsesUnscaledTime 제거

Screen navigation은 항상 unscaled transition을 사용한다.

제거:

- ScreenOptions property/constructor
- ScreenController 전달 branch
- ScreenOptions tests/docs

유지:

- generic `PresentationTransitionParams.UsesUnscaledTime`

### 1.4 ScreenOptions 최종 형태

```text
ID
DuplicatePolicy
BlocksGameplayInput
ShowsCursor
CursorLockMode
OpenDuration
CloseDuration
```

## 6. Phase 2 — Window naming / API cleanup

### 2.1 ContentRoot

rename:

- `XeriWindowPanel.ContentSlot` → `ContentRoot`
- backing field
- UXML element name이 public semantic과 연결돼 있으면 함께 정리
- Application Window Child Session attach caller
- tests / docs / samples

old symbol alias를 남기지 않는다.

### 2.2 Window View Source naming

`CreateView` → `AcquireView`

`ReleaseView` 유지.

Scene-authored borrowed View도 같은 naming으로 표현 가능하게 한다.

### 2.3 Workspace constructor

일반 API에서 raw ScreenOptions를 제거한다.

후보:

```text
XeriWindowWorkspace(
    UIContext context,
    string presentationID,
    XeriWindowWorkspaceOptions options = ...)
```

advanced/internal composition에서만 Screen policy를 직접 설정한다.

### 2.4 Workspace internal Screen audit

Screen host 유지/제거는 2.3과 별도로 판단한다.

public API에서 숨긴다고 내부 Screen을 반드시 제거할 필요는 없다.

실제 lifecycle/input 의미를 비교해 더 단순한 쪽을 선택한다.

## 7. Phase 3 — Presentation state cleanup

### 3.1 Dialogue suppression

KnackH:

- `GameplayUIComposition.LateUpdate()` suppression 재적용 제거
- `PresentationGroup` persistent owner 제거
- target leaf마다 Alpha modifier lease 등록
- dispose 시 modifier 제거

### 3.2 Dynamic CombatFeedback target

polling 대신 target-changed notification을 만든다.

active suppression이 있으면 새 target에 modifier를 등록한다.

old target modifier는 해제한다.

### 3.3 PresentationGroup boundary

tests로 보장:

- one-shot Apply 결과
- local state 보존
- group 자체가 reactive subscription을 만들지 않음
- persistent modifier와 Group snapshot 사용이 충돌하지 않음

## 8. Phase 4 — UITK Native Output

### 4.1 PanelRenderer direct path

교체:

```text
UIDocument
+ UITKPresentationOutput
```

→

```text
PanelRenderer
+ runtime PanelSettings
+ Xeri layer root
```

검증:

- sorting order
- root lifetime
- reload callback
- baseline style 적용
- dispose/rollback

### 4.2 UITKPresentationOutput

direct PanelRenderer 전환 후 역할이 사라지면 제거한다.

compatibility 이유가 실제로 남으면 optional adapter로 격리한다.

### 4.3 Session-local state audit

PanelRenderer migration과 함께:

- RuntimeLayer
- PresentationLayerRegistry
- PresentationLayerHandle
- PresentationSurface

중복 state를 audit한다.

한 번에 구조를 없애는 것이 목적이 아니라 최소 invariant owner를 찾는다.

## 9. Phase 5 — LayerHost / PlacementHost materialization

**현재 상태: 핵심 구현 완료, Unity EditMode/PlayMode Test Runner 검증 완료.**

output-level `PresentationOutputBinding` 중간 경로는 제거됐고,
Generated/Scene-authored Layer가 같은 RuntimeLayer 계약으로 수렴한 상태다.

### 5.1 PresentationLayerHost

Target:

```text
PresentationPlanLayer
+ optional PresentationLayerHost
→ RuntimeLayer
```

Scene-authored Host는 다음 materialization boundary를 제공한다.

- stable LayerID
- 현재 public model에서는 backend requirement를 만족하는 Native Output
- RenderSpace requirement는 Phase 7에서 실제 API가 생길 때 확장
- LayerRoot
- ManagedRoot

ManagedRoot 아래만 Xeri Placement ordering/lifetime 영역으로 사용한다.

Scene-authored background/frame/decoration 등 unmanaged content는 LayerRoot에 남기고
LocalOrder 적용 때문에 sibling order가 변경되지 않게 한다.

Host가 없으면 generated materializer가 동등한 결과를 만든다.

```text
Generated
→ Native Output + LayerRoot + ManagedRoot 생성
→ Session owns all

Scene-authored Host
→ existing Native Output + LayerRoot + ManagedRoot borrow
→ Session owns only runtime child/temp state
```

### 5.2 PresentationPlacementHost

특정 Presentation의 geometry/mount point까지 Scene에서 authoring해야 하는 요구를
optional `PresentationPlacementHost`로 표현한다.

Target:

```text
PresentationPlanPlacement
+ optional PresentationPlacementHost
→ Runtime Placement Root
```

PlacementHost는 physical root만 제공한다.

절대 제공하지 않는 것:

- Presentation 존재 여부
- LayerID override
- LocalOrder override
- backend override

이 값들의 source of truth는 Layout/Plan 하나다.

같은 PlacementHost가 없으면 ManagedRoot 아래 generated Placement Root를 사용한다.

### 5.3 Explicit composition

Host는 Scene global search로 발견하지 않는다.

UIRuntime 또는 상위 composition edge가 Host collection을 명시적으로 Session activation에 전달한다.

검증:

- 같은 Layout/Plan을 generated LayerHost와 Scene-authored LayerHost 각각으로 활성화
- Scene-authored Host 외부 lifetime 비파괴
- Session dispose 후 임시 native state 복원
- LayerRoot unmanaged authored content 유지
- ManagedRoot 아래 generated/provided Placement 혼용
- PlacementHost가 Plan에 없는 PresentationID를 가리키면 activation 전 거부
- PlacementHost가 다른 Layer subtree에 있으면 거부
- Layout LocalOrder가 authored sibling order보다 authoritative
- 현재 단계의 invalid backend/root capability 거부
- Phase 7 이후 render-space capability validation 추가
- duplicate LayerHost / PlacementHost ID 거부
- rollback leak 없음

금지:

- Layout/Plan에 Scene Object 저장
- LayerHost/PlacementHost가 topology/order source가 되는 구조
- Scene global discovery
- missing Host silent search fallback
- output-only `PresentationOutputBinding` compatibility alias를 최종 API에 유지

## 10. Phase 6 — Scene-authored View

Scene-authored View 때문에 Xeri Core에 generic borrow/restore Source 구현을 선제 추가하지 않는다.

Target contract는 기존 Source abstraction이 externally-owned View를 acquire/release할 수 있다는 의미만 유지한다.
실제 consumer에서 Scene-authored View 요구가 확인되면 project/domain Source 또는 최소 adapter로 구현하고,
반복되는 공통 invariant가 확인된 뒤에만 Core helper 승격을 검토한다.

검증:

- Layout이 Scene View reference를 직접 소유하지 않음
- Scene global implicit discovery 없음
- Scene hierarchy sibling order를 LocalOrder source로 사용하지 않음
- 실제 consumer가 borrow path를 도입할 때 external ownership을 파괴하지 않음

## 11. Phase 7 — World / SceneDepth

Target requirement를 실제 API로 도입한다.

- RenderSpace
- DepthMode
- backend capability validation

UITK:

- WorldSpace PanelSettings
- PanelRenderer transform/size/pivot
- 필요한 경우 PanelInputConfiguration

KnackH EntityStatus로 검증:

- occlusion
- scale
- camera relation
- many entity instances
- single renderer vs shared PanelSettings/multi-renderer

native 결과로 충분하면 custom depth compositor를 만들지 않는다.

## 12. Phase 8 — Focus / Input native-first

**현재 상태: Screen/Input 핵심 migration 완료, native Focus 세부 audit 지속.**

- numeric InputPriority / sequence winner 제거 완료
- UITK-only EventSystem/InputModule hard dependency 제거 완료
- Context active path / Cursor owner policy 적용 완료
- Driver/IFocusScope DefaultFocus 단일 source 적용 완료
- native Focus event bridge의 최소 형태는 유지하되 불필요한 callback/state가 더 있는지 audit 지속

### Focus

- Unity FocusController 우선
- custom Panel callbacks 최소화
- LateUpdate reconciliation 제거 가능성 검증
- same PanelSettings common focus context 활용

### Input

- UITK event dispatch를 Unity에 위임
- gameplay/cursor/context contribution policy만 Xeri가 유지
- UITK-only Runtime의 EventSystem/InputModule hard requirement 제거
- UGUI mixed adapter 분리

## 13. Phase 9 — KnackH visual migration

### SystemFade

기존 UITK SceneFade Source/Driver 경로로 전환.

### CombatFeedback

UGUI/TMP per-character implementation을 실제 UITK 표현으로 이식한 뒤 backend 전환.

Layer enum만 바꾸지 않는다.

### EntityStatus

World-space Canvas/UGUIBar를 UITK World 후보로 migration.

### WorldOverlayUI

현재 실제 ScreenOverlay projection UI와 World UI naming을 구분한다.

## 14. Phase 10 — Editor integration

Unity native authoring을 우선한다.

Xeri Editor:

- Layout tree
- Layer/placement validation
- Plan debug
- provided binding diagnostics
- authored UI navigation

full custom preview stage는 만들지 않는다.

## 15. Phase 11 — Peripheral audit

### Tray

- ListView로 대체 가능한 generic list/reorder 범위 확인
- horizontal taskbar 요구만 custom 유지

### Bar

- ProgressBar로 충분한 단순 case 확인
- delta / lag / 4-direction만 custom semantic 유지

### Window StackLayer

- AlwaysOnTop actual consumer 확인
- requirement 없으면 stable API 전에 제거
- 유지하면 Window-only coarse ordering으로 문서화

## 16. Root Layout hot-swap 정리

현재 public Target에서 제거한다.

해야 할 일:

- Target 문서/주석에서 이미 구현된 기능처럼 보이는 표현 제거
- 실제 public API가 없다면 misleading comment 정리
- Child Session이 해결하는 topology variation과 구분

향후 concrete use-case가 생기기 전까지 새 public API를 추가하지 않는다.

## 17. Phase 12 — Object Model / Orchestration Cleanup

객체를 더 많이 만드는 것이 목적이 아니다.
독립된 invariant / lifetime / failure boundary가 있는 책임만 분리하고,
단지 테스트하기 쉽다는 이유로 public/internal interface나 manager/factory를 추가하지 않는다.

### Public API tiering / progressive disclosure

Target public surface는 기능을 일괄 축소하지 않고 세 레벨로 정리한다.

```text
Common / Golden Path
→ Project Composition
→ Backend Extension
```

원칙:

- Common API는 기존 primitive를 호출하는 얇은 facade로만 추가한다.
- facade가 별도 state, ordering, lifetime source of truth를 만들지 않는다.
- Project Composition API는 supported advanced API로 유지한다.
- Backend API는 실제 backend/platform 교체 boundary를 표현하는 범위에서 유지한다.
- advanced API를 사용해도 Layout/Plan topology, Session lifetime, Context authority invariant는 우회할 수 없다.
- 일반 사용자 문서에서는 Common API를 먼저 보여주고 Composition/Backend API는 별도 advanced 섹션으로 내린다.

현재 적용:

- `UIContext.RegisterScreen(...)` → `ScreenRegistry.Register(...)`의 lossless facade
- `UIContext.AcquirePresentation(...)` → 현재 Context의 `PresentationSession`을 사용하는 lossless facade
- `UITKModal.Open(context, presentationID, root)` / `OpenWithFocus(...)`
  → placement usage + hierarchy lifetime + UITK adapter 조립을 기존 `ModalController.Open(...)`으로 위임

Composition 레벨에서는 `UITKModal.Open(context, root, ownedLifetimes)` /
`OpenWithFocus(...)`로 이미 조립된 Root와 lifetime을 직접 넘길 수 있다.
Modal helper는 자신이 변경한 borrowed root의 `enabledSelf` / `pickingMode`를
전달 lifetime 정리 뒤 마지막에 복원하여 caller cleanup이 원래 상태를 덮어쓰지 못하게 한다.
그 아래의 `ScreenRegistry`, `PresentationLease.Acquire(...)`,
`ModalController.Open(IPresentation, IModalInteractionDriver, ...)`도 advanced primitive로 유지한다.

하지 않을 일:

- Registry/Session/Focus/Authority primitive 삭제
- 모든 설정을 하나의 거대 options 객체로 흡수
- advanced 사용을 위해 raw mutable state를 노출
- convenience overload 때문에 backend-specific 옵션을 Core common API에 계속 누적

### ScreenController

`ScreenController`는 Screen stack aggregate/application service로 유지한다.
다음 semantic은 계속 소유한다.

- Open / Replace / Close / Clear command
- Active / Covered / Closing stack state
- current top과 duplicate policy 적용

반면 Source / Presentation usage / Input session / child lifetime / rollback ordering처럼
하나의 Screen Session에 함께 묶이는 resource choreography는 `ScreenSessionResources` 또는 동등한 internal ownership object로 이동 가능한지 검토한다.

분리 기준:

- Controller가 각 resource의 release 순서를 직접 반복 관리하지 않게 할 것
- rollback과 정상 release가 같은 ownership object의 대칭 경로를 사용할 것
- `ScreenOpenManager`, `ScreenCloseManager`처럼 메서드별 manager를 만들지 않을 것

### UIRuntime

UIRuntime은 Composition Root 역할을 유지한다.
UITK native input/focus 전환 후에도 Context Authority 계산이 독립 invariant로 충분히 남는다면
`Base / Override Stack / Effective / Active Path`를 internal authority object로 분리하는 것을 검토한다.

단순 Host validation helper만을 위한 새 service는 만들지 않는다.
backend requirement는 가능한 한 해당 backend/composition boundary가 검증한다.

### XeriWindowWorkspace

Workspace는 public facade로 유지한다.
View acquire, Panel/Controller 조립, Registry 등록, Application Child Session/Context 생성과 rollback을
하나의 open transaction/lifetime bundle로 묶을 수 있는지 검토한다.

목표:

- Open 실패 cleanup과 정상 Release가 같은 ownership graph를 사용
- Workspace가 모든 세부 release 순서를 직접 기억하지 않음
- public factory/service 계층을 추가하지 않고 internal composition helper로 제한

### ModalController

parameterless `ModalController()`가 production에서 실제 standalone capability를 제공하는지 audit한다.
Focus 없는 Modal도 UIContext-owned controller에서 열 수 있으므로,
실제 요구가 없으면 `UIContext`가 항상 ModalController를 소유하는 단일 invariant로 정리한다.

테스트 편의를 위해 context-less production constructor를 유지하지 않는다.

### Window public event surface

`OnPreMinimize/OnMinimize`, `OnPreMaximize/OnMaximize`, `OnPreRestore/OnRestore` 등
상태별 event가 실제 외부 consumer 계약인지 audit한다.

이미 `XeriWindowStateCommandKind/Request`와 state-change 모델이 있으므로,
외부 사용 근거가 약하면 `StateChanging / StateChanged` 같은 더 응집된 contract로 축소 가능한지 검토한다.
`OnPreClose`처럼 cancel 의미가 실제 소비되는 이벤트는 별도 계약으로 남을 수 있다.

### XeriWindowOptions

mutable struct 복사 semantics와 `default` vs `Default()` 차이를 audit한다.
Inspector/serialization 요구를 해치지 않는 범위에서 immutable value-object 형태가 더 안전한지 검토한다.

### Interface audit

각 interface는 다음 중 하나 이상이 있을 때만 유지한다.

- 실제 production 구현이 둘 이상 존재
- backend/platform 교체 boundary
- 독립 lifetime/failure boundary를 caller가 의존
- package 외부 구현을 의도적으로 허용하는 public extension point

`production 구현 1개 + test fake만 존재`가 유일한 이유라면 concrete/internal seam으로 축소 가능한지 검토한다.

### Assembly boundary

현재 단일 Runtime assembly는 UGUI optionalization 시 compile-time dependency 격리를 방해할 수 있다.
실제 migration 단계에서 최소 경계로 다음을 검토한다.

```text
Xeri.UI.Core
Xeri.UI.UITK
Xeri.UI.UGUI   // optional
```

Window/Tray까지 기계적으로 asmdef를 늘리지 않는다.
실제 dependency isolation 요구가 있는 경계만 분리한다.

## 18. Test Structure / Naming / Region Rules

테스트 구조 정리는 테스트 수를 늘리는 작업이 아니다.
각 테스트가 어떤 contract를 검증하는지, 어느 assembly/runtime 환경을 요구하는지,
실패 시 어떤 invariant가 깨졌는지가 바로 보이도록 정리한다.

### Assembly / folder layout

Xeri UI 로컬 규칙을 유지한다.

```text
Tests/EDIT/
└─ inonego.Xeri.UI.TEST.EDIT.asmdef

Tests/PLAY/
└─ inonego.Xeri.UI.TEST.PLAY.asmdef

Tests/Editor/
└─ inonego.Xeri.UI.Editor.TEST.EDIT.asmdef

Runtime/{module}/TEST/EDIT/
└─ Edit test asmref + module-local tests

Runtime/{module}/TEST/PLAY/
└─ Play test asmref + module-local tests

Runtime/{module}/TEST/Editor/
└─ Editor test asmref + editor-only module tests
```

규칙:

- test source를 production assembly로 이동하지 않는다.
- module-local test colocate 구조를 유지한다.
- `TEST/EDIT`, `TEST/PLAY`, `TEST/Editor`에 새 asmdef를 만들지 않고 기존 named asmref를 사용한다.
- GUID 기반 asmref를 도입하지 않는다.
- 빈 `TEST/Editor` 폴더를 미래 대비로 만들지 않는다.
- Unity asset/test scene 이동 시 기존 `.meta` GUID 보존 여부를 명시적으로 확인한다.

### Module-local folder 정리

폴더는 production class 이름을 그대로 복제하기보다 **검증 계약/환경**이 드러나게 묶는다.

Core 예시:

```text
Runtime/Core/TEST/EDIT/
├─ Presentation/
│  ├─ Layout/
│  ├─ Session/
│  └─ State/
├─ Navigation/
│  └─ Screen/
├─ Interaction/
│  ├─ Focus/
│  └─ Input/
├─ Modality/
├─ Runtime/
└─ Support/

Runtime/Core/TEST/PLAY/
├─ Presentation/
├─ Input/
├─ Focus/
├─ Rendering/
└─ Support/
```

Window 예시:

```text
Runtime/Window/TEST/EDIT/
├─ Core/
│  ├─ Window/
│  ├─ Registry/
│  └─ Tray/
├─ Workspace/
│  ├─ Simple/
│  └─ Application/
├─ UITK/
│  ├─ Window/
│  ├─ Container/
│  └─ Themes/
└─ Support/

Runtime/Window/TEST/PLAY/
├─ Integration/
└─ Manual/
```

Tray는 같은 원칙으로 `Core / Reorder / UITK / Support` 정도의 실제 계약만 분리한다.

폴더를 production namespace tree와 기계적으로 1:1 복제하지 않는다.
테스트 준비 환경이나 계약이 실제로 달라질 때만 하위 폴더를 추가한다.

### Fixture 분할 기준

한 production type에 테스트 파일 하나를 강제하지 않는다.
반대로 기능 조각마다 파일을 잘게 나누지도 않는다.

분할 조건:

- 준비 환경이 다르다.
- 실패 의미가 다른 contract다.
- Unity runtime/native backend가 필요한 테스트와 pure EditMode 계약 테스트가 갈린다.
- rollback/failure scenario가 정상 lifecycle과 독립된 fixture를 요구한다.
- 공통 setup보다 contract별 setup이 더 커져 한 fixture가 여러 subsystem fake를 동시에 소유하게 된다.

파일 크기나 테스트 개수는 신호일 뿐 hard limit가 아니다.

예:

```text
TEST_ScreenController_OpenClose.cs
TEST_ScreenController_Replace.cs
TEST_ScreenController_Rollback.cs
TEST_ScreenController_Focus.cs
TEST_ScreenController_InputRelease.cs
```

위 분할은 각 파일의 setup/contract가 실제로 갈릴 때만 적용한다.

### File naming

기본:

```text
TEST_<Subject>.cs
```

같은 Subject를 contract별로 분리할 때:

```text
TEST_<Subject>_<Contract>.cs
```

예:

- `TEST_PresentationLayoutResolver.cs`
- `TEST_ScreenController_Rollback.cs`
- `TEST_XeriWindowWorkspace_Application.cs`
- `TEST_PresentationOrderingPlayMode.cs`

`Tests1`, `Misc`, `New`, `Regression2`처럼 의미 없는 suffix를 사용하지 않는다.

### Test namespace / class naming

기존 module-level namespace를 기본으로 유지한다.

```text
inonego.Xeri.UI.TEST.Core
inonego.Xeri.UI.TEST.Window
inonego.Xeri.UI.TEST.Tray
```

하위 폴더를 만들었다는 이유만으로 namespace를 기계적으로 더 깊게 만들지 않는다.
별도 namespace가 실제 type collision이나 public test-support boundary를 해결할 때만 추가한다.

test fixture class는 기본적으로 파일명과 같은 `TEST_<Subject>[_<Contract>]` 이름을 사용한다.
상속을 의도하지 않는 fixture는 `sealed`를 우선한다.

### Test method naming

현재 프로젝트의 한글 설명형 naming을 유지한다.

기본 형식:

```text
TEST_<Subject>_<조건/행동>_<기대결과>
```

예:

```text
TEST_ScreenController_CoveredScreen_CursorOwner아님()
TEST_PresentationSession_활성Consumer존재_Dispose거부()
TEST_XeriWindowWorkspace_ViewAcquire실패_획득Resource역순반환()
```

규칙:

- 구현 helper 이름보다 외부 contract를 이름에 쓴다.
- 과거 버그 번호만으로 이름 짓지 않는다.
- 회귀 테스트도 어떤 조건과 기대 결과인지 이름만으로 읽혀야 한다.
- Arrange 방식이나 private field 이름을 테스트명에 넣지 않는다.

### Block header

테스트 파일도 일반 C# block header 규칙을 따른다.

`# 설명`에는 fixture가 검증하는 contract를 적는다.

여러 behavior region이 있으면 `# 테스트 구성`을 사용한다.

```text
# 테스트 구성
 O: Open lifecycle
 C: Close lifecycle
 R: Rollback / failure
 F: Focus
```

코드는 현재 상태만 설명하고 변경 이력은 기록하지 않는다.

### Test class region order

테스트 클래스는 다음 기본 순서를 사용한다.

```text
#region 테스트 더블
#region 필드
#region 헬퍼
#region 픽스처
#region <Code>-1: <Contract>
#region <Code>-2: <Contract>
...
```

없는 책임의 region은 만들지 않는다.

세부 규칙:

- nested fake/stub/spy는 `테스트 더블`에 둔다.
- shared fixture state만 `필드`에 둔다.
- factory/assert helper는 `헬퍼`에 둔다.
- `SetUp / TearDown / OneTimeSetUp / OneTimeTearDown`은 `픽스처`에 둔다.
- behavior region은 `#region O-1: Open 정상 lifecycle`처럼 contract를 제목에 적는다.
- region code는 파일-local mnemonic이며 block header의 `# 테스트 구성`과 일치시킨다.
- 개별 테스트마다 region을 만들지 않는다.
- Bind/Unbind, Acquire/Release, Setup/Cleanup처럼 대칭 lifecycle은 같은 contract region에서 읽히게 배치한다.

### Test double naming

역할을 드러내는 이름을 사용한다.

권장:

- `FakeScreenInputDriver`
- `FakeFocusDriver`
- `ManualTransitioner`
- `ThrowingLifetime`
- `RecordingScreenSource`

`MockFoo`는 실제 mock semantics가 없으면 사용하지 않는다.

단일 fixture에서만 쓰는 작은 double은 nested private type으로 유지한다.
같은 semantic의 double을 여러 fixture가 반복 구현할 때만 module-local `Support`로 올린다.

모든 double을 하나의 global TestUtils로 모으지 않는다.

### Arrange / Act / Assert

AAA 주석을 기계적으로 강제하지 않는다.

대신:

- 핵심 전제는 테스트 본문에서 보여야 한다.
- setup helper가 테스트의 중요한 조건을 숨기지 않는다.
- assertion은 실패한 contract를 바로 좁힐 수 있게 작성한다.
- 하나의 테스트에 여러 assert가 있어도 같은 하나의 contract를 검증하면 허용한다.

### Reflection / private field injection

`SetField(...)`, `BindingFlags.NonPublic` 같은 reflection 조립은 축소한다.

우선순위:

1. production에서도 자연스러운 internal constructor / Initialize / Configure seam
2. 실제 prefab/asset/Unity serialized composition을 사용하는 PlayMode integration fixture
3. 마지막 수단으로 제한적 reflection

테스트만을 위해 public setter를 추가하지 않는다.

private field 이름 변경만으로 대량 테스트가 깨지는 구조는 migration 시 정리한다.

### Contract-first tests

테스트가 concrete helper type의 존재를 보존하지 않게 한다.

예:

```text
PresentationLayerRegistry가 존재한다
```

를 검증하기보다:

```text
Session-local LayerID lookup이 독립적이다
active consumer가 있으면 Session release가 거부된다
sibling Session에서 같은 LayerID를 재사용할 수 있다
```

를 검증한다.

`PresentationSurface`, `UITKPresentationOutput`, native Focus bridge처럼
Target에서 concrete type이 사라질 수 있는 경우 테스트도 invariant 기준으로 migration한다.

### Test level boundary

```text
EditMode / pure contract
→ resolver, state rules, registry, modifier math, reorder math

EditMode / orchestration
→ ScreenController, PresentationSession, UIContext, WindowWorkspace, rollback

PlayMode / backend
→ PanelRenderer, Canvas, Input System, native Focus, Scene-authored lifecycle

PlayMode / render contract
→ pixel ordering, World occlusion, mixed backend migration

Manual / UX
→ drag/resize feel, navigation feel, modal/window interaction
```

Manual test는 반드시 `[Explicit]` + `[Category("Manual")]`을 유지하고 correctness의 유일한 검증으로 사용하지 않는다.

### Oversized fixture migration targets

우선 정리 대상:

- `TEST_ScreenController.cs`
- `TEST_UIPresentation.cs`
- `TEST_UIRuntime.cs`
- `TEST_PresentationHandles.cs`

분할은 위 fixture 기준에 따라 수행한다.
단순 줄 수 감소를 목적으로 동일 setup/contract를 여러 파일에 복제하지 않는다.

### Test cleanup 완료 조건

- 각 test file이 하나의 명확한 contract family를 가진다.
- block header의 `# 테스트 구성`과 behavior region code가 일치한다.
- module-local TEST/asmref 구조가 유지된다.
- test double 중복이 bounded-context Support 수준으로 정리된다.
- reflection 기반 private injection이 필요한 위치가 명시적이고 최소화된다.
- concrete legacy type 삭제가 contract coverage 삭제로 이어지지 않는다.
- Edit/Play/Manual의 검증 책임이 중복 없이 구분된다.

## 19. Validation Plan

### Resolver / Plan

- duplicate LayerID
- duplicate LayerOrder
- duplicate PresentationID
- invalid placement LayerID
- LocalOrder isolation
- unsupported backend/render-space
- resolve 중 Unity mutation 없음

### Session / Materialization

- generated LayerHost-equivalent lifecycle
- Scene-authored PresentationLayerHost borrow/restore lifecycle
- LayerRoot / ManagedRoot containment
- authored static content 비파괴
- generated/provided Placement Root 혼용
- optional PresentationPlacementHost mount root 사용
- PlacementHost Layer containment validation
- Plan에 없는 LayerHost / PlacementHost ID 거부
- Layout LayerOrder / LocalOrder authoritative
- sibling Session ID reuse
- Child Session isolation
- active consumer dispose rejection
- rollback leak 없음

### Screen

- DuplicatePolicy Reject / Allow
- transition open/close
- always-unscaled behavior
- Driver default focus
- LastFocus restore
- no registration DefaultFocus path
- no InputPriority symbol/path

### Cursor/Input

- Covered not owner
- Effective Context owner
- ancestor fallback
- baseline restore
- release retention
- gameplay block OR
- inactive sibling contribution excluded

### Window

- ContentRoot attach
- Simple/Application ownership
- sibling Application Window isolation
- Window z-order independent from internal LayerOrder
- Workspace public API without raw ScreenOptions

### Public API tiering

- `UIContext.RegisterScreen(...)`과 `ScreenRegistry.Register(...)`의 등록 수명 동등성
- `UIContext.AcquirePresentation(...)`과 `PresentationLease.Acquire(...)`의 획득/반환 동등성
- UITK Modal Golden Path가 PresentationID placement usage와 hierarchy attach/detach를 ModalSession lifetime에 묶음
- UITK Modal helper가 자신이 변경한 `enabledSelf` / `pickingMode`를 종료·실패 시 원래 상태로 복원
- UITK Modal Composition overload가 caller-provided lifetime을 기존 ModalController에 그대로 이전
- Common helper 실패 시 placement usage/hierarchy/native interaction state leak 없음
- Common API 추가 후에도 ScreenRegistry, ModalController low-level Open, Focus/Authority, PresentationSession advanced path 사용 가능
- facade가 LayerID/LocalOrder/backend topology를 별도로 받거나 override하지 않음

### Suppression

- modifier lease nested owners
- latest Base restore
- dynamic target replacement
- no LateUpdate polling

### Pixel / rendering

- UITK ScreenOverlay sorting
- UGUI/UITK mixed migration ordering
- native World depth/occlusion
- no package-owned ScreenOverlay RT path

### Performance

- independent panels
- optional shared PanelSettings
- World multi-renderer
- native Focus/Input bridge reduction
- old RT path 대비

### Test structure / convention validation

- module-local `TEST/EDIT|PLAY|Editor` asmref가 올바른 test asmdef name을 참조
- test source가 production assembly에 포함되지 않음
- Editor-only test가 Play assembly에 섞이지 않음
- 이동한 test asset/scene의 `.meta` GUID 보존 여부 확인
- block header `# 테스트 구성`과 region code 일치
- region 기본 순서와 빈 region 금지 확인
- `TEST_<Subject>_<Condition>_<Expected>` naming 의미 확인
- reflection/private-field injection 잔존 위치 audit
- Manual test의 `[Explicit]` + `[Category("Manual")]` 확인
- contract-first migration 후 concrete legacy type 전용 test만 남지 않았는지 확인

## 20. Legacy Removal Checklist

완료 시 검색 결과 0건 또는 명시적 optional package 경계여야 한다.

### Core Presentation

- PresentationLayerAsset topology source
- manual top-level Surface provider
- old Profile topology/provider source
- UIDocument mandatory UITK output path
- duplicate LayerID source
- output-only `PresentationOutputBinding` final API/path
- LayerHost 외부 authored content까지 Xeri ordering이 직접 변경하는 경로
- Scene Host가 LayerOrder/LocalOrder를 별도 정의하는 경로
- Core generic Scene/Borrowed View Source helper without concrete repeated demand

### Screen / Input

- `ScreenOptions.InputPriority`
- `inputPriority:` callers
- driver priority comparison
- cursor winner acquisition-sequence tie-break
- cursor winner 선택용 `SessionState.Sequence` / `nextSequence`
- `ScreenOptions.DefaultFocus`
- `defaultFocus:` ScreenOptions caller
- `ScreenOptions.UsesUnscaledTime`
- Screen-specific scaled transition branch

### Window

- `ContentSlot` public symbol
- old UXML/test/doc naming
- `CreateView` Window View Source naming
- general constructor raw ScreenOptions requirement

### Suppression

- Interactive Dialogue suppression LateUpdate path
- persistent Group Apply polling
- CombatFeedback Alpha polling membership

### Runtime backend

- UITK-only EventSystem hard requirement
- UITK-only InputSystemUIInputModule hard requirement
- obsolete native Focus bridge state proven unnecessary

### Tests

- oversized all-in-one fixture가 contract 분리 없이 남아 있는 경로
- 같은 fake/support 구현의 불필요한 fixture별 중복
- private field 이름에 광범위하게 의존하는 reflection setup
- 삭제된 concrete type 존재 자체를 고정하는 test
- Manual-only correctness coverage
- 의미 없는 `Misc/New/Regression2` test file naming
- block header/region code 불일치

### Docs

- old Profile/LayerAsset usage examples
- Target/current-state mixed document
- removed API samples

## 21. Completion Criteria

1. `../../com.inonego.xeri.ui/Documentation~/architecture/presentation.md` contains only the final declarative contract.
2. Current implementation and migration work live only in this document.
3. InputPriority code/test/sample/doc references are gone.
4. ScreenOptions DefaultFocus code/test/sample/doc references are gone.
5. ScreenOptions UsesUnscaledTime code/test/sample/doc references are gone.
6. Screen cursor policy follows ownership topology.
7. ContentRoot is the only Window mount-point public naming.
8. Persistent suppression has no LateUpdate Group reapply path.
9. UITK generated output uses PanelRenderer direct path.
10. Generated/Scene-authored Layer materialization이 모두 같은 RuntimeLayer contract로 수렴한다.
11. Scene-authored Layer는 PresentationLayerHost의 Native Output + LayerRoot + ManagedRoot 경계로 표현된다.
12. optional PresentationPlacementHost는 physical mount root만 제공하고 LayerID/LocalOrder를 override하지 않는다.
13. output-only PresentationOutputBinding compatibility path가 최종 API에 남지 않는다.
14. Xeri ordering은 ManagedRoot subtree에만 적용되고 authored static content를 건드리지 않는다.
15. UITK-only event dispatch does not require UGUI EventSystem infrastructure.
16. Scene-authored Host/View external ownership is explicit and non-destructive.
17. World support is native-first and concrete-use-case validated.
18. old/new topology/focus/input source of truth가 동시에 남아 있지 않다.
19. 공개 docs는 최종 API만 설명한다. 기존 Sample은 OS형 Validation Scene 통합 전까지 current Common API를 사용한다.
20. module-local TEST/asmref 구조가 로컬 규칙과 일치한다.
21. 테스트 fixture가 contract/환경 경계 기준으로 정리되고 거대 all-in-one fixture를 관성적으로 유지하지 않는다.
22. 테스트 block header, region 순서, region code와 test method naming이 문서 규칙에 맞는다.
23. reflection 기반 private injection은 불가피한 Unity integration 경계에만 제한된다.
24. concrete legacy type 제거 후에도 해당 invariant의 contract coverage가 유지된다.
25. EditMode / PlayMode / Manual 검증 책임이 분명히 구분된다.
26. 테스트 편의를 위해 production public API/interface를 새로 만들지 않는다.
27. Common API는 기존 Composition primitive의 lossless facade이며 별도 state source를 만들지 않는다.
28. Project Composition / Backend API는 supported advanced path로 유지되며 Common API 때문에 기능이 축소되지 않는다.
29. Advanced API에서도 Layout/Plan topology, Session lifetime, Context authority invariant를 우회하는 mutable escape hatch가 없다.

## 22. 작업 시 원칙

- 한 Phase를 구현하면서 unrelated future abstraction을 같이 추가하지 않는다.
- 제거 결정은 compatibility alias 없이 실제 caller migration까지 끝낸다.
- 사용처가 없다는 이유만으로 정당한 invariant를 가진 기능을 자동 제거하지 않는다.
- 반대로 미래 가능성만으로 public option을 유지하지 않는다.
- 각 변경은 ownership/lifetime/failure boundary 테스트와 함께 끝낸다.
- 테스트를 추가/분할할 때 기대 결과의 근거와 실패 의미를 먼저 확인한다.
- 테스트 수나 파일 수 자체를 목표로 삼지 않는다.
- production object를 분리하는 이유와 test fixture를 분리하는 이유를 혼동하지 않는다.
- 테스트 때문에 public setter/interface/factory를 만들지 않는다.
- 테스트 파일을 수정할 때 C# convention의 block header, using, XML summary, region 규칙을 함께 적용한다.
