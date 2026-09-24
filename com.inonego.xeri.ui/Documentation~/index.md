# Xeri UI

Xeri UI는 Unity UI Toolkit과 UGUI 위에서 application-level UI lifecycle, presentation topology, focus/input authority와 Window/Tray UI를 구성하는 Unity package입니다.

## 처음 시작하기

1. [설치](getting-started/installation.md)
2. [설정과 시작](getting-started/setup.md)
3. [전체 구조](architecture/overview.md)
4. 필요한 기능의 사용 문서를 읽습니다.

## Architecture

- [전체 구조](architecture/overview.md): Runtime, Context, Presentation, Screen, Window의 책임 경계
- [Presentation Architecture](architecture/presentation.md): 현재 topology, Host, Source, Session, ordering, lifetime과 확장 경계
- [Presentation Composition Contract](architecture/presentation-composition.md): Target 해석, Layer Lease lifetime, destination과 validation 기준

## Core

- [Core 개요](core/index.md): `UIRuntime`, `UIContext`, Screen/Modal/Presentation 진입점
- [Screen과 입력](core/screens.md): Screen lifecycle, Focus, Gameplay Input, Cursor
- [Presentation](core/presentation.md): Target/Layer Lease, static Placement, Modal, Scene Host와 backend output
- [문제 해결](core/troubleshooting.md): Runtime/Host/Focus/Input/Presentation 진단

## Window와 UI 모듈

- [Window](window/index.md): Workspace, Simple/Application Window, ordering과 persistence
- [Window View Source](window/view-source.md): View acquire/release와 UI-local session
- [Tray](tray.md): entry 표시, 선택, 닫기 요청과 optional reorder
- [Bar](bar.md): UGUI/UITK 값 범위와 변화 표시

## Guides

- [Window와 View Source 연결하기](guides/create-window-view.md)

범용 Drag/Drop과 Picker는 base `com.inonego.xeri`가 소유합니다. Xeri UI는 UI domain에서 필요한 계약만 소비합니다.
