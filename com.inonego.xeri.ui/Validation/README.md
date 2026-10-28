# Xeri UI Validation Desktop

`Validation/Scenes/XeriUIValidation.unity` is the package-owned canonical integration scene for Xeri UI.

## Target state

This scene is the package validation surface.

The scene is a free-exploration desktop rather than a scripted test sequence. Open any app or system tool, move and resize windows, switch through the taskbar, minimize/restore/maximize, open nested modals, and exercise Core presentation behavior in any order.

## Visual direction

Validation-specific UI uses a neutral, familiar desktop-OS visual language:

- muted wallpaper and a compact system bar
- desktop app/tool icons
- a full-width taskbar fixed to the bottom edge
- standard light application surfaces
- system-style monitor and validation panels
- restrained system accent colors

Production Window, Tray and UIRuntime styles are not duplicated. The Validation scene uses those real production paths and only styles Validation-owned shell/application content.

## Runtime topology

The scene owns one full-screen HDR-enabled `Validation Camera`. Its settings asset
requests high precision output with `Unaffected` post processing. Runtime UI and
rendering use the same production `UIRuntime` policy and native rendering path.

Use Unity 7 and install the active pipeline with the existing
`Tools/Xeri UI/URP/출력 설정 미리보기` or
`Tools/Xeri UI/HDRP/출력 설정 미리보기`, followed by the corresponding
`출력 설정 적용` command. Pipeline installation belongs
to the host project; the scene does not replace Graphics/Quality settings or create
pipeline features at runtime. HDRP requires camera Postprocess/Custom Post Process
and the installed default Volume components to be active.

Open `Validation/Scenes/XeriUIValidation.unity` and enter Play mode. System Monitor
shows the selected output policy; it does not verify the actual attachment format.
A GPU capture is required to confirm that format. Normal
Native fallback remains available when the host pipeline is not configured or the
camera is unsupported.

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

The scene and its Unity USS/UXML define the Validation visual and behavior reference.
For a complete description, see [Validation Desktop](../Documentation~/validation.md).

- The package `UIRuntimeTheme.tss` imports `UIRuntimeBaseline.uss` to remove default outer margin/padding differences from standard UITK controls.
- Validation USS owns explicit spacing, control height, typography and shell geometry through `--xeri-validation-*` tokens.
- `XeriUIValidationPanelSettings` uses a 1920×1080 reference resolution and expands the panel when the display aspect ratio differs. The desktop and its UITK layers keep a centered 16:9 workspace.
- The Window layer occupies the area between the 60-unit system bar and the 64-unit taskbar. Maximized windows fill that workspace.
- Radial gradients use Unity Native rendering. The separate web mockup is a comparison aid; matching CSS values do not guarantee matching final colors.
