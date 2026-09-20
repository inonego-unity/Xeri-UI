# Xeri UI 문서

Xeri UI는 application UI lifecycle과 다중 Window/Tray UI를 하나의 package에서 제공하는 Unity UI 시스템입니다.

## 처음이라면

1. [설치](getting-started/installation.md)
2. [구조](concepts/architecture.md)
3. [UI Core 설정과 시작](modules/core/setup.md)
4. 필요한 경우 [Window](modules/window.md)와 [Tray](modules/tray.md)를 추가합니다.

## 모듈

- [UI Core](modules/core/architecture.md): Layer, Screen, Modal, Presentation, Focus, Input과 Transition
- [Window](modules/window.md): Window 상태, Registry, UITK Canvas/Panel과 interaction
- [Tray](modules/tray.md): entry 표시, 선택, 닫기 요청과 reorder
- [Window View](modules/window-view.md): Window content source와 UI session 복원
- Bar: UGUI/UITK bar 표시 API

범용 Drag/Drop과 Picker는 base Xeri의 재사용 모듈이며 이 package로 이동하지 않습니다.
