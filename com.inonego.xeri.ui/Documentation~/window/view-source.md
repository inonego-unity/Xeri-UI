# Window View Source

`IXeriUIViewSource`는 Window content View의 acquire/release와 UI-local session save/load를 담당합니다.

## 핵심 계약

```csharp
public interface IXeriUIViewSource
{
    string ID { get; }

    VisualElement AcquireView(XeriUIViewScope scope);
    void ReleaseView(XeriUIViewScope scope, VisualElement view);

    void SaveSession(XeriUIViewScope scope);
    void LoadSession(XeriUIViewScope scope);
}
```

## XeriUIViewScope

Scope는 다음 정보만 제공합니다.

- `ViewSourceID`
- `ViewDataKey`
- `UISession`

Window chrome이나 `ContentRoot`를 Source에 노출하지 않습니다.

View attach/detach는 `XeriWindowSession`이 소유합니다.

## Source 구현

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
        // AcquireView 전에 UI-local state 준비
    }

    public void SaveSession(XeriUIViewScope scope)
    {
        // 현재 UI-local state 기록
    }
}
```

Source가 반환한 View는 다른 hierarchy에 연결되지 않은 상태여야 합니다.

## Resolver

`XeriUIViewResolver`는 stable Source ID를 Source instance로 해석합니다.

```csharp
var resolver = new XeriUIViewResolver();
resolver.Register(new SampleViewSource());
```

Resolver는 Source registration을 관리하지만 Source 객체의 생성/Dispose lifetime은 프로젝트 owner가 관리합니다.

## Workspace lifecycle

Record 기반 Window를 열 때:

```text
ViewSourceID
→ Resolver
→ XeriUIViewScope
→ LoadSession (IXeriUIViewSessionPersistence를 구현한 경우)
→ AcquireView
→ ContentRoot attach
```

Workspace의 `CaptureRecords()`로 캡처할 때:

```text
Registry의 현재 Window Record snapshot 확보
→ SaveSession (IXeriUIViewSessionPersistence를 구현한 경우)
→ 확보한 Window Record 목록 반환
```

Snapshot은 Window 메타데이터를 복사하지만 `UISession`은 Source의 동일 객체를 참조합니다. `SaveSession`은 `scope.UISession` 객체의 필드를 직접 갱신하며, Scope에서 다른 세션 인스턴스로 교체하지 않습니다. 저장 콜백에서 오류가 발생하면 `CaptureRecords()`는 예외를 전달합니다.

캡처는 열린 Window와 View를 유지합니다. Source는 현재 UI-local state를 기록하고 View 자원을 계속 보유합니다.

Window를 종료할 때:

```text
SaveSession (IXeriUIViewSessionPersistence를 구현한 경우)
→ ContentRoot detach
→ ReleaseView
```

`ViewDataKey`가 있으면 Workspace가 View의 persistence key에 적용합니다.

## 책임 경계

Source:

- View acquire/release
- callback/presenter/pool resource
- `IXeriUISession` 해석

Window:

- Window state
- chrome
- ContentRoot attach/detach
- focus / z-order
- Source 호출 순서

Project:

- `IXeriUISession` concrete 데이터
- 실제 disk serialization
- stable Source ID naming
