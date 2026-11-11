# 문제 해결

Xeri UI 문제는 **프로젝트 시작 설정**, **Presentation 연결**, **Screen/Focus/Input**, **개별 View 수명**으로 나눠 확인합니다. Unity Console의 첫 번째 오류와 해당 Scene·Settings의 실제 값을 우선 확인하세요.

## Bootstrapper 시작 전에 오류가 발생함

**오류:** `InvalidOperationException: Bootstrapper와 최초 Application Scene 설정이 필요합니다.`

이 오류는 UniXeri Bootstrapper가 **시작 Scene 및 최초 Application Scene의 인덱스**를 찾지 못했다는 뜻입니다. DOTween 문제와는 다릅니다.

1. Unity Build Settings의 Scene 목록에서 Bootstrapper Scene과 시작할 Application Scene이 활성화돼 있는지 확인합니다.
2. UniXeri의 **Build Profiles Bootstrapper 생성·등록 기능**으로 표준 `Assets/Bootstrapper.unity`를 구성합니다.
3. `Assets/Resources/BootstrapperSettings.asset`의 두 Scene index가 실제 Build Settings의 활성 Scene 번호를 참조하는지 확인합니다. 일반적인 표준 구성은 **0번 Bootstrapper / 1번 Application**입니다.
4. UI Module의 Host Prefab·UI Settings·Module 등록도 확인합니다. 최초 Scene에 자체 `UIRuntime`이 있다면 Host 중복 생성을 피합니다.

Validation 프로젝트에서 실행한다면 최초 Application Scene을 `Packages/com.inonego.xeri.ui/Validation/Scenes/XeriUIValidation.unity`로 연결합니다. 구체적 절차는 [설정과 시작](../getting-started/setup.md#4-unixeri-bootstrapper-연결)에 있습니다.

## DOTween 오류가 나타남

| 증상 | 확인 대상 |
| --- | --- |
| `DG.Tweening` 타입을 찾을 수 없음 | DOTween Runtime 어셈블리가 프로젝트에 설치됐는지 |
| `DOTween.Modules` 참조 오류 | DOTween Modules 어셈블리와 `inonego.Xeri.UI.asmdef` 참조 |
| Bar 애니메이션 없이 즉시 값이 바뀜 | Play Mode, Tween 시간, `DOTWEEN` 컴파일 심볼 |
| `DOTweenEditor.dll`의 `OnScriptsReloaded()` 예외 | Unity 7과 DOTween Editor 확장 버전의 호환성 |

**Editor 확장 오류와 Runtime DLL 로딩 문제는 구분**합니다. 구형 Editor 확장 오류가 남아 있어도 실제 UI가 동작할 수 있지만, 그런 경우에도 Unity 7 호환 버전으로 Editor 확장을 갱신하는 것이 적절합니다. 오류를 숨기려고 Runtime/Modules를 삭제하지 않습니다.

## UIRuntime 초기화가 안 됨

다음을 순서대로 확인합니다.

1. `UISettingsAsset.DefaultLayout`이 지정돼 있는가?
2. `DestinationDefinitions`에 `System`이 존재하며 Root Layout의 유효한 Layer ID를 가리키는가?
3. `BackendSupport`가 Layout에 있는 UITK/UGUI backend를 포함하는가?
4. generated UITK Layer에 유효한 `UITKPanelSettingsTemplate`이 있는가?
5. UI·Gameplay Action Asset과 Map 이름이 유효하고 서로 분리돼 있는가?
6. Host에 Presentation root, Focus Driver, Input Driver, Scene Fade Source가 연결됐는가?
7. scene-authored `PresentationLayerHost[]`가 Layout의 ID/backend와 정확히 일치하는가?
8. 같은 App lifetime에 `UIRuntime`을 중복 초기화하고 있지 않은가?

UGUI capability가 포함되면 시작할 때 `EventSystem + InputSystemUIInputModule`도 필요합니다. UITK-only에는 이를 강제로 추가할 필요가 없습니다.

## Layer 또는 Placement가 열리지 않음

| 증상 | 확인 |
| --- | --- |
| Duplicate Layer ID/Order | 같은 Layout 안의 Layer ID와 `LayerOrder`가 고유한지 |
| Unknown Layer ID | Placement·Host Destination이 실제 Layer를 가리키는지 |
| Duplicate Placement ID/LocalOrder | 같은 Session ID 및 소속 Layer의 순서 규칙 |
| Backend mismatch | 요청한 UITK/UGUI Driver와 실제 Layer의 backend 일치 |
| Scene-authored Layer 누락 | 명시적 `PresentationLayerHost`, LayerID, ManagedRoot, PanelRenderer/Canvas |
| Authored Placement 오류 | `PresentationID`, Plan 소속, ManagedRoot의 direct-child 구조 |

Scene-authored UITK Host는 `PanelRenderer`와 같은 Panel에 연결되는 `VisualElementReference`가 필요합니다. Package는 임의의 Scene 전체를 스캔해 빠진 Host를 자동 검색하지 않습니다.

## Screen Open이 거부됨

- `RegisterScreen`에서 받은 Registration Handle이 살아 있는지 확인합니다.
- Screen ID와 `PresentationTarget`은 별개입니다. Target이 존재하는 Layer 또는 Host Destination을 가리켜야 합니다.
- `ScreenDuplicatePolicy.Reject`와 이미 살아 있는 동일 ID가 충돌하지 않는지 확인합니다.
- 같은 Screen Controller가 전환 중이거나 navigation lifecycle callback에 들어간 상태인지 확인합니다.
- `IScreenSource.Acquire`가 최종 Layer backend에 맞는 Driver를 반환하는지 확인합니다.

Registration을 Dispose해도 이미 열린 Screen Session을 대신 닫지 않습니다. 일반 Screen 종료는 `ScreenSession.Close()` 또는 Controller의 `Close()` 명령을 사용합니다.

## Focus·Gameplay Input·Cursor가 예상과 다름

- Focus 복원 순서는 **LastFocus → Scope.DefaultFocus → backend fallback**입니다.
- 활성 Context의 Focus 권한과 inactive sibling Context의 Screen 기여를 혼동하지 않습니다.
- Gameplay block은 effective Context path의 `BlocksGameplayInput` 기여로 결정됩니다.
- 입력 Release Barrier는 action 해제를 확인한 뒤 Cursor·Screen 종료 정책과 조합됩니다.
- Covered Screen은 기본 Cursor owner가 아닙니다.
- Window의 Focus와 Presentation Layer Order는 별개의 순서입니다.

## Window·Tray 표시 또는 종료가 이상함

- Workspace의 Target은 실제 **UITK Layer**여야 합니다.
- Simple Window는 외부에서 전달한 View를 소유해 배치하며, Application Window는 Child PresentationSession과 Child UIContext를 소유합니다.
- `IXeriUIViewSource.AcquireView`의 반환 View는 **부모 없는 VisualElement**여야 합니다.
- Workspace가 종료된 뒤 View Source·Tray Controller·Tray Source의 호출자 소유 수명을 계속 사용하지 않습니다.
- Tray의 Close는 도메인 Window를 직접 삭제하는 명령이 아니라 **취소 가능한 요청**입니다. 승인 후 Workspace/Source 상태를 갱신해야 합니다.

## Modal 또는 Presentation Session이 남음

Modal을 열었다면 해당 `ModalSession.Dispose()`로 반환하거나, 상위 Screen/Window의 `RegisterChild`에 수명을 이전합니다.

Presentation Session 종료가 거부되면 아직 살아 있는 Layer consumer를 확인합니다.

- `PresentationLayerLease`
- authored Placement 사용 수명
- Screen/Modal 또는 Child Presentation Session
- Window/Drag Visual이 보유한 attachment

**consumer lifetime을 먼저 반환하고 owner Session을 종료**합니다. Runtime-owned `RootPresentation`·`SceneFader`는 소비자가 직접 Dispose하지 않습니다.

## Spotlight 강조 영역이 표시되지 않음

- UI Toolkit은 `UITKSpotlightElement`와 Target이 같은 Panel에 연결돼 있어야 합니다.
- USS로 클리핑되는 조상은 `UITKSpotlightClip`으로 명시하고 실제 Target의 조상인지 확인합니다.
- UGUI는 `UGUISpotlightDriver`의 Root/Graphic 참조와 Target의 Canvas 활성 상태를 확인합니다.
- 유효한 강조 영역이 없으면 dim과 바깥 입력 차단을 함께 해제합니다.

[Spotlight 사용 명세](spotlight.md)에서 설정과 종료 수명을 확인할 수 있습니다.

## Native UI 고정밀·후처리 출력이 나타나지 않음

URP/HDRP 출력은 명시적 설치 도구와 Runtime 정책이 모두 활성화돼야 사용됩니다. 렌더 버퍼·카메라 조건이 맞지 않으면 Native 출력이 유지됩니다. URP/HDRP 각각의 `NativeFallbackReason`은 마지막 검사 사유를 제공하지만, `null`이 GPU 합성 성공을 보장하지는 않습니다.

자세한 버퍼·스텐실·카메라 조건은 [Presentation의 Native UI 출력 정책](presentation.md#native-ui-출력-정책)을 참고합니다.

## 코드 위치

| 범위 | 경로 |
| --- | --- |
| Runtime, Bootstrapper, Settings | `Runtime/Core` |
| Layout, Layer, Host, Session | `Runtime/Core/Presentation` |
| Screen, Modal, Focus, Input | `Runtime/Core/Policy` 및 `Runtime/Core/Backends` |
| Window | `Runtime/Window` |
| Tray, Bar | `Runtime/Tray` 및 `Runtime/Bar` |
| Validation Scene | `Validation/Scenes` 및 `Validation/Runtime` |

구현 계약의 상세 내용은 [Presentation Architecture](../architecture/presentation.md)를 참고합니다.
