# Xeri UI

`com.inonego.xeri.ui`는 Unity의 application UI lifecycle과 Window/Tray UI를 제공하는 패키지입니다.

## 주요 모듈

- **UI Core** — Layer, Screen, Modal, Presentation, Focus, Input, Transition과 UI 수명
- **Window** — 이동, resize, minimize/maximize, focus, Registry와 UITK Window
- **Tray** — entry 표시, 선택, 닫기 요청과 reorder
- **Window View** — Window content source와 UI session 저장/복원
- **Bar** — UGUI/UITK bar 표시

범용 Drag/Drop과 Picker는 base Xeri에서 제공하며 이 패키지가 필요한 기능만 소비합니다.

## Namespace

```text
inonego.Xeri.UI
inonego.Xeri.UI.Window
inonego.Xeri.UI.Window.Editor
inonego.Xeri.UI.Tray
```

## 의존 방향

```text
com.inonego.xeri.ui
        ↓
com.inonego.xeri
```

base Xeri가 이 패키지를 역참조하지 않는 단방향 경계를 유지합니다.

## 문서

자세한 사용법은 [Documentation~/index.md](Documentation~/index.md)에서 시작합니다.
