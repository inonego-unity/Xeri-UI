# 프로젝트 설정과 시작

Xeri UI를 사용하는 프로젝트는 **Presentation Layout → UI Settings → Runtime Host → 시작 Scene**을 연결합니다. Unity의 렌더링·이벤트 시스템은 그대로 사용하고 UI의 표시·입력·수명은 `UIRuntime`이 관리합니다.

## 1. Presentation Layout 작성

`Assets > Create > Xeri > UI > Presentation Layout`에서 `PresentationLayout`을 만듭니다.

| 항목 | 의미 |
| --- | --- |
| `LayerID` | 같은 Presentation Session에서 고유한 ID |
| `DisplayName` | Inspector에서 식별할 이름 |
| `LayerOrder` | 같은 Layout의 Layer 간 표시 순서 |
| `Backend` | UI Toolkit 또는 UGUI |
| `Placements` | Scene-authored 고정 위치가 필요할 때만 사용 |

Placement의 `PresentationID`는 Session 안의 고유 ID이고, `LocalOrder`는 소속 Layer 안의 순서입니다. 일반 Screen/Modal/Splash처럼 나타났다 사라지는 UI에는 Placement를 예약하지 않고 표시 대상 Layer를 동적으로 획득합니다.

예시:

```text
Application  — UITK, order 100
Overlay      — UITK, order 300
Modal        — UITK, order 400
SystemFade   — UGUI, order 10000
```

이 이름과 Order는 예시입니다. 실제 등록된 Layer ID와 아래 Destination 매핑이 일치해야 합니다.

## 2. UI Settings 작성

`Assets > Create > Xeri > UI > Settings`에서 `UISettingsAsset`을 만들고 다음 값을 연결합니다.

| 속성 | 설정 |
| --- | --- |
| `DefaultLayout` | 위에서 만든 Root Layout |
| `UITKPanelSettingsTemplate` | generated UITK Layer의 PanelSettings |
| `UGUIOutputTemplate` | 선택적 UGUI output template, 없으면 패키지 기본값 |
| `DestinationDefinitions` | semantic Host Destination ID → Root Layer ID |
| `BackendSupport` | 프로젝트가 지원할 UITK/UGUI capability |
| `UIActionsAsset` / `UIActionMap` | UI 입력 액션과 맵 |
| `GameplayActionsAsset` / `GameplayActionMap` | Gameplay 입력 액션과 맵 |
| `ReleaseActionNames` | Screen 종료 시 입력 해제를 기다릴 Action 이름 |
| `DefaultFadeColor` / `DefaultFadeDuration` | Scene Fade 기본값 |
| `HighPrecisionOutput` / `UIPostProcessing` | 선택적 URP/HDRP 출력 정책 |

**`System` Destination은 필수**입니다. Scene Fade가 이 Destination의 Root Layer를 사용합니다. Window나 Splash처럼 프로젝트에서 필요한 사용자 정의 Destination ID도 등록할 수 있습니다.

`BackendSupport`는 현재 열려 있는 Layer가 아니라 **프로젝트가 사용할 backend capability 전체**를 나타냅니다. UITK-only 구성은 UGUI EventSystem이 필수가 아니며, UGUI를 포함하면 Host의 `EventSystem + InputSystemUIInputModule`을 검사합니다. UI Action Map과 Gameplay Action Map은 서로 분리합니다.

## 3. Runtime Host 연결

패키지의 기본 Host prefab은 `Runtime/Core/Assets/Host/UIHost.prefab`입니다.

Host에는 Presentation root, Focus Driver, Screen Input Driver, Scene Fade Source가 연결돼야 합니다. Scene에 직접 authored Layer를 배치하는 경우 `PresentationLayerHost[]`를 Runtime composition에 **명시적으로** 전달합니다.

- UGUI: `UGUIPresentationLayerHost`
- UITK: `UITKPresentationLayerHost` + `PanelRenderer` + `VisualElementReference`
- authored Placement까지 사용한다면 `PresentationPlacementHost`

Scene을 이름으로 전체 검색해 누락된 Host를 자동 연결하지 않습니다. generated output의 자원 소유권과 scene-authored output의 대여·복원은 [Presentation](../core/presentation.md)을 참고합니다.

## 4. UniXeri Bootstrapper 연결

일반 프로젝트에서는 **UniXeri의 Build Profiles Bootstrapper 확장에서 제공하는 Scene 생성·등록 기능**을 사용합니다.

1. UniXeri Build Profiles에서 Bootstrapper Scene을 생성·등록합니다.
2. `Assets/Resources/BootstrapperSettings.asset`에서 **Bootstrapper Scene Index**와 **Initial Application Scene Index**를 지정합니다.
3. Build Settings의 Scene 목록을 확인합니다. 표준 구성은 `Assets/Bootstrapper.unity`가 **0번**, 첫 Application Scene이 **1번**입니다.
4. `Assets > Create > Xeri > Bootstrapper > UI Module`에서 `UIBootstrapperModuleAsset`을 생성합니다.
5. Module의 **Host Prefab**과 **Settings**에 위 자산을 지정하고, UniXeri Bootstrapper Settings의 Module 목록에 등록합니다.

`UIBootstrapperModuleAsset`은 `AfterInitialScene` 단계에서 `StartAsync(ApplicationContext, CancellationToken)`로 실행됩니다. 최초 Scene에 **Scene-owned `UIRuntime`**이 있거나 이미 초기화된 Runtime이 있으면 별도 Host를 생성하지 않습니다. 그렇지 않으면 Host prefab을 생성·초기화하고, **UniXeri Application Scope가 생성한 Host의 종료 수명**을 소유합니다.

Validation처럼 Scene이 Runtime composition을 직접 포함하는 구성은 별도 Host를 중복 생성하지 않습니다. 두 방식을 한 Scene에서 동시에 초기화하지 마세요.

## 5. Runtime 사용

Bootstrapper가 최초 Application Scene을 로드하고 UI Runtime이 초기화된 후에 사용합니다.

```csharp
UIRuntime runtime = UIRuntime.Current;
UIContext context = runtime.Main;

ScreenOpenResponse response = context.Screens.Open("Inventory.Home");
```

예시의 `Inventory.Home`은 사용자가 미리 등록한 Screen ID여야 합니다. 등록 방법은 [Screen과 입력](../core/screens.md)에 설명합니다.

`UIRuntime.Main`, `RootPresentation`, `SceneFader`는 Runtime-owned 서비스입니다. 기능 소비자는 개별 Screen/Modal/Window/Layer Lease만 자신이 가진 수명 경계에서 반환하고, Runtime 소유 서비스를 직접 `Dispose()`하지 않습니다.

## 초기 구성 점검

- 두 Bootstrapper Scene 인덱스가 모두 지정됐는가?
- Root Layout Layer와 `DestinationDefinitions`의 ID가 맞는가?
- generated UITK Layer가 있다면 PanelSettings가 유효한가?
- UGUI capability가 있다면 EventSystem과 InputSystemUIInputModule이 있는가?
- DOTween Runtime 및 Modules 어셈블리를 해석할 수 있는가?

직접 조작해 볼 환경은 [Validation Desktop](../validation.md), 오류별 해결은 [문제 해결](../core/troubleshooting.md)을 참고합니다.
