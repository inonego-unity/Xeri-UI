# Window View Source

`IXeriUIViewSource`는 Window가 사용할 **UI Toolkit View를 획득하고 반환하는 계약**입니다. Window의 제목·위치·크기·Focus·Chrome과 View의 생성 방식은 서로 분리됩니다.

View Source는 **Record 기반 Window**에서 사용합니다. 직접 만든 `VisualElement`를 `OpenWindow(id, title, view, ...)`에 전달하는 Simple Window에는 필수가 아닙니다.

## 기본 인터페이스

```csharp
public interface IXeriUIViewSource
{
    string ID { get; }

    VisualElement AcquireView(XeriUIViewScope scope);
    void ReleaseView(XeriUIViewScope scope, VisualElement view);
}
```

| 멤버 | 계약 |
| --- | --- |
| `ID` | Resolver가 사용하는 안정적인 View Source ID |
| `AcquireView(scope)` | 부모가 없는 `VisualElement`를 반환 |
| `ReleaseView(scope, view)` | Source가 획득한 Binding·Presenter·Pool 자원을 대칭 반환 |

Window의 `ContentRoot` attach/detach는 **`XeriWindowSession`이 수행**합니다. Source는 Window Panel이나 ContentRoot를 직접 찾지 않아도 됩니다. Acquire가 반환 전에 실패했다면 Source 내부에서 이미 만든 자원을 정리해야 합니다.

## UI-local 상태 저장은 선택 사항

View의 값이 Window를 닫거나 다시 생성한 뒤에도 유지돼야 하면 `IXeriUIViewSessionPersistence`를 **추가로 구현**합니다.

```csharp
public interface IXeriUIViewSessionPersistence
{
    void SaveSession(XeriUIViewScope scope);
    void LoadSession(XeriUIViewScope scope);
}
```

저장할 UI-local 상태가 없는 Source는 **`IXeriUIViewSource`만 구현**하면 됩니다. 의미 없는 빈 Save/Load 메서드를 포함할 필요가 없습니다.

`XeriUIViewScope`는 다음 값을 읽기 전용으로 제공합니다.

| 속성 | 의미 |
| --- | --- |
| `ViewSourceID` | 호출에 사용된 stable Source ID |
| `ViewDataKey` | UI Toolkit persistence key로 연결할 수 있는 안정적인 키 |
| `UISession` | `XeriWindowRecord`와 공유하는 `IXeriUISession` 객체 |

`UISession` 자체의 실제 데이터 형식과 저장소는 프로젝트가 정합니다. Window 위치·크기·최소화 상태 등은 `XeriWindowRecord`에 있으므로 UI-local Session에 중복 기록하지 않습니다.

## Resolver 등록과 사용

`XeriUIViewResolver`는 ID를 Source 인스턴스로 연결합니다.

```csharp
var resolver = new XeriUIViewResolver();
resolver.Register(new SampleViewSource());

var workspace = new XeriWindowWorkspace(
    runtime.Main,
    PresentationTarget.Host("Window"),
    viewResolver: resolver);
```

`SampleViewSource`와 `Window` Destination은 프로젝트가 정의해야 합니다. Resolver는 Source를 조회·등록하지만, **Source 객체 자체의 생성 및 외부 리소스 종료 수명은 프로젝트 owner가 관리**합니다.

## 실제 호출 순서

Record에 `ViewSourceID`가 지정된 Window를 열 때:

```text
Record.ViewSourceID → Resolver
→ XeriUIViewScope 생성
→ LoadSession (선택적 인터페이스 구현 시)
→ AcquireView → ContentRoot attach
```

현재 Window를 스냅샷으로 저장할 때:

```text
Workspace가 Window Record 목록을 먼저 snapshot
→ SaveSession (선택적 인터페이스 구현 시)
→ Record 목록 반환
```

Window 종료 시:

```text
SaveSession (선택적 인터페이스 구현 시)
→ ContentRoot detach
→ ReleaseView
```

`SaveSession`은 `scope.UISession`을 새 객체로 **교체하는 계약이 아닙니다**. 반환된 Record의 `UISession`은 원래 Source와 공유하는 동일 객체이므로 해당 객체의 **내용을 갱신**해야 합니다.

`CaptureRecords()`는 열린 Window를 종료하지 않으며, SaveSession 중 예외가 발생하면 그 예외를 호출자에게 전달합니다. 저장 시작 시점의 Window Record 목록은 유지되지만, `UISession`은 deep copy가 아니므로 디스크 저장 전에 필요한 값의 스냅샷을 프로젝트가 책임져야 합니다.

## Source·Window·프로젝트의 책임

| 소유자 | 책임 |
| --- | --- |
| **View Source** | View acquire/release, UI Binding·Presenter·Pool, 선택적 Save/Load |
| **Window Session** | Chrome, ContentRoot 부착·분리, 위치·상태·Focus·순서 |
| **Workspace** | Window Session, Registry, Source 호출 순서 |
| **Project** | 구체적 `IXeriUISession` 데이터, ID 규칙, 파일 저장·로드·마이그레이션 |

Window 표시·테마·종료에 대해서는 [Window](index.md), 실제 구현 예시는 [Window와 View Source 연결하기](../guides/create-window-view.md)를 참고합니다.
