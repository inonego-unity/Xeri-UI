# Tray

Tray는 Window, Tab, 작업 항목 같은 entry 목록을 표시하고 선택/닫기 요청을 전달합니다.

## 구조

```text
IXeriTraySource
      ↓ entries
XeriTrayController
      ↓
IXeriTrayRenderer
      ↕
선택 / 닫기
```

`IXeriTraySource`가 모델 목록을 공급하고 `IXeriTrayRenderer`가 표시를 담당합니다.

`XeriTrayController`는 둘 사이의 event/lifecycle을 연결합니다.

## 기본 사용

```csharp
var controller = new XeriTrayController(
    source,
    renderer,
    XeriTrayOptions.Default());

controller.OnEntrySelect += HandleSelect;
controller.OnPreEntryClose += HandleCloseRequest;
controller.OnEntryClose += HandleClose;

controller.Reload();
```

닫기 입력은 모델 삭제가 아니라 요청입니다.

`OnPreEntryClose`에서 취소할 수 있고, 승인된 요청만 `OnEntryClose`로 전달됩니다.

실제 Window/Tab lifecycle은 상위 owner가 변경한 뒤 Source가 `OnReloadRequired`를 발생시키는 구조가 기본입니다.

## Window 연결

`XeriWindowWorkspace`의 Registry를 Tray Source로 만들 수 있습니다.

```csharp
XeriWindowTraySource source =
    workspace.CreateTraySource();
```

기본 Source는 `Normal`, `Minimized`, `Maximized` 상태의 live Window를 모두 entry로 공급합니다.
Tray마다 필요한 상태만 표시하려면 `XeriWindowTrayStateMask`를 지정합니다.

```csharp
XeriWindowTraySource minimizedSource =
    workspace.CreateTraySource(
        XeriWindowTrayStateMask.Minimized);

XeriWindowTraySource visibleSource =
    workspace.CreateTraySource(
        XeriWindowTrayStateMask.Normal |
        XeriWindowTrayStateMask.Maximized);
```

Window Tray entry 선택은 `source.Activate(entry)`로 연결할 수 있습니다.
Minimized Window는 최소화 이전 상태로 복구되고, Normal/Maximized Window는 현재 상태를 유지한 채 활성화됩니다.

Source와 `XeriTrayController` lifetime은 호출자가 소유합니다.

## 표시 옵션

`XeriTrayEntry.IsActive`는 현재 포커스, `IsVisible`은 연결 콘텐츠의 표시 상태입니다. Window Source는 Normal·Maximized를 표시 상태로, Minimized를 숨김 상태로 전달합니다. UITK 버튼의 `--active`·`--visible` 클래스는 각각 이 값을 반영합니다. `StateMarker`는 선택적인 포커스 표시이며, 표시 상태의 배경 tint와 독립적입니다.

`XeriTrayOptions`:

- `VisibleContent`
- `UssClass`
- `Reorderable`
- `ReorderAxis`
- `AnimateReorder`

## Reorder

Reorder는 공통 `IXeriTrayRenderer`/`XeriTrayController` event가 아닙니다.

UITK concrete `XeriTrayPanel`이 optional `OnEntryReorder` capability를 제공합니다.

```text
XeriTrayPanel
→ XeriTrayReorderManipulator
→ OnEntryReorder
→ 상위 owner가 실제 모델 순서 변경
→ Source reload
```

`XeriTrayReorderSession`, calculator와 animator는 preview/input state를 처리하지만 실제 domain list를 직접 변경하지 않습니다.

## 책임

Tray가 소유하는 것:

- entry 표시
- 선택 입력
- 취소 가능한 닫기 요청
- Source reload 반영
- UITK optional reorder input/preview

Tray가 소유하지 않는 것:

- Window/Tab domain lifetime
- Window minimize/maximize
- 실제 persistence storage

## 종료

`XeriTrayController.Dispose()`는 Source/Renderer event subscription을 해제합니다.

Source 자체가 `IDisposable`이면 Source owner가 별도로 반환합니다.
