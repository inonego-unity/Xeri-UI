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

Screen ID와 표시 위치는 분리됩니다. Registration이 별도의 `PresentationTarget`을 소유하며,
ScreenController는 Open 시 `UIContext.AcquireLayer(target)`으로 Layer Lease를 획득한 뒤 Source에 최종 Layer Driver만 전달합니다.

```text
Screen ID
!= PresentationTarget
!= Layer ID
!= Host destination ID
```

## 등록

Tier 1 Feature API:

```csharp
ScreenRegistrationHandle registration =
    context.RegisterScreen(
        options,
        PresentationTarget.Host(PresentationDestinationID.Application),
        source);
```

`RegisterScreen`은 `ScreenRegistry.Register`를 감싸는 façade입니다. `context.ScreenRegistry`의 lifetime은 `UIContext`가 소유하므로 직접 `Dispose()`하지 않습니다.

프로젝트 composition에서 Registry를 직접 다뤄야 하면:

```csharp
ScreenRegistrationHandle registration =
    context.ScreenRegistry.Register(
        options,
        PresentationTarget.Local("Application.Screen"),
        source);
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

`ScreenViewScope`는 Screen ID, resolved Layer Driver, OpenParams와 `ScreenSession`을 제공합니다.

`IScreenSource`는 Host topology를 알 필요가 없습니다. `ScreenController`가 Target을 resolve해 Layer Lease를 소유하고
Source에는 backend-specific 최종 Layer Driver만 전달합니다.

Acquire가 성공하기 전에 실패하면 그 호출에서 생성한 resource를 즉시 rollback합니다. 성공한 `ScreenInstance`는 `Release`가 대칭적으로 반환합니다.

## Focus

Screen의 Focus source는 Driver/`IFocusScope`입니다.

```text
LastFocus
→ Scope.DefaultFocus
→ backend fallback
```

일반 Screen 코드는 Focus Registry를 직접 다루지 않습니다.

특수 composition에서는 다음 Tier 2 API를 사용할 수 있습니다.

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

Runtime bootstrap은 `UISettingsAsset.BackendSupport`를 기준으로 지원 backend를 고정합니다.

UGUI capability가 포함되면 시작 시 `EventSystem + InputSystemUIInputModule`을 검증합니다.
현재 active Layout만 보고 infrastructure capability를 추론하지 않습니다.

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


## 상태 관찰과 실패 경계

`ScreenController.OnStackChanged`는 현재 stack을 관찰하는 알림입니다. Context authority와 Cursor policy의 필수 적용 경로와 분리되어 있으며, observer 예외는 로그로 보고하고 다른 observer를 계속 호출합니다. 이 예외 때문에 이미 수락된 Screen을 롤백하지 않습니다. Observer의 재진입으로 stack이 다시 바뀌면 이전 알림의 남은 호출은 중단합니다.

Modal의 `OnStackChanged`에도 같은 관찰 경계를 적용합니다. Open 알림에서 Modal이 종료되면 종료된 Session을 성공 결과로 반환하지 않습니다.
