# Window

`XeriWindowWorkspace`는 하나의 `PresentationTarget` Layer 안에서 여러 Window의 lifetime, state, focus와 z-order를 관리합니다.

## Workspace

Workspace가 표시될 target을 명시합니다. app-wide Window Layer는 Host destination으로 노출하는 구성이 일반적입니다.

```text
Destination: Window
→ Root Layer: Desktop.Windows
```

Workspace 생성:

```csharp
var workspace = new XeriWindowWorkspace(
    runtime.Main,
    PresentationTarget.Host("Window"));
```

현재 Session의 local Layer에 Workspace를 둘 경우에는 `PresentationTarget.Local("Window.Layer")`을 사용합니다.

필요하면 Registry, View Resolver, Drag Factory, Animation Options, Theme Resolver를 주입할 수 있습니다.

## Simple Window

caller-owned View를 직접 열 수 있습니다.

```csharp
XeriWindowSession window = workspace.OpenWindow(
    "inventory",
    "Inventory",
    inventoryView,
    new Vector2(80f, 80f),
    new Vector2(520f, 420f));
```

Simple Window는 Workspace Context 안에서 persistent Focus Scope를 사용합니다. 별도 Child `PresentationSession`이나 Child `UIContext`를 만들지 않습니다.

## Record 기반 Window

저장 가능한 Window는 `XeriWindowRecord`를 사용합니다.

```csharp
XeriWindowSession window =
    workspace.OpenWindow(record);
```

`ViewSourceID`가 있으면 Workspace가 `IXeriUIViewResolver`를 통해 View Source를 해석합니다.

View Source lifecycle은 [Window View Source](view-source.md)를 참고합니다.

## Application Window

독립 Screen/Modal/Focus/Input state가 필요한 Window는 Child Layout을 사용합니다.

```csharp
var appOptions =
    new XeriWindowApplicationOptions(appLayout);

XeriWindowSession app = workspace.OpenApplicationWindow(
    "inventory.app",
    "Inventory",
    new Vector2(100f, 100f),
    new Vector2(720f, 560f),
    appOptions);
```

Application Window 내부 구조:

```text
XeriWindowPanel.ContentRoot
→ Child PresentationSession
→ Child UIContext
```

`app.Context`에서 Application Window 내부 Screen/Modal을 엽니다.

```csharp
app.Context.Screens.Open("Inventory.Home");
```

Application Window의 Child `PresentationSession`과 Child `UIContext` 수명은 Window Session이 소유합니다. `app.Context`와 `app.Context.Presentation`은 조회·composition에 사용할 수 있지만 직접 `Dispose()`하지 않습니다.

각 Application Window의 Presentation topology와 UIContext state는 sibling Window와 독립적입니다.

## ContentRoot

`ContentRoot`는 Window chrome과 application content의 physical boundary입니다.

```text
XeriWindowPanel
├─ TitleBar / Controls / Resize Handles
└─ ContentRoot
   ├─ Simple Window View
   └─ Child PresentationSession
```

## Window command

`XeriWindowSession`이 일반 명령 진입점입니다.

```csharp
window.Focus();
window.BringToFront();
window.SendToBack();
window.SetStackLayer(XeriWindowStackLayer.AlwaysOnTop);
window.Close();
```

state transition과 bounds 변경은 내부 `XeriWindowController`가 관리합니다.

## Ordering

Window ordering은 Presentation ordering과 별개입니다.

```text
Presentation LayerOrder
!= Presentation LocalOrder
!= Window StackLayer
!= Window instance z-order
```

`BringToFront` / `SendToBack`은 같은 StackLayer 안의 instance order를 변경합니다.

`SetStackLayer`는 Window-specific coarse z-group을 변경합니다.

## Focus와 authority

Simple Window:

```text
Window activation
→ Workspace Context Primary Focus Scope 변경
```

Application Window:

```text
Window activation
→ Application Child Context authority 활성화
```

Window를 전환해도 각 Window/Context의 LastFocus는 독립적으로 유지됩니다.

## Persistence

`XeriWindowRecord`는 Window 위치, 크기, 상태, StackLayer와 View Source/session 정보를 보관합니다.

현재 live state snapshot:

```csharp
IReadOnlyList<XeriWindowRecord> records =
    workspace.CaptureRecords();
```

`CaptureRecords()`는 Registry의 현재 Window 상태(적용된 Theme ID 포함)를 먼저 snapshot으로 확보한 뒤, 살아 있는 View Source의 `SaveSession`을 호출합니다. 저장 중 Window가 닫혀도 시작 시점의 record 목록은 유지됩니다. `UISession`은 deep copy가 아니라 Source와 공유하는 동일 객체이므로 `SaveSession`은 해당 객체의 값을 갱신해야 합니다. 저장 콜백이 실패하면 예외를 전달합니다.

disk serialization 형식과 저장 시점은 프로젝트가 소유합니다. View Source의 자세한 수명 계약은 [Window View Source](view-source.md)를 참고합니다.

## Window 테마

기본 테마 ID는 `windows`, `mac`, `minimal`입니다. `XeriWindowPanel.ApplyTheme(themeID)`는 **테마 ID**만 받으며, USS 클래스 문자열은 입력 계약이 아닙니다. Resolver에서 해석되지 않는 임의의 ID는 `windows`로 폴백합니다.

커스텀 테마는 `XeriWindowThemeAsset`에 고유 ID와 Window USS를 등록한 뒤 Resolver를 Workspace에 전달합니다.

```csharp
var themes = new XeriWindowThemeResolver();
themes.Register(customWindowTheme);

var workspace = new XeriWindowWorkspace(
    runtime.Main,
    PresentationTarget.Host("Window"),
    themeResolver: themes);
```

`window.Panel.ApplyTheme("custom")`처럼 실행 중 테마를 바꿀 수 있습니다. 해당 Panel과 `window.Controller.ThemeID`, `Registry.TryGetRecord()`, `CaptureRecords()`의 Theme ID가 일치하도록 연결됩니다. Window 등록 이벤트에서도 이미 적용된 Theme ID를 조회할 수 있습니다.

- `WindowStyleSheet` 및 `WindowThemeStyleSheet`는 Panel에 적용됩니다. 커스텀 테마에는 Windows 기본 클래스와 `xeri-window--theme-custom` 클래스가 함께 붙습니다.
- 지원 범위는 Window USS 스타일 전환입니다. UXML 구조 교체와 Tray 테마는 이 Asset의 계약에 포함하지 않습니다.
- Theme ID는 Resolver에 등록한 동안 변경하지 않습니다. ID를 바꿔야 한다면 새 Resolver를 구성해 등록합니다.
- 외부에서 `IXeriWindowRegistry`를 구현한다면 `XeriWindowController.ThemeID`를 Registry snapshot에 반영해야 Window 테마 상태를 정확하게 반환합니다. Registry 자체에는 테마 변경 명령이 없으며 실제 변경 진입점은 Panel입니다.

## Tray

Workspace의 Registry를 Tray에 연결할 수 있습니다.

```csharp
XeriWindowTraySource source =
    workspace.CreateTraySource();
```

기본 Source는 모든 live Window 표시 상태(`Normal`, `Minimized`, `Maximized`)를 공급합니다.
Tray별 state projection은 `XeriWindowTrayStateMask`로 제한할 수 있습니다.

```csharp
XeriWindowTraySource minimizedSource =
    workspace.CreateTraySource(
        XeriWindowTrayStateMask.Minimized);
```

Tray entry 선택은 `source.Activate(entry)`를 사용합니다.
Minimized Window는 최소화 이전 상태로 복구하고, 이미 표시 중인 Window는 상태를 바꾸지 않고 활성화합니다.

반환된 Source lifetime은 호출자가 소유합니다.

## 종료

Window 하나:

```csharp
window.Close();
// 또는 lifetime 자체를 종료
window.Dispose();
```

Workspace 전체:

```csharp
workspace.Dispose();
```

Workspace는 살아 있는 Window Session과 내부 Context/Screen registration을 정리합니다.

## Advanced composition

표준 Workspace보다 낮은 레벨이 필요하면 다음 계약을 직접 사용할 수 있습니다.

- `XeriWindowController`
- `IXeriWindowRegistry` / `XeriWindowRegistry`
- `IXeriWindowDriver`
- `IXeriWindowStateTransitioner`
- `IXeriWindowDragFactory`
- theme / resize cursor adapter

이 API는 표준 Workspace의 내부 state를 병렬 수정하는 backdoor가 아니라 대체 composition을 만드는 경계입니다.


## 콘텐츠 종료와 등록 알림

`XeriWindowSession.RegisterChild`로 등록한 콘텐츠는 창 또는 Application Context 중 먼저 끝나는 경계에서 역순으로 한 번 반환합니다. 콘텐츠 정리가 시작되면 새 등록을 거부하며, 한 항목이 실패해도 다른 콘텐츠와 Context 정리를 계속합니다.

Registry의 `OnRegister`는 완료된 등록의 관찰 알림입니다. Observer 예외는 로그로 보고하고 등록을 유지합니다. Observer가 등록을 제거하거나 교체한 경우에는 이전 등록의 남은 알림을 중단하고, 바깥 `Register`는 유효하지 않은 Handle을 성공 결과로 반환하지 않습니다.
