# 저장 가능한 Window 만들기

이 가이드는 **UI Toolkit View Source를 등록하고 Window를 열어 UI 상태를 저장하는 최소 구성**을 설명합니다. Runtime과 `Window` Presentation Destination은 [프로젝트 설정과 시작](../getting-started/setup.md)에 따라 준비돼 있다고 가정합니다.

## 1. UI-local 상태 데이터

Window의 위치·크기·상태는 `XeriWindowRecord`가 소유합니다. View 안의 입력값처럼 Window 내용을 다시 만들어도 유지할 데이터만 `IXeriUISession`에 둡니다.

```csharp
[Serializable]
public sealed class SearchWindowSession : IXeriUISession
{
    public string SearchText = string.Empty;
}
```

이 Session은 `XeriWindowRecord.UISession`에 저장됩니다.

## 2. View Source 구현

다음 예시는 현재 검색어를 `TextField`에 표시하고 `SaveSession`에서 **기존 Session 객체를 갱신**합니다. Source의 저장 대상 View는 Window별 Scope로 구분합니다.

```csharp
public sealed class SearchViewSource :
    IXeriUIViewSource, IXeriUIViewSessionPersistence
{
    public string ID => "search.view";

    private readonly Dictionary<XeriUIViewScope, TextField> fields = new();

    public void LoadSession(XeriUIViewScope scope)
    {
        if (scope.UISession is SearchWindowSession state)
        {
            state.SearchText ??= string.Empty;
        }
    }

    public VisualElement AcquireView(XeriUIViewScope scope)
    {
        var root = new VisualElement();
        var field = new TextField("Search")
        {
            value = (scope.UISession as SearchWindowSession)?.SearchText
                ?? string.Empty,
        };
        root.Add(field);
        fields.Add(scope, field);
        return root;
    }

    public void SaveSession(XeriUIViewScope scope)
    {
        if (scope.UISession is SearchWindowSession state &&
            fields.TryGetValue(scope, out var field))
        {
            state.SearchText = field.value;
        }
    }

    public void ReleaseView(XeriUIViewScope scope, VisualElement view)
    {
        fields.Remove(scope);
        // 프로젝트 Presenter, event binding, pool 자원도 여기서 해제합니다.
    }
}
```

예제에는 `System`, `System.Collections.Generic`, `UnityEngine.UIElements`, `inonego.Xeri.UI.Window` 관련 using이 필요합니다. View 객체는 **다른 부모에 연결되지 않은 상태**로 반환해야 합니다. 상태 저장이 필요 없다면 `IXeriUIViewSessionPersistence` 자체를 구현하지 않습니다.

## 3. Resolver와 Workspace

```csharp
var resolver = new XeriUIViewResolver();
resolver.Register(new SearchViewSource());

var workspace = new XeriWindowWorkspace(
    runtime.Main,
    PresentationTarget.Host("Window"),
    viewResolver: resolver);
```

`Window` Destination은 Settings에 미리 매핑된 UITK Root Layer를 가리킵니다. Child/local 구성이라면 `PresentationTarget.Local(layerID)`를 사용합니다.

## 4. Window Record와 생성

```csharp
var record = new XeriWindowRecord
{
    ID = "search.window",
    Title = "Search",
    Pos = new Vector2(80f, 80f),
    Size = new Vector2(480f, 320f),
    NormalPos = new Vector2(80f, 80f),
    NormalSize = new Vector2(480f, 320f),
    ViewSourceID = "search.view",
    ViewDataKey = "search-window",
    UISession = new SearchWindowSession(),
};

XeriWindowSession window = workspace.OpenWindow(record);
```

Record의 `ViewSourceID`가 Resolver에 등록돼 있어야 합니다. Source는 UI 내용만 만들고 Window Chrome·Title Bar·Resize Handles는 Workspace가 생성합니다.

## 5. 현재 Window 상태 캡처

```csharp
IReadOnlyList<XeriWindowRecord> records =
    workspace.CaptureRecords();
```

Capture는 현재 Window 목록과 적용된 테마·상태를 먼저 기록한 뒤 각 View Source의 `SaveSession`을 호출합니다. 위 예제는 `SearchWindowSession.SearchText`를 같은 객체에 갱신합니다. 프로젝트는 결과를 실제 저장 포맷으로 직렬화하고, 다음 실행에서 Record와 Session을 복원해 다시 `OpenWindow(record)`를 호출합니다.

`CaptureRecords()`는 Window를 닫지 않습니다. `UISession`을 deep copy하지 않기 때문에 불변 스냅샷이 필요하면 프로젝트에서 값을 별도로 복사합니다.

## 6. Window 종료와 프로젝트 정리

```csharp
window.Close();
workspace.Dispose();
```

Window 종료 시 SaveSession → detach → ReleaseView 순서로 Source를 반환합니다. `workspace.Dispose()`는 아직 열린 Window Session을 정리합니다. View Source가 별도 서비스나 Pool을 소유하면 프로젝트 owner가 그 수명도 정리합니다.

## 독립적인 Application Window가 필요하면

일반 View를 넘어 Screen/Modal을 독립적으로 운영해야 하는 경우, 별도 Child Layout을 가진 Application Window를 선택합니다.

```csharp
var applicationOptions =
    new XeriWindowApplicationOptions(childLayout);

XeriWindowSession app = workspace.OpenApplicationWindow(
    "search.app", "Search",
    new Vector2(100f, 100f),
    new Vector2(720f, 560f),
    applicationOptions);
```

Application Window의 `app.Context`에 Screen을 등록한 뒤 `app.Context.Screens.Open(id)`로 조작합니다. Source 기반 Simple Window와는 **다른 ownership 모델**입니다.

전체 상태·테마·Tray 명세는 [Window](../window/index.md), 선택적 Session 수명은 [View Source](../window/view-source.md)를 참고합니다.
