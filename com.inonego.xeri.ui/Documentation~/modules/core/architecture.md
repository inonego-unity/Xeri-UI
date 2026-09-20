# UI Core 구조와 수명

## 목적

`UIRuntime`, `UIContext`, Profile, Layer, Screen Session이 어떤 소유 관계로 연결되는지 이해하고, 생성 순서와 종료 순서를 잘못 섞지 않도록 하는 구조 문서입니다.

## 언제 읽는가

- Main/Child Context 중 무엇을 써야 하는지 결정할 때
- Profile과 Layer의 소유자를 정할 때
- Screen/Overlay/Modal이 남아서 Profile 종료가 거부될 때
- Handle과 Session의 정상 종료 순서를 설계할 때

## 전체 구조

일반적인 사용 흐름은 다음과 같다.

```text
UISettingsAsset
    ↓
UIBootstrapperModuleAsset + UIHost.prefab
    ↓
UIRuntime.Current
    ↓
UIRuntime.Main
    ↓
UIProfileAsset 획득
    ↓
ScreenOptions + IScreenSource 등록
    ↓
ScreenController.Open / Replace / Close / Clear
    ↓
Session → Registration → Source → Profile 순서로 종료
```

Runtime과 Context의 책임은 다음과 같이 나뉜다.

```text
UIRuntime
├── Main
│   ├── LayerRegistry
│   ├── ScreenRegistry
│   ├── Screens
│   ├── Modals
│   └── 선택적 Child Context
├── SceneFader
└── Settings
```

| 구성 요소 | 책임 |
|---|---|
| `UIRuntime` | App 범위 UI Runtime과 전역 Scene Fade |
| `UIContext` | 독립 Layer 공간, Screen Registry, Stack, Modal Stack과 Focus 기록 |
| `PresentationLayerRegistry` | UGUI·UITK 공통 Layer ID, Order와 사용 수명 |
| `ScreenRegistry` | Screen ID와 `ScreenOptions`, `IScreenSource`의 대응 관계 |
| `ScreenController` | Context 안의 Screen Stack과 상태 전환 |
| `ModalController` | Modal 순서와 top 상호작용 상태 |
| `FocusController` | Context별 기본·마지막·대체 Focus 기록 |

대부분의 UI는 `runtime.Main`만 사용한다. 별도 Stack과 Focus 기록이 필요한 내장 창 같은
독립 표시 범위만 Child Context로 만든다.

## Layer와 Profile

### Layer 정의

`Assets > Create > Xeri > UI > Presentation Layer`에서
`PresentationLayerAsset`을 만든다.

| 필드 | 의미 |
|---|---|
| `ID` | Screen과 표시 기능이 조회하는 stable string ID |
| `Order` | UGUI와 UI Toolkit이 공유하는 Screen Space Overlay 순서 |

동시에 활성화되는 Layer의 ID와 Order는 각각 고유해야 한다. UGUI와 UI Toolkit은 같은 Order
공간을 사용하므로 Backend가 다르다는 이유로 같은 Order를 중복하지 않는다.

Layer Prefab Root에는 `IPresentationLayerDriver` 구현이 정확히 하나 있어야 한다.

| Driver | 공개 Root | 필수 조건 |
|---|---|---|
| `UGUILayerCanvas` | `RectTransform` | Screen Space Overlay, 기본 Display와 Sorting Layer |
| `UITKLayerPanel` | `VisualElement` | PanelRenderer, 기본 Display, Target Texture 미지정 |

Camera Canvas, World Space Canvas, 다른 Display와 호출자 소유 RenderTexture Panel은 이 공통
Screen Space Overlay Order 계약 밖에서 호출자가 관리한다.

`UITKLayerPanel`은 활성화될 때 다음을 자동 처리한다.

- `PanelRenderer.sortingOrder`에 공통 Layer Order 적용
- PanelRenderer가 생성한 실제 `VisualElement` Root 공개
- Layer Root의 `xeri-ui` Class
- `UIRuntimeBaseline.uss` 적용
- Target Texture 없는 Unity native Screen Space Overlay 직접 출력

6000.7.0a6에서는 PanelRenderer Root getter가 internal이므로 이 버전 종속 접근은 UITK backend 내부에
격리한다. 상위 Core와 사용자 코드는 `IPresentationLayerDriver<VisualElement>`만 사용한다.

### Profile 획득

`Assets > Create > Xeri > UI > Profile`에서 `UIProfileAsset`을 만든다. Entry 하나는
`PresentationLayerAsset`과 Layer Prefab용 `IGameObjectProvider`를 묶는다.

```csharp
UIProfileHandle profileHandle = runtime.AcquireProfile(profile);
```

Scene이나 게임 모드에서 필요한 Profile은 해당 범위의 조립 객체가 소유한다. 포함된 Layer를
사용하는 Screen, Overlay, Modal과 Drag Visual이 모두 종료된 뒤 Handle을 해제한다.

```csharp
profileHandle.Dispose();
profileHandle = null;
```

활성 Layer 소비자가 남아 있으면 종료는 상태 변경 전에 거부되고 소유권이 유지된다. 소비자를
종료한 뒤 같은 Handle로 다시 요청할 수 있다. 실제 종료가 시작된 뒤의 일반 실패는 재시도하지
않는다.

## Main과 Child Context

Main Context는 일반적인 App UI의 기본 표시 범위다. Child Context는 다음 조건을 모두 만족할
때만 만든다.

- Parent와 독립된 Screen Stack 또는 Modal Stack이 필요하다.
- Parent와 별도의 마지막 Focus 기록이 필요하다.
- Child 전체를 하나의 수명으로 종료할 소유자가 있다.

```csharp
UIContext windowContext = runtime.Main.CreateChild(windowLayerRegistry);
windowContext.Focus();

// Window 범위가 끝날 때 Screen, Modal과 하위 Context를 함께 종료한다.
windowContext.Dispose();
```

Layer Registry를 생략하면 Parent와 같은 표시 공간을 공유한다. 특정 Window Root 안에 UI를
배치하려면 해당 Root의 Layer Driver를 별도 `PresentationLayerRegistry`에 등록해 전달한다.
Context는 전달받은 Registry를 소유하거나 해제하지 않는다.

`Focus()`와 `Unfocus()`는 실제 Focus 적용 Context만 전환한다. Visibility, Raycast, Input Map,
Layer 순서나 Screen 상태는 변경하지 않는다.

`UIFocusDriver`는 같은 Host의 `FocusDriverBehaviour` Component를 수집해 하나의
Focus 경로로 합친다. UGUI EventSystem과 UI Toolkit Panel 처리는 각 Driver에 남고,
공통 Driver는 구체 backend이나 대상 타입을 알지 않는다. 현재 Focus가 유효한 대상 없이
끝나면 Focus 권한을 가진 Context의 마지막·화면 기본·Driver 기본·대체 Focus 순서가
다시 적용된다.

## 종료와 소유권

| 획득 결과 | 소유자가 종료할 항목 | 종료 전 조건 |
|---|---|---|
| `UIProfileHandle` | Profile Layer | 해당 Layer 소비자 종료 |
| `ScreenRegistrationHandle` | Screen 등록 | 해당 Source의 열린 Session 종료 |
| `ScreenSession` | View, Layer Usage, Input과 자식 Handle | 정상 Close는 현재 top |
| `Lease<TView>` | `PresentationLease.Acquire<TView>`로 획득한 View와 Layer Usage | 없음 |
| `ModalSession` | Modal Stack 항목과 전달된 소유 수명 | 없음 |
| `Lease` | Visibility, Spotlight 또는 Input Block 요청 | 없음 |
| `DragVisualHandle` | Drag 부모와 Transform | 없음 |
| Child `UIContext` | Screen, Modal, Focus와 하위 Context | 소유 범위 종료 |

일반적인 종료 순서는 다음과 같다.

1. 열린 Screen과 Session 자식을 닫는다.
2. `ScreenRegistrationHandle`을 해제한다.
3. Source, Binding과 선택 기능 객체를 해제한다.
4. 추가 `UIProfileHandle`을 해제한다.
5. App 종료에서만 `runtime.Shutdown()`을 호출한다.

일반 `Dispose`, `Release`와 Callback 정리는 attempt-once다. 정리가 시작된 객체를 재시도
Registry나 복구 상태 머신에 넣지 않는다. 상태 변경 전 사전 조건 거부로 소유권이 유지되는
경우만 일반 종료 실패와 구분한다.
