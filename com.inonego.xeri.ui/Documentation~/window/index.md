# Window

`XeriWindowWorkspace`는 하나의 Presentation placement 안에서 여러 Window의 lifetime, state, focus와 z-order를 관리합니다.

## Workspace

Root `PresentationLayout`에 Workspace가 사용할 UITK placement를 정의합니다.

```text
PresentationID: Window.Workspace
LayerID:         Desktop
LocalOrder:      100
```

Workspace 생성:

```csharp
var workspace = new XeriWindowWorkspace(
    runtime.Main,
    "Window.Workspace");
```

필요하면 Registry, View Resolver, Drag Factory, Animation Options를 주입할 수 있습니다.

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

`CaptureRecords()`는 View Source의 session 저장을 먼저 수행한 뒤 ordered record snapshot을 반환합니다.

disk serialization 형식과 저장 시점은 프로젝트가 소유합니다.

## Tray

Workspace의 Registry를 Tray에 연결할 수 있습니다.

```csharp
XeriWindowTraySource source =
    workspace.CreateTraySource();
```

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
