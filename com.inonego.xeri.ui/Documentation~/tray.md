# Tray

Tray는 **항목 목록을 표시하고 선택·닫기 요청을 전달하는 UI 모듈**입니다. Window Taskbar, Tab 목록, 작업 목록처럼 동일한 입력 모델을 사용할 수 있습니다. 모델 변경이나 실제 Window 종료는 Tray가 아니라 **Source의 상위 owner**가 담당합니다.

## 구성 요소와 책임

| 구성 요소 | 역할 |
| --- | --- |
| `IXeriTraySource` | 현재 `XeriTrayEntry` 목록을 제공하고 변경 시 Reload 요청 |
| `IXeriTrayRenderer` | 항목을 표시하고 선택·닫기 입력 발생 |
| `XeriTrayController` | Source와 Renderer의 이벤트 연결, Reload 및 취소 가능한 Close 전달 |
| `XeriTrayPanel` | UI Toolkit 기본 Renderer, 선택적 Drag Reorder |
| `XeriTrayOptions` | 표시 내용·스타일·Reorder 동작 설정 |

```text
Source.GetEntries() → Controller.Reload() → Renderer.Reload()
                          ↑                    ↓
Source.OnReloadRequired   ←        Select / Close input
```

## 기본 사용

기존 UITK hierarchy의 적절한 Container에 `XeriTrayPanel`을 연결합니다.

```csharp
XeriWindowTraySource source = workspace.CreateTraySource();
var panel = new XeriTrayPanel();
var controller = new XeriTrayController(
    source,
    panel,
    XeriTrayOptions.Default());

controller.OnEntrySelect += (_, e) => source.Activate(e.Entry);
controller.OnEntryClose += (_, e) => source.Close(e.Entry);

controller.Reload();
```

`workspace`는 생성된 `XeriWindowWorkspace`이고, `panel`은 프로젝트가 자신의 UITK Root에 추가한 뒤 수명 종료 시 제거합니다. Window Source는 Registry가 변경되면 Controller에 Reload를 요청합니다.

이 예시는 **Window를 위한 연결**입니다. 일반 Tray Source를 사용한다면 프로젝트가 자신의 도메인 모델에서 항목을 선택하거나 닫고, 변경을 알리도록 구현합니다.

## 항목 상태

`XeriTrayEntry`는 이름·아이콘·선택·표시 여부 등의 현재 상태를 제공합니다.

- `IsActive`: 현재 활성/포커스 항목. 기본 Panel의 `--active` 스타일에 반영됩니다.
- `IsVisible`: 연결된 콘텐츠가 현재 표시되는지 여부. `--visible` 스타일에 반영됩니다.
- `CanClose`: 닫기 입력을 허용할지 결정합니다.
- `StateMarker`: 선택적으로 별도의 상태 표시 영역을 제공합니다.

Window Source에서 `Normal`과 `Maximized`는 보이는 Window이고 `Minimized`는 숨겨진 Window입니다. `IsActive`와 `IsVisible`는 서로 다른 상태이므로 둘을 동일하게 다루지 않습니다.

## Window 상태별 Tray 생성

`XeriWindowWorkspace.CreateTraySource`는 기본적으로 `Normal`·`Minimized`·`Maximized` 상태의 살아 있는 Window를 공급합니다.

```csharp
var allWindows = workspace.CreateTraySource();

var minimizedOnly = workspace.CreateTraySource(
    XeriWindowTrayStateMask.Minimized);

var visibleOnly = workspace.CreateTraySource(
    XeriWindowTrayStateMask.Normal |
    XeriWindowTrayStateMask.Maximized);
```

Source의 `Activate(entry)`는 최소화된 Window를 **최소화 직전 표시 상태**로 복원하고, 이미 표시된 Window는 상태 변경 없이 활성화합니다. `Close(entry)`는 연결된 Window 종료 명령에 위임합니다.

여러 Source를 만든 경우 각각 독립된 수명이 있으며, 별도로 `Dispose()`해야 합니다.

## 닫기 요청 계약

Renderer의 Close 입력이 곧바로 모델 삭제를 뜻하지는 않습니다.

1. `CanClose = false`인 항목은 Close 요청을 전달하지 않습니다.
2. `XeriTrayController.OnPreEntryClose`에서 상위 정책이 요청을 검사합니다.
3. `XeriTrayCancelEventArgs.Cancel = true`이면 종료를 취소합니다.
4. 취소되지 않으면 `OnEntryClose`에서 실제 owner가 종료·제거 작업을 수행합니다.
5. 변경된 Source가 Reload를 요청하면 Panel이 최신 목록을 표시합니다.

예:

```csharp
controller.OnPreEntryClose += (_, e) =>
{
    if (ShouldKeepEntryOpen(e.Entry))
    {
        e.Cancel = true;
    }
};
```

이 예제의 `ShouldKeepEntryOpen`은 프로젝트가 구현할 정책 함수입니다. 하나의 이벤트 Observer가 예외를 던지면 Controller는 관련 오류를 보고하고 Close 승인 처리를 중단합니다.

## 표시 옵션과 Reorder

`XeriTrayOptions`가 제공하는 값:

| 옵션 | 의미 |
| --- | --- |
| `VisibleContent` | 아이콘·제목·닫기 버튼의 표시 범위 |
| `UssClass` | Tray root에 추가할 프로젝트 USS class |
| `Reorderable` | 사용자가 Drag Reorder를 할 수 있는지 |
| `ReorderAxis` | `Horizontal` 또는 `Vertical` |
| `AnimateReorder` | Drag preview offset 애니메이션 |

Reorder는 **일반 `IXeriTrayRenderer` / `XeriTrayController` 이벤트가 아닙니다**. 구체 UITK `XeriTrayPanel.OnEntryReorder`를 구독해야 합니다.

```text
XeriTrayPanel drag
→ XeriTrayReorderManipulator preview
→ OnEntryReorder(request)
→ 프로젝트가 도메인 목록의 실제 순서를 변경
→ Source Reload
```

`AnimateReorder`가 꺼져도 Drag/Commit/Cancel의 소유권과 preview offset 정리는 유지됩니다. 사용자 정의 `IXeriTrayReorderAnimator`를 지정하면 별도 미리보기 표현을 만들 수 있지만 도메인 목록을 직접 변경해서는 안 됩니다.

## 수명과 예외 경계

`XeriTrayController.Dispose()`는 Source/Renderer의 이벤트 구독을 해제합니다. **Source 자체의 Dispose는 대신하지 않습니다.** Window 소유자에서는 일반적으로 Controller를 먼저 반환하고, 이어서 Source를 반환하며 Panel을 계층에서 제거합니다.

이벤트의 `add`·`remove` 접근자는 일반 C# 이벤트처럼 **정상 완료**되는 것을 전제로 합니다. 외부 구현이 일부만 구독한 뒤 예외를 던지는 경우까지 자동 롤백하지 않습니다. 이벤트 중 Controller가 종료되면 남은 Reload·닫기 승인은 중단합니다.

Tray는 프로젝트의 Window/Tab/작업 모델 소유권, 실제 데이터 영속화, Window 상태 전환 규칙을 대신 소유하지 않습니다.
