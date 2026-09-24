# Bar

Bar는 값 범위와 값 변화 구간을 UGUI 또는 UI Toolkit으로 표시하는 presentation primitive입니다.

Screen/Modal lifecycle과 독립적이며 HUD, Window content나 authored UI에 직접 사용할 수 있습니다.

## 공통 값 모델

`UGUIBar`와 `UITKBar`는 같은 값 의미를 사용합니다.

| API | 의미 |
|---|---|
| `LowValue` | 표시 범위 하한 |
| `HighValue` | 표시 범위 상한 |
| `Value` | 목표값 |
| `Ratio` | 목표값의 정규화 비율 |
| `Direction` | 값 증가 방향 |
| `ChangeCurve` | 변화 표시 transition curve |

`BarDirection`:

- `LeftToRight`
- `RightToLeft`
- `BottomToTop`
- `TopToBottom`

## 기본 사용

```csharp
bar.SetRange(0f, maxHP, instant: true);
bar.Direction = BarDirection.LeftToRight;
bar.SetValue(currentHP);
```

즉시 반영:

```csharp
bar.SetValue(currentHP, instant: true);
```

## UGUIBar

`UGUIBar`는 다음 Image 계층을 사용합니다.

- Background
- Change
- Foreground

Change 영역은 증가/감소 중인 차이를 표현합니다.

색상:

- `ForegroundColor`
- `IncreaseColor`
- `DecreaseColor`

## UITKBar

`UITKBar`는 `[UxmlElement]`입니다.

UXML attribute:

- `low-value`
- `high-value`
- `value`
- `direction`

`UITKBar`는 Unity `ProgressBar` 내부 Visual Tree에 의존하지 않고 Background/Change/Foreground element를 직접 소유합니다.

## Lifecycle

Bar는 gameplay state 원본을 소유하지 않습니다.

전이가 보이지 않는 상태에서는 불필요한 transition lifetime을 만들지 않습니다.

- inactive `UGUIBar`: 즉시 최신 값 적용
- Panel 밖 `UITKBar`: 즉시 최신 값 적용

내부 `BarState`와 `BarTransition`은 implementation detail입니다.
