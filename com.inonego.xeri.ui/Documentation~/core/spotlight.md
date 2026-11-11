# Spotlight

Spotlight는 **화면의 특정 UI 영역을 밝게 남기고 주변을 어둡게 표시하는 안내 효과**입니다. 사용자는 강조할 실제 UI 요소를 지정하고, 필요하면 강조 영역 바깥의 Pointer 입력을 막을 수 있습니다.

Xeri UI는 **UI Toolkit**과 **UGUI**를 위한 별도 Spotlight Driver를 제공합니다. Spotlight는 Screen Stack, Modal, Gameplay Input 정책이나 대상 View의 생성·파괴를 소유하지 않습니다.

## 기본 구성과 수명

| 구성 | UI Toolkit | UGUI |
| --- | --- | --- |
| 요청 Owner | `UITKSpotlight` | `UGUISpotlight` |
| 표시 Driver | `UITKSpotlightElement` | `UGUISpotlightDriver` |
| 표시 인자 | `UITKSpotlightParams` | `UGUISpotlightParams` |
| 대상 | `UITKSpotlightTarget` | `UGUISpotlightTarget` |
| 반환 | `Show(...)`의 `Lease` | `Show(...)`의 `Lease` |

Owner는 Driver를 생성하지 않습니다. 프로젝트가 **이미 UI hierarchy에 연결된 Driver**를 준비하고 `Show(driver, parameters)`를 호출합니다. `Show`가 반환한 Lease를 해제하면 그 요청이 끝납니다.

같은 Driver에 여러 요청을 중첩하면 **가장 최근에 표시한 요청이 우선**합니다. 최상위 요청을 반환하면 이전의 살아 있는 요청이 다시 표시됩니다. 모든 요청을 종료하려면 Spotlight Owner의 `Dispose()`를 호출합니다.

## UI Toolkit Spotlight

`UITKSpotlightElement`는 화면을 덮는 VisualElement입니다. 강조할 대상과 **동일한 Panel**에 연결하고, 표시할 UI보다 상위에 배치합니다. 대상 요소는 실제 Panel에 연결되어 있어야 합니다.

```csharp
var spotlight = new UITKSpotlight();

// spotlightElement는 같은 Panel에 이미 추가된 UITKSpotlightElement입니다.
// targetButton은 강조할 VisualElement입니다.
var highlight = spotlight.Show(
    spotlightElement,
    new UITKSpotlightParams(
        new[] { new UITKSpotlightTarget(targetButton) },
        blocksOutsideInput: true));

// 안내 화면이 끝났을 때
highlight.Dispose();
spotlight.Dispose();
```

`blocksOutsideInput: true`이면 Spotlight의 **구멍 바깥 영역만** Pointer를 가로챕니다. `false`이면 어둡게 보이더라도 바깥 입력은 차단하지 않습니다. Spotlight는 Gameplay Input Action Map을 별도로 차단하지 않습니다.

### 여백과 클리핑

`UITKSpotlightTarget`은 Target과 사방 여백을 받습니다. `Vector4` 여백의 순서는 **왼쪽, 오른쪽, 위쪽, 아래쪽**입니다.

```csharp
var highlighted = new UITKSpotlightTarget(
    targetButton,
    new Vector4(8f, 8f, 6f, 6f));
```

ScrollView viewport와 인라인 `overflow: hidden` 클리핑은 현재 hierarchy를 기준으로 처리합니다. 반면 **USS로만 정의된 clip 조상**은 UI Toolkit의 계산된 overflow 상태를 자동으로 해석하지 않으므로 명시합니다.

```csharp
var highlighted = new UITKSpotlightTarget(
    targetButton,
    clips: new[]
    {
        new UITKSpotlightClip(
            clippingAncestor,
            OverflowClipBox.PaddingBox),
    });
```

`clippingAncestor`는 실제 Target의 상위 VisualElement여야 합니다. 기본 `PaddingBox` 대신 `OverflowClipBox.ContentBox`를 지정할 수도 있습니다. 표시 도중 Target이 해당 조상 밖으로 이동하면 잘못된 입력 통과 영역이 남지 않도록 그 구멍은 숨겨집니다.

Target을 숨기거나 다른 Panel로 이동하면 더 이상 유효한 강조 구멍으로 표시하지 않습니다. 유효한 구멍이 하나도 없을 때는 **dim과 입력 차단을 함께 제거**합니다.

## UGUI Spotlight

UGUI에는 **`UGUISpotlightDriver`와 `UGUISpotlightGraphic`이 연결된 표시 Root**가 필요합니다. Driver의 직렬화 참조인 `root`와 `graphic`을 프로젝트의 Canvas hierarchy에서 미리 연결합니다.

```csharp
var spotlight = new UGUISpotlight();

// spotlightDriver는 Root/Graphic 참조가 구성된 Driver입니다.
// targetRect는 강조할 RectTransform입니다.
var highlight = spotlight.Show(
    spotlightDriver,
    new UGUISpotlightParams(
        new[] { new UGUISpotlightTarget(targetRect) },
        blocksOutsideInput: true));

highlight.Dispose();
spotlight.Dispose();
```

`UGUISpotlightTarget`도 `Vector4` 여백을 받으며 순서는 왼쪽, 오른쪽, 위쪽, 아래쪽입니다. 강조 범위는 Canvas 계층의 활성화 상태, `CanvasGroup.alpha`, `RectMask2D`, `MaskableGraphic.maskable` 및 `overrideSorting` 경계를 반영해 계산합니다.

표시 가능한 Target이 하나도 없으면 UGUI Driver 역시 dim과 바깥 입력 차단을 비웁니다. 대상이 움직이거나 Layout이 변하면 표시 중인 구멍 위치를 다시 계산합니다.

## 여러 대상과 종료

`UITKSpotlightParams`와 `UGUISpotlightParams`는 **하나 이상의 대상 목록**을 받습니다. 생성 시 목록을 복사하므로 생성 뒤 원래 List에 대상을 추가·삭제해도 해당 요청의 목록은 바뀌지 않습니다. 새 대상 조합은 새로운 Params로 요청하세요.

Spotlight Owner와 요청 Lease는 프로젝트 또는 Screen/Window/Lab의 상위 수명에 연결합니다. 일반적으로 화면을 닫을 때 먼저 요청 Lease를 반환하고, Owner와 Driver가 연결된 hierarchy도 적절히 종료합니다. SpotLight는 대상 UI 자체를 파괴하지 않습니다.

## 확인할 사항

- **구멍이 보이지 않음:** UITK라면 Spotlight Element와 대상이 같은 Panel인지, UGUI라면 Driver의 Root/Graphic이 연결됐는지 확인합니다.
- **일부 구멍이 보이지 않음:** Target의 활성/표시 상태, 조상 클리핑 및 USS로 지정한 `UITKSpotlightClip`을 확인합니다.
- **입력이 막히지 않음:** `blocksOutsideInput` 값, Driver의 실제 표시 상태, 적어도 하나의 유효한 구멍이 있는지 확인합니다.
- **기존 안내가 다시 나타남:** 동일 Driver에 남아 있는 이전 `Show` 요청 Lease가 있는지 확인합니다.

직접 실험하려면 [Validation Desktop](../validation.md)의 **Core Lab**에서 Spotlight를 실행할 수 있습니다.
