# UI Core Validation

Xeri UI Core의 공개 경로를 한 Scene에서 검증하는 선택형 Package Sample이다. Screen Stack, Modal, Presentation Lease, Spotlight, Scene Fade, Focus, Input, UITK Layer와 Gradient 표현을 실제 Runtime 수명으로 확인한다.

샘플은 Xeri의 공개 API와 Package 내부 샘플 자원만 사용한다. 프로젝트 전용 Settings, Bootstrapper, Render Pipeline 우회 Component에는 의존하지 않는다.

## 가져오기

1. Unity에서 `Window > Package Manager`를 연다.
2. `Xeri` Package를 선택한다.
3. `Samples`의 `UI Core Validation`에서 `Import`를 누른다.
4. 가져온 `GameUIValidation.unity`를 연다.

Unity는 샘플을 다음 형식의 프로젝트 경로로 복사한다.

```text
Assets/Samples/Xeri/<version>/UI Core Validation/
```

## 실행

1. `GameUIValidation.unity`를 연다.
2. Play Mode로 진입한다.
3. Mouse 또는 Keyboard/Gamepad Navigation과 Submit으로 버튼을 조작한다.

활성 `UIRuntime`이 없으면 샘플은 `GameUIValidationSettings.asset`과 Xeri의 `UIHost.prefab`으로 독립 Runtime을 만들고 전체 기능을 검증한다. 이미 App Runtime이 있으면 App Profile을 바꾸지 않고 샘플 전용 Layer Registry와 Child `UIContext`만 생성한다.

## 검증 항목

| 명령 | 검증 내용 |
|---|---|
| `PUSH DETAIL` | `IScreenSource`가 만든 Screen Session을 Stack에 추가 |
| `PUSH ANOTHER` | 같은 Screen ID의 별도 Session 수명 |
| `REPLACE TOP` | 현재 top을 새 Session으로 교체 |
| `POP SCREEN` | Close Transition과 이전 Focus 복원 |
| `OPEN MODAL` | `ModalSession`의 Presentation·interaction·owned lifetime 수명 |
| `SPOTLIGHT` | UITK Spotlight Lease를 현재 Screen의 자식 수명으로 소유 |
| `Overlay Toast` | `PresentationLease`가 View와 Layer Usage를 함께 소유 |
| `Cover → Reveal` | 기본 `SceneFader`의 Cover/Reveal 실행 |
| `Clear & Restore` | Stack 전체 정리 후 Dashboard 재생성 |

Dashboard에는 Stack 수, Modal 수, Fade 상태, 마지막 Input Device와 주요 UI Core 상태가 표시된다. Gradient는 Unity 6000.7 native Linear/Radial 표현만 검증하며 Xeri 전용 gradient fallback은 사용하지 않는다.

## 샘플 구조

```text
GameUIValidation.unity
GameUIValidationSettings.asset
GameUIValidationGameplay.inputactions
Runtime/   # 샘플 조립 코드와 전용 Assembly
UI/        # UXML, USS, Layer, PanelSettings, Profile
Fonts/     # Unity/HTML 공용 Inter, OFL 1.1
Web~/      # 1920x1080 HTML/CSS 시각 기준본
```

`GameUIValidationGameplay.inputactions`는 샘플을 독립 실행하기 위한 최소 UI/Gameplay Action Map이다. 실제 애플리케이션에서는 프로젝트 입력 계약을 사용한다.

## HTML/CSS 기준본

`Web~/index.html`을 브라우저로 열면 Unity 화면의 1920x1080 기준본을 확인할 수 있다. `Web~/style.css`와 `UI/GameUIValidationScreen.uss`는 동일한 정보 구조와 주요 시각 값을 유지한다.

브라우저 전용 표현은 검증 참고용이며 Unity Runtime 계약이 아니다. Text Engine rasterization처럼 엔진별 차이는 완전한 픽셀 일치를 요구하지 않는다.

Inter Font는 SIL Open Font License 1.1을 따른다. 라이선스는 `Fonts/Inter-LICENSE.txt`에 포함된다.

## 제거

Package Manager의 Sample Import는 Xeri Runtime을 변경하지 않는다. 검증 화면이 필요 없으면 `Assets/Samples/Xeri/<version>/UI Core Validation` 폴더만 제거하면 된다.
