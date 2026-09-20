# Xeri Tray

Xeri Tray는 Window, 탭, 작업 항목처럼 여러 entry를 한 줄 또는 한 영역에 표시하고 선택·닫기·재정렬 입력을 공통 계약으로 처리하는 UI 시스템입니다.
Core는 데이터 공급과 표시를 분리하고, UI Toolkit 구현은 Renderer와 Reorder 동작을 제공합니다.

## 왜 필요한가

Tray Renderer가 실제 Window/Document 목록을 직접 수정하기 시작하면 표시와 도메인 수명이 결합됩니다. Xeri Tray는 Source가 entry를 공급하고 Renderer는 입력만 발생시키도록 분리해서 같은 모델을 다른 표시 backend에서도 재사용할 수 있게 합니다.

## 언제 사용하는가

- 여러 Window/Tab/작업 항목을 공통 목록으로 표시할 때
- 선택과 닫기 요청을 모델 변경과 분리하고 싶을 때
- drag reorder preview와 실제 데이터 순서 변경을 별도 책임으로 두고 싶을 때

단순 버튼 몇 개를 고정 배치하는 UI라면 Tray 구조가 필요하지 않습니다.

## 기본 사용

```csharp
var controller = new XeriTrayController(source, renderer, XeriTrayOptions.Default());

controller.OnEntrySelect += (_, e) => SelectEntry(e.Entry);
controller.OnPreEntryClose += (_, e) =>
{
    // 필요하면 취소하거나, 상위 소유자에게 실제 Close를 요청한다.
};

controller.Reload();
```

닫기 입력은 모델 삭제가 아니라 **요청**입니다. 실제 Window/Document 수명을 끝낸 뒤 Source가 `OnReloadRequired`를 발생시키는 구성이 기본입니다.

## 책임 범위

### 담당하는 것

- 현재 Tray entry 목록 표시
- entry 선택 입력 전달
- 취소 가능한 entry 닫기 요청
- Source 변경 시 reload
- 선택적인 drag reorder
- 표시 콘텐츠, USS class, reorder 정책 설정

### 담당하지 않는 것

- entry가 의미하는 도메인 객체의 실제 수명
- Window 자체의 최소화·최대화 상태
- 데이터 저장 또는 탭 복구 정책

## 핵심 구조

```text
IXeriTraySource
      ↓ entries
XeriTrayController
      ↓
IXeriTrayRenderer
      ↕ 사용자 입력
선택 / 닫기 / reorder
```

`IXeriTraySource.GetEntries()`가 현재 모델 목록을 공급하고, `IXeriTrayRenderer.Reload()`가 실제 표시를 갱신합니다.
## Controller 흐름

`XeriTrayController`는 Source와 Renderer 이벤트를 연결합니다.
Source가 `OnReloadRequired`를 발행하면 Controller가 다시 `GetEntries()`를 호출하고 Renderer에 전달합니다.
Renderer의 선택 입력은 `OnEntrySelect`로 전달되고, 닫기 입력은 `OnPreEntryClose`의 취소 가능한 요청으로 변환됩니다.

```csharp
var controller = new XeriTrayController(source, renderer, options);
controller.OnEntrySelect += HandleSelect;
controller.OnPreEntryClose += HandleCloseRequest;
controller.Reload();
```

Controller는 entry를 직접 삭제하지 않습니다. 닫기 요청을 받은 소유자가 실제 모델 변경을 수행하고 Source가 reload를 요청하는 구조가 자연스럽습니다.

## 표시 옵션

`XeriTrayOptions`는 다음 표시·동작 정책을 정의합니다.

| 옵션 | 의미 |
|---|---|
| `VisibleContent` | entry에서 표시할 정보 범위 |
| `UssClass` | Tray root에 적용할 USS class |
| `Reorderable` | drag reorder 허용 여부 |
| `ReorderAxis` | reorder 기준 축 |
| `ReorderMode` | drag 위치 해석 방식 |
| `AnimateReorder` | preview 애니메이션 사용 여부 |
| `ReorderAnimationDuration` | reorder 전환 시간 |

## Reorder

Reorder는 입력 세션과 실제 모델 변경을 분리합니다.
`XeriTrayReorderSession`은 drag 중인 Button, Entry, 시작 index와 pointer 위치를 보관하고, calculator와 animator가 이동 후보와 preview를 처리합니다.

실제 데이터 순서를 바꾸는 책임은 Source 또는 상위 소유자에게 남겨야 합니다.
## 확장 지점

| 목적 | 계약 |
|---|---|
| entry 공급 | `IXeriTraySource` |
| 표시 backend | `IXeriTrayRenderer` |
| reorder preview | `IXeriTrayReorderAnimator` |
| reorder 대상 | `IXeriTrayReorderTarget` |

## 제약과 주의사항

- Controller의 닫기 이벤트는 실제 삭제 명령이 아니라 요청입니다.
- Renderer가 모델 목록을 직접 소유하거나 변경하지 않도록 합니다.
- Reorder preview와 실제 Source 순서 변경을 같은 책임으로 합치지 않습니다.
- Window 목록을 Tray에 표시할 때도 Window lifecycle은 Window 시스템이 소유합니다.
- 현재 `XeriTrayController`에는 별도 `Dispose`/`Unbind` API가 없으므로 Source·Renderer와 Controller를 같은 수명 범위에서 조립하는 것이 안전합니다.

## 관련 문서

- [Xeri Window](window.md)
- [Xeri UI 문서](../index.md)
- [Xeri 확장 계약](https://inonego-unity.github.io/Xeri/docs/concepts/extension-contracts.html)
