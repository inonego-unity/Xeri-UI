# Xeri UI agent notes

새 Xeri UI 저장소에서는 `.agents/skills/xeri-ui-test-editor-structure/SKILL.md`의 테스트/Editor assembly 구조를 우선 적용합니다.

- 기존 Xeri, 기존 Window/Tray 저장소와 소비 프로젝트를 별도 요청 없이 수정하지 않습니다.
- package public namespace는 `inonego.Xeri.UI`, `inonego.Xeri.UI.Window`, `inonego.Xeri.UI.Tray` 계열을 사용합니다.
- 범용 Drag/Drop과 Picker는 base Xeri 소유로 유지합니다.
- Unity asset을 이동/복사할 때 serialized migration을 위해 기존 `.meta` GUID 보존 여부를 명시적으로 검토합니다.
- 공개 Manual source of truth는 `com.inonego.xeri.ui/Documentation~`입니다.
