# Window

Window는 Xeri UI의 **이동·크기 조절·최소화·최대화·복원·활성화·닫기 기능**을 제공하는 UI Toolkit 구성 요소입니다. `XeriWindowWorkspace`가 같은 Presentation Layer에 있는 여러 Window Session의 수명·포커스·순서를 관리합니다.

## 어떤 Window를 사용할지 선택

| 방식 | 콘텐츠 | 권장 용도 |
| --- | --- | --- |
| **Simple Window** | 직접 전달한 `VisualElement` | 인벤토리, 설정 패널, 단순 도구 창 |
| **Record 기반 Window** | `XeriWindowRecord`와 View Source | 위치·상태·UI session 저장이 필요한 창 |
| **Application Window** | Child PresentationSession + Child UIContext | 독립 Screen·Modal·Focus·Input 흐름이 필요한 앱 |

세 방식 모두 실제 Window의 이동·상태·순서 기능은 공유합니다. Simple Window는 Workspace의 Context를 사용하며 **Window마다 별도의 Child UIContext를 만들지 않습니다.**

## Workspace 생성

Window를 표시할 **UITK PresentationTarget**을 프로젝트 설정에 연결합니다. 예를 들어 `Window` Destination이 Root Layout의 `Desktop.Windows` Layer를 가리키도록 구성합니다.

```csharp
var workspace = new XeriWindowWorkspace(
    runtime.Main,
    PresentationTarget.Host("Window"));
```

Local Child Session 안의 Layer를 사용한다면 `PresentationTarget.Local("Window.Layer")`를 선택합니다. 두 Target은 동일한 ID 네임스페이스가 아닙니다.

Workspace는 독립된 Child Context를 만들고, 첫 Window를 열 때 Window Container용 Core Screen을 표시합니다. Registry, View Resolver, Drag Factory, Animation Options, Theme Resolver는 필요할 때 생성자 인자로 대체할 수 있습니다.

## Simple Window 열기

부모가 없는 View를 프로젝트에서 만든 뒤 직접 전달합니다.

```csharp
VisualElement inventoryView = BuildInventoryView();

XeriWindowSession window = workspace.OpenWindow(
    "inventory",
    "Inventory",
    inventoryView,
    new Vector2(80f, 80f),
    new Vector2(520f, 420f));
```

`BuildInventoryView()`는 프로젝트가 구현하는 View 생성 함수입니다. 직접 전달한 View 객체의 생성·파괴와 View 내부의 binding 해제는 프로젝트가 담당합니다. Workspace는 Window ContentRoot에 View를 연결하고 Session 종료 시 분리합니다.

## Record와 View Source로 열기

`XeriWindowRecord`에 ID, 제목, 위치, 크기, 상태, View Source ID와 선택적 `UISession`을 저장할 수 있습니다.

```csharp
XeriWindowSession window = workspace.OpenWindow(record);
```

`record.ViewSourceID`가 있으면 Workspace는 `IXeriUIViewResolver`를 통해 Source를 찾고 `AcquireView`를 호출합니다. Source가 선택적 `IXeriUIViewSessionPersistence`를 구현했다면 `LoadSession`과 `SaveSession`도 사용합니다.

다음 사용 계약은 [Window View Source](view-source.md), 실제 작성 순서는 [Window와 View Source 연결하기](../guides/create-window-view.md)를 참고합니다.

## Application Window 열기

Window 내부에 독립된 Screen/Modal stack과 Focus/Input 권한이 필요하면 Child Layout을 제공합니다.

```csharp
var appOptions = new XeriWindowApplicationOptions(appLayout);

XeriWindowSession app = workspace.OpenApplicationWindow(
    "inventory.app",
    "Inventory",
    new Vector2(100f, 100f),
    new Vector2(720f, 560f),
    appOptions);
```

```text
XeriWindowPanel
└─ ContentRoot
   └─ Child PresentationSession
      └─ Child UIContext
         ├─ Screen
         └─ Modal
```

`app.Context`에서 Application Window 내부 Screen과 Modal을 조작합니다.

```csharp
app.Context.Screens.Open("Inventory.Home");
```

예시 Screen ID는 해당 Context에 미리 등록돼 있어야 합니다. Application A/B처럼 동일 Child Layout과 Screen ID를 사용해도 **각 Window의 Child Context·Session은 별개**입니다. `app.Context`와 `app.Context.Presentation`은 조회·조립에 사용하지만 직접 `Dispose()`하지 않습니다.

## Window 명령과 상태

`XeriWindowSession`이 일반적인 Window 명령의 진입점입니다.

```csharp
window.Focus();
window.BringToFront();
window.SendToBack();
window.SetStackLayer(XeriWindowStackLayer.AlwaysOnTop);
window.Close();
```

Window state와 좌표 변경은 내부 `XeriWindowController`가 관리합니다. UI의 Title Bar·Resize Handle은 해당 Controller에 입력을 연결합니다.

- `Normal`: 현재 위치·크기 사용
- `Minimized`: Window 내용 숨김, Tray에서 복구 가능
- `Maximized`: Workspace 작업 영역에 맞춰 표시
- `Closed`: Window Session 종료

최소화에서 돌아올 때는 **최소화 이전 표시 상태**를 복원합니다. 활성 Window 변경은 Focus authority와 연결되지만 Window의 z-order를 Presentation LayerOrder와 혼동하지 않습니다.

## 표시 순서와 Focus

```text
Presentation LayerOrder / LocalOrder
  ≠ Window StackLayer
  ≠ Window instance order
  ≠ Context Focus authority
```

`BringToFront`와 `SendToBack`은 같은 StackLayer 안에서 Window 순서를 변경합니다. `SetStackLayer`는 coarse z-group을 변경합니다.

Simple Window는 Workspace Context의 Focus Scope를, Application Window는 해당 Child Context의 authority를 사용합니다. Window를 전환해도 각 Scope의 마지막 Focus는 독립적으로 유지됩니다.

## USS 테마

내장 테마 ID는 `windows`, `mac`, `minimal`입니다. 테마 변경 API는 **USS 클래스 문자열이 아니라 Theme ID**를 받습니다.

```csharp
window.Panel.ApplyTheme("mac");
```

커스텀 테마는 `XeriWindowThemeAsset`에 고유 ID, `WindowStyleSheet`·`WindowThemeStyleSheet`를 넣고 `XeriWindowThemeResolver`에 등록합니다.

```csharp
var themes = new XeriWindowThemeResolver();
themes.Register(customWindowTheme);

var workspace = new XeriWindowWorkspace(
    runtime.Main,
    PresentationTarget.Host("Window"),
    themeResolver: themes);
```

`customWindowTheme`은 프로젝트가 생성한 Asset입니다. 등록되지 않은 Theme ID는 기본 `windows`로 폴백합니다.

현재 적용된 테마는 Panel → Controller의 `ThemeID` → Registry Record Snapshot으로 이어집니다. `CaptureRecords()`에서도 같은 ID를 사용합니다. 테마 등록 후 Asset의 ID는 변경하지 마세요. 커스텀 Asset이 교체하는 범위는 **Window USS**이며 UXML·Tray의 외관 구조를 변경하지 않습니다.

## 상태 저장과 복원

`XeriWindowRecord`는 Window 위치·크기·상태·StackLayer·ThemeID와 View Source 정보, 선택적 UI session을 표현합니다.

```csharp
IReadOnlyList<XeriWindowRecord> records =
    workspace.CaptureRecords();
```

`CaptureRecords()`는 **현재 Registry 상태를 먼저 스냅샷**으로 확보한 뒤, 살아 있는 View Source의 `SaveSession`을 호출합니다. Source가 값을 갱신하는 `UISession`은 별도 deep copy가 아니라 **기존 Session 객체와 동일한 참조**입니다. 저장 시 Window가 닫혀도 캡처 시작 시점의 Window Record 목록이 기준입니다.

저장 callback이 실패하면 `CaptureRecords()`는 예외를 반환하지 않고 **던집니다**. 실제 디스크 직렬화·저장 시점·버전 마이그레이션은 프로젝트가 결정합니다.

## Tray와 연결

`XeriWindowTraySource`는 Workspace Registry에서 현재 Window 상태를 항목으로 투영합니다.

```csharp
var allWindows = workspace.CreateTraySource();
var minimized = workspace.CreateTraySource(
    XeriWindowTrayStateMask.Minimized);
```

기본 Source에는 `Normal`·`Maximized`·`Minimized`가 포함됩니다. 항목 선택 시 Source의 `Activate(entry)`는 최소화 이전 상태로 복원하거나 현재 창을 활성화합니다. 실제 Tray 연결 예시는 [Tray](../tray.md)를 참고합니다.

## 소유권과 종료

Window 하나를 닫으려면 `window.Close()` 또는 `window.Dispose()`를 사용합니다. Workspace 전체를 종료할 때는 `workspace.Dispose()`를 호출합니다.

`XeriWindowSession.RegisterChild`로 등록한 콘텐츠의 수명은 Window/Child Context 종료 시 **역순으로 한 번 반환**합니다. 하나의 정리 실패가 다른 콘텐츠의 정리를 막지 않도록 오류를 수집합니다.

상위 Registry의 `OnRegister`는 완료된 등록의 관찰 알림입니다. Observer 예외는 기록하고 다른 Observer 호출을 계속하지만, Observer가 등록을 제거한 경우 이전 Handle을 성공으로 반환하지 않습니다. Registry/Tray 인터페이스의 사용자 정의 이벤트 `add`·`remove`가 **부분 실행 후 예외를 발생시키는 경우의 복구는 지원 계약에 포함하지 않습니다**.

고급 조립이 필요한 프로젝트에서만 `XeriWindowController`, `IXeriWindowRegistry`, `IXeriWindowStateTransitioner`, Drag Factory와 cursor adapter를 직접 사용합니다. 이것들은 표준 Workspace의 상태를 병렬 수정하기 위한 API가 아니라 **대체 composition을 만드는 경계**입니다.
