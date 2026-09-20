# Xeri UI

`Xeri-UI`는 Xeri의 UI 전용 시스템을 하나의 Unity Package로 관리하는 저장소입니다.

## Package

- UPM package: `com.inonego.xeri.ui`
- Runtime assembly: `inonego.Xeri.UI`
- Editor assembly: `inonego.Xeri.UI.Editor`
- Base dependency: `com.inonego.xeri`

## 범위

- UI Core: `UIRuntime`, Layer, Screen, Modal, Presentation, Focus, Input, Transition
- Window: 다중 Window 상태, Registry, UITK Canvas/Panel, drag/resize, theme
- Tray: Window/작업 entry 표시, 선택, 닫기 요청, reorder
- Window View: Window content source와 UI session 복원 계약
- Bar: UGUI/UITK bar 표시
- UI 전용 backend, effect, layout, validation sample

범용 Drag/Drop과 Picker는 `com.inonego.xeri`가 소유하며 Xeri UI가 필요할 때 소비합니다.

## 문서

- [Manual](com.inonego.xeri.ui/Documentation~/index.md)
- [문서 작성 규칙](Docs/documentation-style.md)
- [문서 사이트 유지보수](Docs/site-maintenance.md)

## 로컬 문서 빌드

```pwsh
./build-docs.ps1
```
