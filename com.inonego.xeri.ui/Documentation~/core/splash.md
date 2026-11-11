# Splash

Splash는 임의의 UI 콘텐츠를 잠시 표시한 뒤 반환하는 Presentation 기능입니다.
게임 시작, 브랜드 로고, 안내 콘텐츠 등 도메인과 무관하게 사용할 수 있습니다.

UI Toolkit과 UGUI 모두 동일한 `SplashSession`과 `SplashParams`를 사용합니다.
Splash는 Screen Stack, Scene Fade, 입력 키 설정, 콘텐츠 디자인을 소유하지 않습니다.

## 표시할 Layer 준비

Splash는 전달된 `PresentationTarget`의 Layer를 획득합니다. `PresentationTarget.Host("Splash")` 예제를 사용하려면 먼저 `UISettingsAsset.DestinationDefinitions`에 `Splash` Destination과 실제 Layer를 매핑해야 합니다. UI Toolkit 콘텐츠는 UITK Layer에, UGUI 콘텐츠는 UGUI Layer에 표시합니다.

필요하면 `PresentationTarget.Local(layerID)`로 현재 Session의 Layer에 표시할 수 있습니다. 동일한 Target에 서로 다른 Root를 가진 Splash를 함께 표시하는 것은 허용되지만, **하나의 Root를 여러 Session이 동시에 소유해서는 안 됩니다**.

## UI Toolkit

```csharp
VisualElement root = CreateSplashRoot(); // 부모가 없는 독립 Root
VisualElement logo = root.Q<VisualElement>("Logo");

SplashSession session = UITKSplash.Show(
    context,
    PresentationTarget.Host("Splash"),
    root,
    SplashParams.Default(),
    fadeTarget: logo,
    onEnded: result => Debug.Log(result));
```

`VisualTreeAsset.CloneTree()`로 생성한 Root와 C#에서 만든 `VisualElement`가 동일하게 동작합니다.
USS는 해당 View가 소유합니다. `fadeTarget` 생략 시 전체 Root를 페이드합니다.
Fade In은 `SplashParams.VisibleAlpha`까지 진행합니다(기본값 1). USS 스타일 계산 시점에 의존하지 않으며, Layer 부착 전에 원래 inline style을 보관해 부착 콜백의 변경과 무관하게 완료·취소·실패 후 호출 전 상태를 복원합니다.
배경과 버튼까지 함께 사라져야 하면 콘텐츠 일부 대신 Root 전체를 페이드 대상으로 지정하세요.

## UGUI

```csharp
RectTransform root = CreateSplashPanel(); // 부모가 없는 Root
RectTransform logo = root.Find("Logo") as RectTransform;

// logo GameObject에는 CanvasGroup이 있어야 합니다.
SplashSession session = UGUISplash.Show(
    context,
    PresentationTarget.Host("Splash"),
    root,
    SplashParams.Default(),
    fadeTarget: logo,
    onEnded: result => Debug.Log(result));
```

UGUI의 Fade 대상은 `CanvasGroup`을 직접 포함해야 합니다.
`fadeTarget` 생략 시 Root의 `CanvasGroup`을 사용합니다.
Fade In은 `SplashParams.VisibleAlpha`까지 진행합니다(기본값 1). Layer 부착 전에 CanvasGroup alpha와 Root 활성 상태를 보관하고, 부착 콜백이 Root를 비활성화해도 재생 시 활성화합니다. 종료 시 호출 전 상태를 복원합니다.
예제의 `Splash` Host Destination은 사용하는 UISettingsAsset에서 UGUI Layer로 매핑해야 합니다.
Validation Scene의 동일 이름 Destination은 UITK Layer용이므로 UGUI 예제는 별도 UGUI 구성에서 사용합니다.

## 실행 및 소유권

- `SplashParams`는 Fade In, Hold, Fade Out 시간과 표시 중 목표 Alpha(`VisibleAlpha`, 0~1)를 지정합니다. 시간은 모두 Unscaled Time 기준입니다.
- `SplashSession.Phase`는 `Entering → Holding → Exiting → Finished`로 진행합니다.
- `Skip()`은 즉시 Fade Out으로 이동합니다. `Cancel()`은 전환 없이 반환합니다.
- `Dispose()`는 호출자 수명 종료용이며 완료 콜백 없이 표시를 반환합니다.
- `Show()`는 `UIContext` 소유 Runtime으로 전환을 실행합니다. 생성 중 실패는 예외를 던지고 롤백하며, 시작 후 실패·예기치 않은 Transition 취소는 `SplashResult.Failed`로 종결합니다.
- 모든 시간이 0이면 `Show()`가 반환되기 전에 완료 콜백이 실행될 수 있습니다.
- 종료 시 Element/RectTransform은 Layer에서 분리하고 Fade 대상의 opacity/alpha를 복원합니다. UGUI에서는 비활성 Root도 Fade 초기 Alpha를 먼저 적용한 뒤 활성화하고, 종료 시 원래 활성 상태로 되돌립니다.
- 전달한 `VisualElement` 및 `GameObject` 자체의 생성·파괴는 호출자 책임입니다.
- Context/Window/Lab 등 상위 owner가 세션의 `Dispose()`를 보장해야 합니다.
- Layer에는 `PresentationTarget.Local(...)` 또는 `Host(...)`로 접근합니다. 표시 순서는 Presentation Layout의 `LayerOrder`를 따릅니다.
- 입력 차단은 자동으로 적용되지 않습니다. Screen/Modal 등 기존 입력 정책과 조합합니다.
- 같은 Target에 서로 다른 Root의 여러 세션을 동시에 표시하는 것은 허용됩니다. 하나의 UGUI Root는 반환 전까지 한 세션만 소유하며, 실행 중 외부에서 부모를 바꿔도 중복 Show를 거부합니다.

## Validation

`Validation/Scenes/XeriUIValidation.unity`의 Core Lab → 화면 표시 → Splash 재생으로
UITK 콘텐츠를 반복 검증할 수 있습니다. UGUI Splash는 라이브러리 API로 별도 제공됩니다.
콘텐츠의 Skip/Cancel 버튼과 Core Lab의 결과 상태를 확인합니다.
