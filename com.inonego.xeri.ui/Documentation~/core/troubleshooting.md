# 문제 해결

## Runtime이 초기화되지 않음

다음을 확인합니다.

1. `UISettingsAsset.DefaultLayout`
2. `UITKPanelSettingsTemplate`
3. `PresentationDestinationID`에 `System` destination이 있고 실제 Root Layer를 가리키는지
4. `BackendSupport`
5. UI/Gameplay Action Asset과 Map 이름
6. Runtime Host의 Presentation root / Focus Driver / Input Driver / Fade Source
7. explicit `PresentationLayerHost[]`의 ID/backend

`BackendSupport`에 UGUI가 포함되면 `EventSystem + InputSystemUIInputModule`도 확인합니다.

## Layout resolve 실패

| 증상 | 확인 |
|---|---|
| duplicate LayerID | Layer ID uniqueness |
| duplicate LayerOrder | 같은 Layout의 order uniqueness |
| duplicate PresentationID | Session scope placement ID uniqueness |
| unknown LayerID | Placement가 실제 Layer를 참조하는지 |
| duplicate LocalOrder | 같은 Layer 안 order uniqueness |
| backend mismatch | UGUI/UITK requirement와 Host/backend 일치 |

## Scene-authored Layer가 열리지 않음

`PresentationLayerHost`를 확인합니다.

- LayerID가 Plan Layer와 일치
- backend 일치
- Runtime Host composition에 명시적으로 연결
- ManagedRoot가 LayerRoot 아래 distinct descendant
- UITK는 같은 `PanelRenderer`를 가리키는 `VisualElementReference` 사용

Scene 전체를 ID로 검색해 Host를 보완하지 않습니다.

## PlacementHost 오류

- `PresentationID`가 Plan에 존재하는지
- 올바른 LayerHost에 속하는지
- root가 ManagedRoot의 direct child인지
- backend가 placement와 일치하는지

PlacementHost는 LayerID/LocalOrder를 override하지 않습니다.

## Screen Open 거부

다음을 확인합니다.

1. registration lifetime이 살아 있는지
2. registration의 `PresentationTarget`이 유효한 local Layer 또는 Host destination을 가리키는지
3. `DuplicatePolicy.Reject`와 live duplicate가 충돌하지 않는지
4. Controller가 navigation callback/transition command 중인지
5. `IScreenSource.Acquire`가 resolved Layer backend와 맞는 Driver를 받는지

## Focus가 예상 위치로 돌아오지 않음

일반 복원 순서:

```text
LastFocus
→ IFocusScope.DefaultFocus
→ backend fallback
```

Window/Modal composition에서는:

- Scope가 올바른 Context에 등록됐는지
- Simple Window Primary Scope가 active Window와 일치하는지
- Application Window Child Context authority가 활성인지
- Modal Focus Override가 `ModalSession` lifetime과 함께 반환되는지

를 확인합니다.

## Gameplay Input / Cursor가 이상함

- `BlocksGameplayInput`
- effective Context path
- input-release barrier
- Covered Screen을 Cursor owner로 기대하고 있지 않은지
- UI/Gameplay Action Map 구성이 분리됐는지

Cursor는 numeric priority나 최근 획득 순서로 결정하지 않습니다.

## Modal이 남음

Common UITK Modal은 `ModalSession`을 반드시 반환해야 합니다.

```csharp
ModalSession modal =
    UITKModal.Open(
        context,
        PresentationTarget.Host(PresentationDestinationID.Modal),
        root);

modal.Dispose();
```

Screen에 종속된 Modal은 `ScreenSession.RegisterChild(modal)`로 lifetime을 이전할 수 있습니다.

## PresentationSession 종료가 거부됨

active Layer consumer lifetime이 남아 있는지 확인합니다.

- `PresentationLayerLease`
- static authored Placement usage
- live Screen/Modal Session
- Drag Visual Layer Lease
- Child PresentationSession

consumer lifetime을 먼저 반환한 뒤 Session owner를 종료합니다.

## UITK Layer가 표시되지 않음

- `PanelRenderer` 존재
- `PanelSettings`와 ThemeStyleSheet 존재
- generated output의 targetTexture/ordering 설정
- Scene Host의 `VisualElementReference` resolution
- LayerRoot/ManagedRoot/PlacementRoot hierarchy

Generated output과 Scene-authored output의 ownership은 다릅니다.

```text
Generated
→ Runtime PanelSettings clone 소유

Scene-authored
→ authored PanelSettings / VisualTreeAsset identity 유지
→ sortingOrder만 scoped 적용/복원
```

## 어디를 봐야 하나

| 관심사 | 코드 영역 |
|---|---|
| Runtime | `Runtime/Core/Runtime` |
| Layout / Session / Host | `Runtime/Core/Presentation` |
| Screen | `Runtime/Core/Policy/Navigation` |
| Modal | `Runtime/Core/Policy/Modality` |
| Focus / Input | `Runtime/Core/Interaction`, `Runtime/Core/Backends/InputSystem` |
| UITK / UGUI backend | `Runtime/Core/Backends` |
| Window | `Runtime/Window` |

구조 자체의 source of truth는 [Presentation Architecture](../architecture/presentation.md)입니다.
