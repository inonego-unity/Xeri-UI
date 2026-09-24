# Screen과 입력

Screen은 `UIContext` 안의 navigation/lifecycle stack state입니다. 화면 크기나 full-screen 여부가 Screen을 정의하지 않습니다.

## ScreenOptions

`ScreenOptions`는 Screen registration이 소유하는 navigation/input/cursor/transition policy를 정의합니다.

```csharp
var options = new ScreenOptions(
    "Gameplay.Pause",
    duplicatePolicy: ScreenDuplicatePolicy.Reject,
    blocksGameplayInput: true,
    showsCursor: true,
    cursorLockMode: CursorLockMode.None,
    openDuration: 0.2f,
    closeDuration: 0.2f);
```

정책:

- `ID`
- `DuplicatePolicy`
- `BlocksGameplayInput`
- `ShowsCursor`
- `CursorLockMode`
- `OpenDuration`
- `CloseDuration`

Screen의 표시 위치는 같은 ID의 `PresentationID` placement가 결정합니다.

## 등록

Common API:

```csharp
ScreenRegistrationHandle registration =
    context.RegisterScreen(options, source);
```

`RegisterScreen`은 `ScreenRegistry.Register`를 감싸는 façade입니다.

프로젝트 composition에서 Registry를 직접 다뤄야 하면:

```csharp
ScreenRegistrationHandle registration =
    context.ScreenRegistry.Register(options, source);
```

## 열기와 닫기

```csharp
ScreenOpenResponse response =
    context.Screens.Open("Gameplay.Pause");
```

주요 명령:

- `Open(id, openParams)`
- `Replace(id, openParams)`
- `Close()`
- `Clear()`

Open이 허용되면 `ScreenOpenResponse.Session`으로 해당 instance의 lifetime을 얻습니다.

View 내부에서 자기 Screen을 닫을 때는 전역 Stack을 다시 찾지 않고 Scope가 받은 Session을 사용합니다.

```csharp
scope.Session.Close();
```

## IScreenSource

`IScreenSource`는 Screen View/Driver의 acquire/release origin입니다.

```csharp
public ScreenInstance Acquire(ScreenViewScope scope)
{
    if (scope.Layer is not IPresentationLayerDriver<VisualElement> layer)
    {
        throw new InvalidOperationException();
    }

    VisualElement root = BuildScreenView();
    layer.Root.Add(root);

    var presenter = new ExamplePresenter(root, scope);
    var driver = new UITKScreenDriver(root, presenter.DefaultFocus);
    return new ScreenInstance(driver, presenter);
}

public void Release(ScreenInstance instance)
{
    // callback/binding/view resource 반환
}
```

`ScreenViewScope`는 Screen ID, placement Driver, OpenParams와 `ScreenSession`을 제공합니다.

Acquire가 성공하기 전에 실패하면 그 호출에서 생성한 resource를 즉시 rollback합니다. 성공한 `ScreenInstance`는 `Release`가 대칭적으로 반환합니다.

## Focus

Screen의 Focus source는 Driver/`IFocusScope`입니다.

```text
LastFocus
→ Scope.DefaultFocus
→ backend fallback
```

일반 Screen 코드는 Focus Registry를 직접 다루지 않습니다.

특수 composition에서는 다음 advanced API를 사용할 수 있습니다.

- `RegisterFocusScope`
- `SetPrimaryFocusScope`
- `PushFocusOverride`
- Context Base/Override Authority

Focus precedence는 숫자가 아니라 ownership입니다.

```text
Override.Top
→ Primary
→ Base
```

## Gameplay Input

`BlocksGameplayInput`은 active Context path의 contribution입니다.

active path 안에서 하나라도 gameplay block을 요구하면 gameplay input을 block합니다. inactive sibling Context는 contribution하지 않습니다.

## Cursor

Cursor owner는 ownership topology로 결정합니다.

```text
input-release barrier owner
→ effective Context의 Active Top Screen
→ active ancestor owner
→ captured baseline
```

Covered Screen은 기본 Cursor owner가 아닙니다.

## UITK-only / UGUI mixed

UITK-only Runtime은 UI Toolkit native event path를 사용합니다.

UGUI Layer가 포함된 Runtime은 `EventSystem + InputSystemUIInputModule`을 mixed input path로 사용합니다.

UI input과 Gameplay input은 `UISettingsAsset`의 action asset/map 설정으로 연결합니다.

## Screen state hook

`IScreenStateHandler`은 다음 lifecycle callback을 제공합니다.

| callback | 시점 |
|---|---|
| `OnOpening` | Open transition 시작 전 |
| `OnOpened` | Open transition 완료 |
| `OnClosing` | Close transition 시작 전 |
| `OnClosed` | cleanup 완료 |

동일 Controller에 대한 navigation command를 lifecycle callback 안에서 재진입시키지 않습니다.

## Child lifetime

Screen과 함께 종료돼야 하는 Lease/Handle/Session은 `ScreenSession`에 이전합니다.

```csharp
response.Session.RegisterChild(ownedLifetime);
```

등록된 child lifetime은 Session 종료 시 역순으로 반환됩니다.

## 종료

일반 composition 종료:

1. Screen을 Close/Clear합니다.
2. `ScreenRegistrationHandle`을 Dispose합니다.
3. Source owner가 Source resource를 반환합니다.
4. 상위 `UIContext`/`UIRuntime` owner가 종료합니다.

Registration Dispose는 새 Open 조회를 막지만 이미 열린 Session을 대신 닫지 않습니다.
