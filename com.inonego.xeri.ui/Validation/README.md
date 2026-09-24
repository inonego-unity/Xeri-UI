# Xeri UI Validation Desktop

`Validation/Scenes/XeriUIValidation.unity` is the package-owned canonical integration scene for Xeri UI.

## Target state

This scene is the package validation surface.

The scene is a free-exploration desktop rather than a scripted test sequence. Open any app or system tool, move and resize windows, switch through the taskbar, minimize/restore/maximize, open nested modals, and exercise Core presentation behavior in any order.

## Visual direction

Validation-specific UI uses a neutral, familiar desktop-OS visual language:

- muted wallpaper and a compact system bar
- desktop app/tool icons
- a centered taskbar
- standard light application surfaces
- system-style monitor and validation panels
- restrained system accent colors

Production Window, Tray and UIRuntime styles are not duplicated. The Validation scene uses those real production paths and only styles Validation-owned shell/application content.

## Runtime topology

- `Desktop` — scene-authored UITK layer with authored static shell, Status and Taskbar placement.
- `Desktop.Windows` — generated UITK Window workspace.
- `Desktop.Overlay` — generated UITK global overlay.
- `Desktop.Modal` — generated UITK global modal.
- `SystemFade` — generated UGUI scene fade.

`PresentationLayout` remains the topology/order source of truth. Scene-authored hosts provide physical roots only.

## Apps and tools

- Task Board — natural Window/input interaction.
- Preferences — standard UITK form/focus interaction.
- Application A/B — the same Child Layout and Screen IDs in independent Child PresentationSession/UIContext instances.
- Core Lab — root Screen stack, transient Presentation lifetime, Spotlight and Scene Fade.
- Window Lab — Window state, AlwaysOnTop and per-Tray state projection.
- Modal Lab — root nested Modal stack.

The System Monitor is read-only telemetry. The Validation panel records exercised coverage but never gates interaction.

## Layout contract

Unity is the single Validation visual and behavior source of truth.

- The package `UIRuntimeTheme.tss` imports `UIRuntimeBaseline.uss` to remove default outer margin/padding differences from standard UITK controls.
- Validation USS owns explicit spacing, control height, typography and shell geometry through `--xeri-validation-*` tokens.
- `UIPanelSettings` provides the 1920×1080 reference resolution used by the Validation scene.
