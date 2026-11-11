# Bar

Bar는 **수치 범위와 증감 상태를 UI Toolkit 또는 UGUI로 표시하는 시각 컴포넌트**입니다. 체력·경험치·진행도·자원량에 사용하며 Screen/Window 수명 또는 게임 데이터 자체를 소유하지 않습니다.

## 사용 대상

| 구현 | 작성 방식 | 기본 표시 요소 |
| --- | --- | --- |
| `UGUIBar` | GameObject의 MonoBehaviour | `BackgroundImage` / `ChangeImage` / `ForegroundImage` |
| `UITKBar` | `[UxmlElement]` 또는 `new UITKBar()` | `Background` / `Change` / `Foreground` |

두 구현의 값 계산과 방향은 동일합니다. UGUI는 RectTransform의 Anchor를, UITK는 VisualElement inset을 사용합니다.

## 값 모델

| API | 동작 |
| --- | --- |
| `LowValue` | 범위 시작값 |
| `HighValue` | 범위 끝값 |
| `Value` | 현재 목표값 |
| `Ratio` | `(Value - LowValue) / (HighValue - LowValue)`를 0~1로 제한한 값 |
| `Direction` | 화면에서 값이 증가하는 방향 |
| `ChangeCurve` | 애니메이션의 시간·지연·곡선 |
| `SetRange(low, high, instant)` | 범위를 갱신하고 표시 |
| `SetValue(value, instant)` | 목표값을 갱신하고 표시 |
| `Refresh(instant)` | 현재 데이터로 표시를 다시 계산 |

`HighValue == LowValue`처럼 범위가 0이거나 결과가 유효하지 않으면 비율은 0입니다. 범위를 초과하는 값은 0~1로 제한해 표시하며, 원본 데이터는 프로젝트가 소유합니다.

## 기본 사용

UGUI/UITK 모두 공통 메서드를 사용합니다.

```csharp
bar.SetRange(0f, maxHealth, instant: true);
bar.Direction = BarDirection.LeftToRight;
bar.SetValue(currentHealth);
```

즉시 표시하려면:

```csharp
bar.SetValue(currentHealth, instant: true);
```

`BarDirection`의 방향은 `LeftToRight`, `RightToLeft`, `BottomToTop`, `TopToBottom`입니다.

## 증감 구간

Bar의 표시 영역은 Background, Foreground, Change로 나뉩니다.

- **증가 중**: Foreground는 현재 표시 비율까지, Change는 현재 비율에서 목표 비율까지의 차이를 표시합니다.
- **감소 중**: Foreground는 새 목표값을 먼저 보여주고, Change는 이전 표시 비율까지 남아 있는 감소 구간을 나타냅니다.
- **변화 없음**: Foreground와 목표값이 같아지면 Change 영역은 비워집니다.

UGUI는 `ForegroundColor`·`IncreaseColor`·`DecreaseColor`를 사용하고, UITK는 `xeri-bar--increase`·`xeri-bar--decrease` USS 클래스로 변화를 표현합니다.

## 에디터와 애니메이션 동작

- `SetValue` 또는 `SetRange`의 `instant: true`는 목표값을 즉시 확정합니다.
- `DOTWEEN` 심볼을 사용하며 Play Mode에서 유효한 `ChangeCurve.Duration`이 있으면 DOTween으로 표시 비율을 전환합니다.
- 심볼이 없거나 시간이 0이면 즉시 반영합니다. **Presentation Transition 자체는 DOTween Runtime 어셈블리를 필요로 합니다.**
- 비활성 `UGUIBar`는 최신 목표를 즉시 표시하고, 비활성화 시 이전 전이를 종료합니다.
- Panel에 연결되지 않은 `UITKBar`는 Tween을 시작하지 않고, Panel에 다시 연결되면 최신 목표를 반영합니다.
- 목표값이 갱신되면 이전 Bar 전이를 중단하고 새 목표값을 사용합니다.

## 프로젝트 책임

Bar는 수치를 자동으로 계산하거나 Gameplay State에 구독하지 않습니다. 게임 로직이 값 변경 시 `SetValue`를 호출하고, 적절한 GameObject/VisualElement의 lifetime과 USS 스타일을 소유해야 합니다.

[설치와 의존성](getting-started/installation.md)에서 DOTween 설정을 확인할 수 있습니다.
