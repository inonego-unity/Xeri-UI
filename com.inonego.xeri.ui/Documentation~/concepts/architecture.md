# 구조와 의존 방향

Xeri UI는 UI 전용 lifecycle과 표시 시스템을 `com.inonego.xeri.ui`에 모으고, UI 밖에서도 재사용되는 기반 기능은 `com.inonego.xeri`에 유지합니다.

## Package 경계

```text
com.inonego.xeri.ui
        ↓
com.inonego.xeri
```

의존 방향은 단방향입니다. base Xeri는 Xeri UI 타입을 참조하지 않습니다.

## Xeri UI가 소유하는 것

- `UIRuntime`과 `UIContext`
- Presentation Layer와 Profile
- Screen Stack과 Modal
- Presentation Alpha/Visibility와 Transition
- Focus와 UI/Gameplay Input composition
- UI backend, safe area, world projection, Scene Fade와 Spotlight
- Window 상태/Registry와 UITK Window
- Tray 표시와 reorder presentation
- Window content View Source/Session
- UGUI/UITK Bar

## base Xeri에 유지하는 것

UI 밖에서도 독립적으로 의미가 있는 기능은 base Xeri에 둡니다.

- `Lease`, primitive, serialization, bootstrapper와 공통 utility
- Drag/Drop core와 UGUI/UITK adapter
- Picker model/view/editor 기능
- 다른 비 UI 도메인 모듈

Window titlebar drag는 base Xeri의 Drag/Drop을 소비하지만 Drag/Drop 자체를 이 package가 소유하지 않습니다.

## Core와 Window의 관계

UI Core는 application 범위 Layer/Screen/Modal/Input 수명을 담당합니다.
Window는 한 표시 영역 안에서 여러 자유 배치 Window의 상태, 순서와 interaction을 담당합니다.

Window를 `UIRuntime`의 필수 하위 서비스로 강제하지 않습니다. 프로젝트는 Window가 필요할 때 UI Layer 또는 UITK host에 `XeriWindowCanvas`를 조립할 수 있습니다.

## Window View의 범위

`IXeriUIViewSource`, `IXeriUIViewResolver`, `XeriUIViewScope`, `IXeriUISession`은 Window content 생성과 복원을 위한 계약입니다.
Screen의 `IScreenSource`와는 lifecycle 책임이 다르므로 하나의 source 계약으로 합치지 않습니다.
