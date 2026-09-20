# UI Core Screen과 입력

## 목적

Screen 하나를 `ScreenOptions + IScreenSource + ScreenInstance`로 구성하고, View 획득/반환·Focus·Gameplay Input 차단을 같은 Screen Session 수명 안에서 연결하는 방법을 설명합니다.

## 언제 읽는가

- 새 Screen Source를 구현할 때
- Screen Open/Replace/Close 동작과 중복 정책을 정할 때
- View callback과 입력 상태를 Screen 종료에 맞춰 정리해야 할 때
- Focus가 예상대로 복원되지 않을 때

## Screen 구현

Screen 하나는 세 계약으로 구성한다.

| 계약 | 책임 |
|---|---|
| `ScreenOptions` | ID, Layer, 중복, Focus, Input과 Transition 정책 |
| `IScreenSource` | View 획득, Binding과 대칭 반환 |
| `ScreenInstance` | `IScreenDriver`와 선택적 `IScreenStateHandler` |

### Options

```csharp
var options = new ScreenOptions
(
    id: "example.screen",
    layerID: "Screen",
    duplicatePolicy: ScreenDuplicatePolicy.Reject,
    blocksGameplayInput: true,
    showsCursor: true,
    cursorLockMode: CursorLockMode.None,
    inputPriority: 100,
    openDuration: 0.2f,
    closeDuration: 0.2f,
    usesUnscaledTime: true
);
```

Focus 후보는 다음 순서로 선택된다.

1. 유효한 `ScreenOptions.DefaultFocus`
2. 유효한 `IScreenDriver.DefaultFocus`
3. Focus Driver fallback

동적으로 생성되는 View의 기본 Focus는 Source가 생성한 Driver에 전달한다.

### Source

`IScreenSource.Acquire`는 한 호출 안에서 다음 작업을 끝낸다.

1. `scope.Layer`를 필요한 Root 타입으로 확인한다.
2. View를 생성하거나 Provider에서 획득한다.
3. `scope.OpenParams.Payload`를 필요한 타입으로 확인한다.
4. Button Callback과 데이터 Binding을 연결한다.
5. `ScreenInstance`를 반환한다.

중간에 실패하면 이번 호출에서 만든 View와 Binding을 즉시 정리하고 예외를 전달한다. 실패한
View를 Source 소유 목록에 남기지 않는다.

`Release`는 Callback과 Binding을 해제하고, Source 소유 매핑에서 제거한 뒤 View를 원래
공급 경로로 반환한다.

```csharp
public sealed class ExampleScreenSource : IScreenSource
{
    public ScreenInstance Acquire(ScreenViewScope scope)
    {
        // Layer 확인 → View 획득 → Payload 검증 → Binding 연결
        return new ScreenInstance(driver, stateHandler);
    }

    public void Release(ScreenInstance instance)
    {
        // Binding 해제 → View 반환
    }
}
```

UGUI Source는 `IPresentationLayerDriver<RectTransform>`을 요구하고 View Prefab의
`UGUIScreenDriver`를 반환한다. UI Toolkit Source는
`IPresentationLayerDriver<VisualElement>`을 요구하고 `VisualTreeAsset`을 Clone한다.

```csharp
var screenRoot = viewAsset.Instantiate();
layer.Root.Add(screenRoot);

var defaultFocus = screenRoot.Q<VisualElement>("DefaultFocus");
var driver = new UITKScreenDriver(screenRoot, defaultFocus);
return new ScreenInstance(driver, stateHandler);
```

UI Toolkit Source는 `Release`에서 Callback과 Binding을 먼저 해제한 뒤 생성한 Root에
`RemoveFromHierarchy()`를 호출한다.

### 등록과 Open

Screen Layer를 포함한 Profile을 먼저 획득한 뒤 등록한다.

```csharp
ScreenRegistrationHandle registration = context.ScreenRegistry.Register
(
    options,
    source
);
```

```csharp
ScreenOpenResponse response = context.Screens.Open
(
    "example.screen",
    new ScreenOpenParams(payload)
);

if (!response.Accepted)
{
    Debug.LogError($"{response.Kind}: {response.Error}");
}
```

`Payload`는 호출자가 소유한다. Source는 타입을 확인해 읽지만 Payload의 수명을 종료하지 않는다.

| `ScreenOpenKind` | 의미 |
|---|---|
| `Accepted` | Session 생성과 Open 시작 수락 |
| `Rejected` | 등록, 중복 정책 또는 현재 상태에서 거부 |
| `Cancelled` | `OnOpening`에서 취소 |
| `SourceFailed` | View 획득 또는 Binding 실패 |
| `TransitionFailed` | Open Transition 시작 실패 |

### Stack 명령

```csharp
context.Screens.Open("example.first");
context.Screens.Replace("example.second");
context.Screens.Close();
context.Screens.Clear();
```

| 명령 | 동작 |
|---|---|
| `Open` | 현재 top을 Covered로 만들고 새 Screen 추가 |
| `Replace` | 새 Open이 수락되면 기존 top 대체 |
| `Close` | 현재 top을 정상 Transition으로 닫음 |
| `Clear` | 모든 Screen을 최신 항목부터 Transition 없이 종료 |

View의 닫기 Button은 전역 Stack을 다시 조회하지 않고 자신이 받은 Session을 닫는다.

```csharp
scope.Session.Close();
```

### 상태 훅과 자식 수명

선택적 `IScreenStateHandler`는 동기 훅을 제공한다.
MonoBehaviour 기반 Screen은 `ScreenStateBehaviour`를 상속해 필요한 훅만 재정의할 수 있다.

| 훅 | 시점 |
|---|---|
| `OnOpening` | Open Transition 전, 취소 가능 |
| `OnOpened` | Open Transition 완료 후 |
| `OnClosing` | 정상 Close Transition 전, 취소 가능 |
| `OnClosed` | 자식 Handle과 Close Transition 정리 후 |

훅 안에서 다른 `Open`, `Replace`, `Close` 또는 `Clear`를 재진입 호출하지 않는다. 후속 명령은
훅이 반환된 다음 실행한다.

Screen과 함께 닫을 Handle은 Session에 넘긴다.

```csharp
response.Session.RegisterChild(ownedHandle);
```

등록된 자식은 Session 종료 시 등록 역순으로 한 번 해제된다.

## Focus와 Input

Screen Stack이 바뀌면 Runtime은 다음 상태를 합성한다.

- Options 또는 Driver의 기본 Focus
- Screen별 마지막 Focus와 fallback
- Gameplay Action별 기존 활성 상태
- UI Action Map 활성 상태
- Cursor 표시와 Lock Mode
- `InputPriority`

Screen Source가 별도 전역 Focus Manager나 Input Map 전환 코드를 만들지 않는다.

Host의 `InputSystemUIInputModule`에는 Point, Move, Submit, Cancel과 Click Action Reference가
실제로 연결되어 있어야 한다. Xeri의 UI Input Actions와 프로젝트 Gameplay Input Asset은
별도 에셋으로 유지한다.

마지막 입력 장치 변경은 Runtime에서 구독할 수 있다.

```csharp
runtime.OnLastInputDeviceChanged += HandleInputDeviceChanged;
```

구독한 소유자는 Runtime 종료 전에 같은 Callback을 해제한다.
