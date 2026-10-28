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

## Native Output 수명과 콘텐츠 연결

출력 수명은 각 Backend Output 하나가 소유합니다. `UITKPresentationOutput`은
생성/대여 초기화, PanelSettings, Native Root 재연결과 기본 스타일의 반환까지
관리하며 별도의 기본 스타일 Lease 객체를 만들지 않습니다. 원래 있던 클래스·스타일시트와
배경색·opacity는 유지하고, 이번 Output이 추가한 항목만 해제합니다.
`UGUIPresentationOutput` 역시 Canvas의 생성/대여와 정렬 순서 복원을 직접 관리합니다.
두 Output은 렌더 파이프라인이나 FP16 버퍼를 선택하지 않습니다.

독립된 authored UITK Layer Host들이 같은 `PanelSettings`를 빌리는 구성은 Session 생성 전에
거부합니다. 같은 설정의 정렬 순서를 여러 Output이 바꾸거나 서로 다른 원본으로 복원하는 충돌을
막기 위한 조건입니다. 부모 Output 안의 embedded 자식 Session은 이 조건의 독립 출력에 포함되지 않습니다.

authored Layer/Placement의 Native 트리가 교체되면 현재 `UITKPresentationLayer.Root`도 바뀌며,
unload 중에는 null이 됩니다. 생성한 Placement 루트는 같은 인스턴스를 새 ManagedRoot에 부착합니다.
authored Placement는 새 참조의 direct-child 조건을 확인하고 Xeri 런타임 콘텐츠만 다시 연결합니다.
작성된 정적 요소는 옮기지 않습니다. 각 요소의 display·z-index 대여 상태와 포커스 콜백도 함께 반환합니다.

사용자 Source에서 재연결이 필요한 런타임 콘텐츠는 다른 hierarchy에 붙지 않은 루트로 만든 뒤
`UITKPresentationLayer.Attach(uitkLayer, content)`로 연결하고, 반환된 `Lease`를 콘텐츠 수명 종료 시
해제합니다. Xeri Layer는 이 등록을 기준으로 콘텐츠를 재연결합니다. `Root.Add`로 직접 추가한 사용자
요소 전체를 자동으로 옮기지는 않습니다. Window workspace·Modal·SceneFade의 내장 경로는 같은 부착 계약을 사용합니다.

## Native UI 출력 정책

`UISettingsAsset`의 `HighPrecisionOutput`과 `UIPostProcessing`은 독립적인 프로젝트 출력 정책입니다. `UIRuntime`이 초기화된 동안 정책이 적용되며, 종료하거나 초기화에 실패하면 등록이 해제됩니다. 기본값은 고정밀 출력 OFF와 후처리 Unaffected입니다. Layer, Panel 또는 Canvas별 설정은 변경하지 않습니다.

URP와 HDRP 통합은 지원되는 카메라와 버퍼에서 Unity Native UITK/UGUI 출력을 합성합니다. 설치되지 않았거나 조건이 맞지 않으면 Unity Native 출력으로 돌아갑니다. 실제 출력은 선택된 정책뿐 아니라 각 파이프라인의 설치 상태와 카메라 조건에 따라 달라집니다.

스타일 해석과 Gradient 보간은 Unity Native Renderer에 맡깁니다. FP16 합성은 이미 계산된 정점 색이나 Gradient의 정밀도를 복구하지 않으며, Chrome CSS와의 색상 일치 보정은 제공하지 않습니다.

## Unity 7 URP Native UI 출력 (Opt-in)

URPNativeOverlayFeature는 inonego.Xeri.UI.URP 어셈블리의 선택형 URP Renderer Feature입니다.
URP Renderer Data의 Renderer Features에 이 기능을 설치하고 `UIRuntime`의 출력 정책으로 활성화합니다.
Native UITK/UGUI RendererList를 정책에 맞는 시점에 카메라 버퍼에 직접 그립니다.
고정밀 출력이 켜진 경우에는 카메라 HDR과 URP HDR Color Buffer Precision **64-bit**가 필요하며,
실제 RenderGraph 색상 attachment가 R16G16B16A16_SFloat인지 확인합니다. 고정밀 출력이 꺼지고
후처리 영향만 켜진 경우에는 지원되는 기존 카메라 중간 버퍼를 사용합니다.
서로 다른 PanelRenderer 및 ScreenSpaceOverlay Canvas의 Native sortingOrder를 그대로 사용하며,
UI 별도의 캡처 RT와 별도 EventSystem은 만들지 않습니다.

현재 적용 범위는 Linear Color Space/SDR Display Output의 **단일 전체 화면 Game/Base Camera**입니다.
카메라 스택, 여러 활성 카메라, XR, RenderTexture 대상, Render Scale·동적 해상도와
고정밀 요청 시 비-FP16 attachment는 지원 대상으로 판단하지 않고
URP Native Overlay에 맡깁니다. Renderer Feature는 카메라 종료 시 변경한 전역 overlay ownership을 복원합니다.
해당 경계는 아직 다중 카메라/SceneView 동시 렌더링의 완전한 지원 계약이 아닙니다.
기본 비활성화입니다. Xeri UISettingsAsset 출력 정책과 명시적 Direct Output Editor 설치 도구를 사용해 프로젝트 단위로 활성화할 수 있으며, 미지원 조건에서는 Native로 돌아갑니다.

### URP 프로젝트 설정

Unity Editor의 **Tools → Xeri UI → URP → 출력 설정 미리보기**에서 Graphics 기본
Pipeline과 Quality별 Pipeline Asset 및 각 Universal Renderer Data를 함께 확인합니다.
**출력 설정 적용**을 선택하면 해당 자산을 중복 없이 구성합니다.
UISettingsAsset을 선택했거나 프로젝트에 유일한 UISettingsAsset이 있어야 하며,
여러 개가 있을 경우 임의로 하나를 고르지 않습니다. Universal Renderer Data에
Native Direct Output Feature를 추가하고, **High Precision ON일 때만** HDR Precision을
64-bit로 변경합니다. 다른 Feature나 Graphics/Quality 참조는 유지합니다.

HDR Precision을 변경하거나 비활성 RendererFeature를
활성화하거나 Xeri Feature를 새로 만든 경우에는 소유 변경을
`ProjectSettings/XeriUIURPDirectOutputInstallationState.asset`에 기록합니다.
**출력 설정 적용 해제**는 해당 기록과 현재 자산을 대조해
Xeri가 추가한 Feature 및 적용한 값을 선택적으로 복원합니다. 사용자가 Feature
설정이나 정밀도를 다른 값으로 변경한 경우 강제로 덮어쓰지 않고 보존합니다.
동일한 설정값을 사용자가 나중에 의도적으로 재선택한 경우는 자동 구별할 수 없으므로
적용 해제 전 미리보기를 확인해야 합니다. 모든 설치·해제 메뉴는 Editor Undo/Redo를 지원하며,
Renderer 자산과 ProjectSettings 소유 기록을 함께 복원하고 기록 파일도 동기화합니다.
사용자 편집을 보존하거나 삭제된 대상을 처리한 기록은 종료합니다. 재설치는 그때의 사용자
설정을 새 원본으로 삼습니다. 패키지 파일이나 사용자의 전체 렌더링 설정을 삭제하지 않습니다.

코드에서는 `URPDirectOutputInstaller.InstallProject(settings)`와 `UninstallProject()`를 사용합니다.
`InstallProject`는 실제 변경된 URP 자산 수, `UninstallProject`는 실제 복원한 항목 수를 반환합니다.
변경이 없으면 0입니다. 단일 자산은 `Install(asset, settings)`로 구성하며 반환값은 구성 가능한
고유 렌더러 수입니다. `InstallRenderer(renderer)`도 같은 소유 기록을 사용합니다.
프로젝트 적용 해제는 이 API들로 누적한 **프로젝트 전체의 Xeri 소유 변경**을 처리합니다.

| High Precision | UI Post Processing | 현재 URP 동작 |
| --- | --- | --- |
| OFF | Unaffected | Unity 기본 Native ScreenOverlay |
| OFF | Affected | 기존 카메라 중간 버퍼 포맷으로 후처리 이전 Native UI Draw |
| ON | Unaffected | FP16 attachment에 주요 장면 후처리 이후 Native UI Draw |
| ON | Affected | FP16 attachment에 주요 장면 후처리 이전 Native UI Draw |

Affected는 **URP Uber Post 이전**을 뜻하며 TAA를 포함한 모든 효과가 UI에 적용된다는
뜻이 아닙니다. Unaffected 역시 FinalPass·출력 인코딩·Dither 등 모든 후속 처리에서
제외된다는 보장이 아닙니다. FP16이 요청됐으나 실제 attachment가
`R16G16B16A16_SFloat`가 아니면 무조건 Native fallback하고, Gamma Color Space,
HDR Display Output, 다중 카메라/스택, XR 및 Render Scale 변경 등은 아직 미지원입니다.
`UIRuntime`이 초기화되지 않았거나 종료된 프로젝트에서는 Unity Native 출력을 유지합니다.
URP와 HDRP는 같은 `UIPresentationOutputPolicy` 합성 판정을 사용하며,
Renderer Feature의 별도 정밀도·후처리 정책은 제공하지 않습니다.

Native UITK/UGUI RendererList는 **색상 attachment뿐 아니라 크기·샘플 수가 일치하는
깊이·스텐실 attachment도 필요**합니다. 해당 attachment를 연결하지 않으면 실제
Preferences Window에서 배경만 회색으로 표시되고 제목·본문 등 자식 UI가 누락될 수
있습니다. 현재 Direct Feature는 attachment 조건이 맞지 않으면 패스를 등록하지 않고
Unity Native 출력을 유지합니다. 단순 컴파일 성공이나 화면 전체 캡처만으로는 이 결함을
발견하지 못하므로 실제 Window 내부의 픽셀 대비까지 검증합니다.

깊이 전용 포맷(`D16_UNorm` 등)은 크기와 샘플 수가 같아도 UI 클리핑에 필요한
스텐실이 없으므로 Native 출력을 유지합니다. `URPNativeOverlayFeature.NativeFallbackReason`은
마지막 Game 카메라 검사에서 확인한 미지원 사유를 제공합니다. 사유가 바뀔 때 경고를
기록하며, 같은 사유로 매 프레임 반복하지 않습니다. `null`은 정책에 따른 기본 Native 출력,
다른 Feature의 소유권, 미검사 상태도 포함하므로 FP16 실행 성공의 근거로 사용하지 않습니다.

URP Feature가 정책 적용과 카메라 소유권을 직접 소유합니다. 중첩 Unity Pass는
RenderGraph 콜백을 Feature로 전달하며 별도 정책·소유 상태를 갖지 않습니다.

## Unity 7 HDRP Native UI 출력 (Opt-in)

HDRP 17.7에서는 `Tools/Xeri UI/HDRP/출력 설정 미리보기`로 대상을 확인한 뒤
**출력 설정 적용**으로 설치하고 **출력 설정 적용 해제**로 소유 변경을 반환합니다.
설치기는 현재 HDRP Global Settings의 Before/After Post Process 목록,
Player에서 사용할 기본 Volume Profile의 두 타입과 프로젝트 HDRP의 FP16 버퍼 포맷을 준비합니다.
기본 Volume Profile은 프로젝트의 `Assets/`에 저장돼 있어야 합니다.
UI별 Texture/Material이나 Window 전용 합성기를 만들지 않습니다.

코드에서는 `HDRPNativeOutputInstaller.Install()`과 `Uninstall()`을 사용합니다.
`Install`은 실제 자산 변경 여부를 반환하고 필수 자산 누락은 예외로 알립니다.
`Uninstall`은 자산 또는 소유 기록을 해제했는지 반환합니다. 사용자 포맷 편집은 유지하며 해당 기록을
종료하므로 다음 설치·해제에서도 그 사용자 값으로 돌아갑니다. 삭제된 Global Settings의 기록도
종료하고 남아 있는 Volume·포맷 변경을 독립적으로 반환합니다. 설치기가 추가한 Volume을
사용자가 비활성화하거나 이름·숨김 설정을 바꾼 경우에는 해당 Volume과 Custom Post Process
등록을 함께 보존하고 소유 기록을 종료합니다. 변경되지 않은 설치 Volume과 등록은 제거합니다.

`HDRPNativeOverlayPostProcess.NativeFallbackReason`은 마지막 HDRP Game 카메라의 미지원 또는
합성 생략 사유를 읽기 전용으로 제공합니다. 카메라 조건·포맷·스텐실과 CustomPP 예약 여부를 확인하고,
같은 사유를 매 프레임 반복 출력하지 않습니다. null은 GPU 실행 성공을 증명하지 않습니다.

`UIRuntime`의 `HighPrecisionOutput`과 `UIPostProcessing`을 실행 중 적용합니다.
OFF/Unaffected는 Unity Native 출력, Affected는 주요 후처리 전,
ON/Unaffected는 주요 후처리 뒤의 `R16G16B16A16_SFloat` 버퍼에 그립니다.
AfterPostProcess 뒤에도 HDRP의 FXAA/최종 화면 변환은 남아 있으므로
Unaffected가 모든 최종 효과를 우회한다는 뜻은 아닙니다.

`HDRPNativeOverlayPostProcess`가 정책·카메라 범위·스텐실 수명을 직접 소유합니다.
Before 등록용 파생 타입은 Unity의 타입별 고정 삽입 지점 때문에 필요하며 별도 상태를 갖지 않습니다.
실제 CustomPP 예약의 `IsActive()` 안에서만 Native 소유권을 획득하므로,
Camera Postprocess/Custom Post Process 또는 해당 VolumeComponent가 꺼지면 Native를 유지합니다.
카메라 종료 시에는 획득한 전역 객체만 복원합니다.

UITK/UGUI는 깊이·스텐실을 연결한 Native Render Pass 안에서 원래 RendererList 순서대로 그립니다.
지원 범위는 단일 전체 화면 Game 카메라, Linear SDR, 원래 해상도입니다.
다중 카메라·부분 viewport·offscreen·동적 해상도·XR·HDR display는 Native로 유지합니다.
설치 복원 메뉴는 설치기가 추가한 등록·기본 볼륨과 포맷 변경만 되돌립니다.

## UGUI output

UGUI Layer는 `UGUIPresentationOutput`의 Canvas를 native output boundary로 사용합니다.
UITK의 `UITKPresentationOutput`과 같이 Generated `Initialize`, Scene-authored
`InitializeBorrowed`, 대칭적인 `ReleaseOutputState` 수명을 가집니다.
Scene-authored Canvas의 원래 `sortingOrder`는 Output이 저장·복원하며, LayerHost는
ManagedRoot와 호스트 활성 상태의 대여·복원을 담당합니다. Generated 출력을 제거할 때는
공통 Materializer 정리 경계에서 Native Output 해제와 GameObject 파괴를 각각 시도합니다.

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
