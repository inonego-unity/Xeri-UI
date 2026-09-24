# Validation Desktop

Xeri UI package의 canonical 통합 검증 환경은 다음 Scene이다.

```text
Packages/com.inonego.xeri.ui/Validation/Scenes/XeriUIValidation.unity
```

이 Scene은 기능별 단계 진행형 테스트가 아니라 작은 데스크톱 OS처럼 자유롭게 조작하는 integration validation surface다.

## 검증 범위

- scene-authored Desktop UITK Layer Host
- generated Window / Overlay / Modal UITK Layer
- generated UGUI System Fade
- Root `UIContext` Screen / Modal / Focus / Input
- `XeriWindowWorkspace`
- `XeriWindowTraySource → XeriTrayController → XeriTrayPanel`
- Child Application `PresentationSession + UIContext`
- Window drag / resize / minimize / restore / maximize / close
- Application sibling isolation과 teardown

## 사용 방식

Play 후 Desktop의 앱과 system tool을 원하는 순서로 실행한다.

- Task Board와 Preferences는 일반 application interaction을 제공한다.
- Application A/B는 같은 Child Layout과 Screen ID를 사용하지만 독립된 child UI world를 가진다.
- Core Lab은 root Screen stack, transient Presentation lifetime, Spotlight와 Scene Fade를 행사한다.
- Window Lab은 Window state와 Tray state projection을 행사한다.
- Modal Lab은 root nested Modal stack을 행사한다.

System Monitor는 Screen/Modal/Window/Input 변경 이벤트로 현재 상태를 표시한다. 접힌 동안에는 표시 갱신을 생략하며 주기적인 Tray 전체 조회를 하지 않는다.

Explored 목록은 관찰한 동작을 기록한다. 자동 테스트 통과나 입력 차단·격리의 완전한 검증을 뜻하지 않는다. Independent app contexts는 서로 다른 Context가 생성되었음을 뜻하며, sibling Screen/Modal 격리는 별도 자동 테스트와 실제 조작으로 검증한다.

## Visual contract

Validation의 시각 기준과 runtime 동작 기준은 `Validation/Scenes/XeriUIValidation.unity`와 해당 Unity USS/UXML 자산을 source of truth로 사용한다.

Validation 고유 UI는 일반적인 desktop OS visual language를 사용한다. Production Window/Tray style은 Validation에서 복제하지 않고 실제 production 경로를 그대로 사용한다.


## 수명과 공용 스타일

- `UIContext.RegisterChild`는 자식 Context와 Screen/Modal보다 먼저 콘텐츠를 역순 반환한다.
- `XeriWindowSession.RegisterChild`는 창 종료와 Application Context 종료에 같은 콘텐츠 소유권을 적용한다. 일부 정리가 실패해도 나머지 정리를 시도한다.
- `ModalSession.RegisterChild`는 Modal 종료에 입력·외관 바인딩을 함께 반환한다.
- `ModalController.OnStackChanged`는 스택 및 Focus 복원 후 실제 상태를 알린다. 관찰자 예외는 로그로 보고하며 반환될 Modal 소유권을 끊지 않는다.
- Window/UITK 컨트롤 외관은 `Runtime/Resources/XeriUI/Theme/XeriDesktopTheme.uss`를 선택적으로 연결하고 Root에 `xeri-desktop-theme` 클래스를 추가해 재사용한다. 밝은 외관은 `xeri-desktop-theme--light`, 조밀한 간격은 `xeri-desktop-theme--compact`다. Validation 앱 전용 레이아웃과 색상은 Validation에 남는다.

## Presentation composition 검증

[Presentation Composition Contract](architecture/presentation-composition.md)의 다음 경계를 검증합니다.

- runtime Layer 획득 기반 Overlay / Modal
- Application Window Child Context가 global Overlay destination을 사용하는 사례
- Root Layout Scene Fade placement 없이 System destination에서 Fade가 동작하는 사례
- UITK-only infrastructure와 mixed UGUI capability 구성을 분리한 bootstrap 검증
- sibling Context의 logical ownership과 global destination이 섞여도 Focus/Input authority가 유지되는 사례
- Layer 획득과 feature 조립 중 backend/focus/source 실패 시 역순 rollback
- live consumer가 있는 Layer registration 해제 거부
- scene-authored borrowed surface의 native state 복원

이 항목들은 Plan topology mutation을 검증하는 것이 아니다.
immutable local Plan을 유지한 채 Layer Lease 획득·반환이 독립 lifetime으로 동작하는지를 검증한다.

Validation Scene의 탐색 항목은 자동 Edit/Play 테스트의 대체물이 아니다. 플랫폼·해상도·backend 조합의 모든 경우를 수동 Scene만으로 보장하지 않는다.
