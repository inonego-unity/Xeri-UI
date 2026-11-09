# Xeri UI

`Xeri-UI`는 `com.inonego.xeri.ui` Unity package를 관리하는 저장소입니다.

## Package

- UPM package: `com.inonego.xeri.ui`
- Runtime assembly: `inonego.Xeri.UI`
- Editor assembly: `inonego.Xeri.UI.Editor`
- Base dependency: `com.inonego.xeri`

## 범위

- Core: Presentation, Screen, Modal, Focus, Input, Scene Fade
- Window: Workspace, Window Session, Registry, USS 테마 전환, interaction과 persistence
- Tray: entry 표시, 선택, 닫기 요청, optional reorder
- Window View: content source와 UI-local session
- Bar: UGUI/UITK 값 표시

범용 Drag/Drop과 Picker는 base Xeri가 소유합니다.

## 문서

- [Xeri UI Manual](com.inonego.xeri.ui/Documentation~/index.md)
- [Window와 USS 테마](com.inonego.xeri.ui/Documentation~/window/index.md)
- [Window View Source와 저장](com.inonego.xeri.ui/Documentation~/window/view-source.md)
- [Presentation Architecture](com.inonego.xeri.ui/Documentation~/architecture/presentation.md)
- [문서 작성 규칙](Docs/documentation-style.md)
- [문서 사이트 유지보수](Docs/site-maintenance.md)

## 로컬 문서 빌드

```pwsh
./build-docs.ps1
```

API metadata 생성에는 패키지의 각 production assembly에 대응하는 Unity 생성 `.csproj`가 필요합니다. 준비·검증 절차는 [문서 사이트 유지보수](Docs/site-maintenance.md)를 참고합니다.
