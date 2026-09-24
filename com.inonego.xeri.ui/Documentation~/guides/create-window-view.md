# Window와 View Source 연결하기

이 가이드는 저장 가능한 Window를 `IXeriUIViewSource`와 연결하는 흐름을 설명합니다.

## 1. UI Session

View 재생성 사이에 유지할 UI-local state가 있으면 `IXeriUISession` 구현을 준비합니다.

```csharp
[Serializable]
public sealed class SampleViewSession : IXeriUISession
{
    public string SearchText = string.Empty;
    public int SelectedIndex = -1;
}
```

Window 위치/크기/상태는 `XeriWindowRecord`가 소유하므로 UISession에 중복 저장하지 않습니다.

## 2. View Source

```csharp
public sealed class SampleViewSource : IXeriUIViewSource
{
    public string ID => "sample.view";

    public VisualElement AcquireView(XeriUIViewScope scope)
    {
        var root = new VisualElement();
        root.Add(new Label("Sample"));
        return root;
    }

    public void ReleaseView(
        XeriUIViewScope scope,
        VisualElement view)
    {
        // callback/presenter/pool resource 반환
    }

    public void LoadSession(XeriUIViewScope scope)
    {
        // AcquireView 전에 session state 준비
    }

    public void SaveSession(XeriUIViewScope scope)
    {
        // 현재 UI-local state 기록
    }
}
```

## 3. Resolver

```csharp
var resolver = new XeriUIViewResolver();
resolver.Register(new SampleViewSource());
```

## 4. Workspace

Workspace가 사용할 Presentation target을 정합니다. app-wide Window Layer를 쓰는 경우 Settings의 destination mapping에서 예를 들어 `Window → Desktop.Windows`를 구성합니다.

Workspace 생성:

```csharp
var workspace = new XeriWindowWorkspace(
    runtime.Main,
    PresentationTarget.Host("Window"),
    viewResolver: resolver);
```

Child/local composition 안에서는 `PresentationTarget.Local(layerID)`를 사용할 수 있습니다.

## 5. Record

```csharp
var record = new XeriWindowRecord
{
    ID = "sample.window",
    Title = "Sample Window",
    Pos = new Vector2(80f, 80f),
    Size = new Vector2(480f, 320f),
    NormalPos = new Vector2(80f, 80f),
    NormalSize = new Vector2(480f, 320f),
    ViewSourceID = "sample.view",
    ViewDataKey = "sample-window",
    UISession = new SampleViewSession(),
};

XeriWindowSession window =
    workspace.OpenWindow(record);
```

Workspace lifecycle:

```text
LoadSession
→ AcquireView
→ ContentRoot attach
→ Window lifetime
→ SaveSession
→ detach
→ ReleaseView
```

## 6. Snapshot

```csharp
IReadOnlyList<XeriWindowRecord> records =
    workspace.CaptureRecords();
```

disk serialization 방식과 저장 시점은 프로젝트가 결정합니다.

## 7. Application Window

독립 Screen/Modal stack이 필요한 Window는 Child Layout을 사용합니다.

```csharp
var appOptions =
    new XeriWindowApplicationOptions(appLayout);

XeriWindowSession app = workspace.OpenApplicationWindow(
    "inventory.app",
    "Inventory",
    new Vector2(100f, 100f),
    new Vector2(720f, 560f),
    appOptions);

app.Context.Screens.Open("Inventory.Home");
```

Application Window는 `ContentRoot → Child PresentationSession → Child UIContext` 구조를 소유합니다.

## 8. 종료

```csharp
window.Close();
workspace.Dispose();
```

더 자세한 계약은 [Window](../window/index.md)와 [Window View Source](../window/view-source.md)를 참고합니다.
