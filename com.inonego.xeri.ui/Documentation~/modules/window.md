# Xeri Window

Xeri Window는 일반적인 패널 표시를 넘어 이동, 크기 변경, 최소화, 최대화, 포커스, 닫기와 화면 정렬을 하나의 상태 모델로 관리하는 커스텀 윈도우 시스템입니다.
`XeriWindowController`가 개별 윈도우 상태를, `XeriWindowRegistry`가 여러 윈도우의 등록·활성·정렬·저장 record를 관리합니다.

## 왜 필요한가

일반 패널에 이동·크기 변경·최소화·포커스·저장 복원 기능을 각각 붙이면 상태 전환과 z-order, View 데이터 수명이 서로 얽히기 쉽습니다. Xeri Window는 창 자체의 상태를 Registry/Record로 분리하고 실제 화면 내용은 View Source/Session에 맡깁니다.

## 언제 사용하는가

- 게임 내 툴, 에디터형 UI처럼 여러 창을 동시에 배치할 때
- Window 위치·크기·상태를 저장하고 다시 복원해야 할 때
- View 내용과 Window chrome/lifecycle을 분리하고 싶을 때
- Tray와 최소화/복원 흐름을 연결해야 할 때

단순 Screen 전환 UI라면 이 패키지의 UI Core Screen/Modal 구조가 더 적합할 수 있습니다.

## 기본 사용

직접 `VisualElement`를 전달하는 가장 작은 사용은 `XeriWindowCanvas`에서 시작합니다.

```csharp
var canvas = new XeriWindowCanvas();
var handle = canvas.AddWindow
(
    "sample.window",
    "Sample",
    contentView,
    new Vector2(80f, 80f),
    new Vector2(480f, 320f)
);

canvas.RemoveWindow(handle);
```

저장 가능한 View와 Session을 함께 쓰려면 [Xeri Window와 View Source 연결하기](../guides/create-window-view.md)를 참고합니다.

## 책임 범위

### 담당하는 것

- 윈도우 이동과 크기 변경
- `Normal`, `Minimized`, `Maximized` 상태 전환
- 상태 전환 중 pending 상태와 전환 실행 경계
- 포커스 획득과 해제
- Registry 등록과 안정적인 문자열 ID
- 같은 stack layer 안의 앞/뒤 순서와 활성 윈도우
- 현재 위치·크기·상태를 `XeriWindowRecord`에 동기화

### 담당하지 않는 것

- 프로젝트 도메인 데이터의 수명
- 화면 자체의 업무 로직
- 저장 record를 실제 디스크에 저장하는 정책
- 임의의 애플리케이션 탭/문서 모델

## 핵심 구조

```text
Window View / Driver
        ↕
XeriWindowController
        ↕
XeriWindowRegistry
        ↓
XeriWindowHandle + XeriWindowRecord
```
## Controller와 상태 전환

`XeriWindowController`는 `IXeriWindowDriver`와 `IXeriWindowStateTransitioner`를 조합합니다.
외부 코드는 `Move()`, `Resize()`, `Minimize()`, `Maximize()`, `ShowNormal()`, `Restore()`, `Close()`와 `Focus()`를 통해 상태를 요청합니다.

`EffectiveState`는 진행 중 전환 목표가 있으면 그 목표를 우선하고, 그렇지 않으면 실제 Driver 상태를 반환합니다.
따라서 전환 애니메이션 중에도 호출자는 "현재 목표 상태"를 일관되게 읽을 수 있습니다.

`XeriWindowOptions`는 최소·최대 크기와 이동, resize, minimize, maximize, close, focus 허용 여부를 정의합니다.
`StackLayer`는 서로 다른 화면 정렬 계층을 구분합니다.

## Registry와 Handle

`XeriWindowRegistry.Register(id, controller)`는 안정적인 ID를 가진 `XeriWindowHandle`을 반환합니다.
같은 ID가 이미 등록되어 있으면 기존 Handle을 반환합니다.

```csharp
var registry = new XeriWindowRegistry();
XeriWindowHandle handle = registry.Register("inventory", controller);

registry.Focus(handle);
registry.BringToFront(handle);
```

Handle의 `IsValid`는 현재 Registry에 같은 Handle이 등록되어 있을 때만 `true`입니다.
Registry는 Controller의 위치·크기·상태 이벤트를 받아 대응하는 `XeriWindowRecord`를 갱신합니다.

## 정렬과 포커스

- `Focus()`는 active handle을 갱신하고 해당 윈도우를 같은 layer의 앞으로 이동시킵니다.
- `BringToFront()`와 `SendToBack()`은 같은 `StackLayer` 안의 표시 순서를 바꿉니다.
- `SetStackLayer()`는 윈도우의 화면 정렬 계층을 바꾸고 새 계층에서 앞으로 이동시킵니다.
- Registry의 `Records`는 stack layer 순서를 반영해 반환됩니다.
## 소유권과 수명

`XeriWindowHandle`은 `IDisposable` 자원이 아니라 Registry 등록을 참조하는 식별 Handle입니다.
윈도우 수명을 끝내려면 화면 닫기와 Registry 등록 해제를 각각 현재 조립 구조에 맞게 수행해야 합니다.

`XeriWindowRecord`는 현재 창 상태를 저장·복원하기 위한 데이터이며, Registry가 실제 영속 저장소를 소유하지는 않습니다.

## 확장 지점

| 목적 | 계약 |
|---|---|
| 다른 표시 backend | `IXeriWindowDriver` |
| 상태 전환 구현 | `IXeriWindowStateTransitioner` |
| 상태 애니메이션 | `IXeriWindowStateAnimator` |
| drag 생성 | `IXeriWindowDragFactory` |
| resize cursor | `IXeriWindowResizeCursorProvider` |
| theme 해석 | `IXeriWindowThemeResolver` |
| Registry 대체 | `IXeriWindowRegistry` |

## 제약과 주의사항

- Registry ID는 저장·복원에 사용되므로 동일 범위에서 안정적으로 유지합니다.
- 전환 중 실제 Driver 상태만 읽어 후속 동작을 결정하지 말고 `EffectiveState` 계약을 사용합니다.
- 프로젝트 문서나 탭의 업무 상태를 Window Registry에 직접 넣지 않습니다.
- Window 상태 record와 실제 영속 저장 책임을 분리합니다.

## 관련 문서

- [Xeri Tray](tray.md)
- [Window View](window-view.md)
- [Xeri UI 문서](../index.md)
- [Xeri 소유권과 수명](https://inonego-unity.github.io/Xeri/docs/concepts/ownership-and-lifetime.html)
- [Xeri 확장 계약](https://inonego-unity.github.io/Xeri/docs/concepts/extension-contracts.html)
