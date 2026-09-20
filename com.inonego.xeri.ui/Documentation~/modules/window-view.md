# Xeri Window View

Xeri Window View는 UI Toolkit `VisualElement`를 stable ID로 생성하고, Window 재생성 전후에 UI session을 저장·복원하기 위한 계약입니다.

## 왜 필요한가

Window나 Tool UI가 재생성될 때 `VisualElement` 자체를 저장할 수는 없습니다. 화면 생성 코드와 유지할 UI 상태를 분리하고 stable ID로 다시 해석할 수 있어야 같은 Window record를 복원하거나 다른 Host에서 View를 재구성할 수 있습니다.

## 언제 사용하는가

- Window record에서 ID만으로 View를 다시 생성해야 할 때
- 검색어·선택 상태 등 View-local 상태를 재생성 전후에 유지할 때
- Host/slot 같은 현재 표시 위치를 View Source에 runtime context로 전달해야 할 때

한 번 만들고 버리는 단순 `VisualElement`라면 View Source/Session 구조까지 사용할 필요는 없습니다.

## 기본 사용

```csharp
var resolver = new XeriUIViewResolver();
resolver.Register(new SampleViewSource());

if (resolver.TryGetViewSource("sample.view", out var source))
{
    var scope = new XeriUIViewScope
    (
        "sample.view",
        "sample-key",
        session,
        hostRoot,
        viewSlot
    );

    source.LoadSession(scope);
    VisualElement view = source.CreateView(scope);
}
```

Window와 함께 연결하는 전체 예는 [Xeri Window와 View Source 연결하기](../guides/create-window-view.md)를 참고합니다.

## 핵심 구성

| 타입 | 역할 |
|---|---|
| `IXeriUIViewSource` | View 생성과 session 저장/로드 계약 |
| `IXeriUIViewResolver` | stable ID로 View Source를 조회하는 계약 |
| `XeriUIViewResolver` | 기본 등록/조회 구현 |
| `XeriUIViewScope` | Source 호출에 전달되는 runtime 범위 |
| `IXeriUISession` | View가 이어받을 수 있는 UI 작업 상태 marker |

## 전체 흐름

```text
stable ViewSourceID
      ↓
IXeriUIViewResolver
      ↓
IXeriUIViewSource
      ↓ LoadSession(scope)
      ↓ CreateView(scope)
VisualElement
      ↓
Host / Window
      ↓ SaveSession(scope)
IXeriUISession
```
## Scope

`XeriUIViewScope`는 직렬화 데이터가 아니라 View Source 호출 때 사용하는 runtime 전달 객체입니다.

- `ViewSourceID`: Source의 stable ID
- `ViewDataKey`: UI Toolkit `viewDataKey`로 사용할 stable key
- `UISession`: 재생성 뒤 이어받을 UI 상태
- `HostRoot`: View가 속한 host root
- `ViewSlot`: 생성된 View가 붙을 위치

`VisualElement`는 Unity 직렬화 대상이 아니므로 Scope 자체를 저장하지 않습니다. 저장할 상태는 `IXeriUISession` 구현에 두고 Source가 `SaveSession`/`LoadSession`에서 해석합니다.

## Window와의 연결

`XeriWindowCanvas`는 Window record의 `ViewSourceID`로 Source를 resolve합니다.

1. `LoadSession(scope)`을 먼저 호출합니다.
2. `CreateView(scope)`로 `VisualElement`를 생성합니다.
3. `ViewDataKey`가 있으면 생성된 View에 적용합니다.
4. Window가 정리될 때 같은 Source에 `SaveSession(scope)`을 요청합니다.

따라서 Window는 구체 View 타입을 알 필요가 없고, record에는 stable ID와 session만 남길 수 있습니다.
## 책임 범위

View 시스템은 다음을 책임지지 않습니다.

- Window의 위치, 크기, focus와 transition
- domain document/session 자체의 저장 포맷
- View Source ID의 프로젝트별 naming 정책
- View 내부 상태를 어떤 형태의 `IXeriUISession`으로 보관할지 결정하는 것

`XeriUIViewResolver`는 등록된 Source를 ID로 찾는 최소 registry이며 Source의 생성·파괴 수명을 자동 소유하지 않습니다.

## 관련 문서

- [Xeri Window](window.md)
- [Xeri Tray](tray.md)
