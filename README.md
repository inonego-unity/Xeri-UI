# Xeri UI

`Xeri-UI`는 `com.inonego.xeri.ui` Unity package를 관리하는 저장소입니다.

## Package

- UPM package: `com.inonego.xeri.ui`
- Runtime assembly: `inonego.Xeri.UI`
- Editor assembly: `inonego.Xeri.UI.Editor`
- Base dependency: `com.inonego.xeri`

## 범위

- Core: Presentation, Screen, Modal, Focus, Input, Scene Fade
- Window: Workspace, Window Session, Registry, interaction과 persistence
- Tray: entry 표시, 선택, 닫기 요청, optional reorder
- Window View: content source와 UI-local session
- Bar: UGUI/UITK 값 표시

범용 Drag/Drop과 Picker는 base Xeri가 소유합니다.

## 문서

- [Xeri UI Manual](com.inonego.xeri.ui/Documentation~/index.md)
- [Presentation Architecture](com.inonego.xeri.ui/Documentation~/architecture/presentation.md)
- [문서 작성 규칙](Docs/documentation-style.md)
- [문서 사이트 유지보수](Docs/site-maintenance.md)

## 로컬 문서 빌드

```pwsh
./build-docs.ps1
```
