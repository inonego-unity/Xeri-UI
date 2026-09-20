---
name: xeri-ui-test-editor-structure
description: Xeri UI package의 runtime/editor/test assembly와 module-local TEST asmref 배치 규칙.
---

# Xeri UI Test / Editor Structure

## Production assemblies

- Runtime: `com.inonego.xeri.ui/Runtime/inonego.Xeri.UI.asmdef`
- Editor: `com.inonego.xeri.ui/Editor/inonego.Xeri.UI.Editor.asmdef`
- Editor 전용 production source를 Runtime 모듈 옆에 둘 때 `Runtime/{module}/Editor/Editor.asmref`로 Editor assembly에 연결합니다.

## Test assemblies

- EditMode: `Tests/EDIT/inonego.Xeri.UI.TEST.EDIT.asmdef`
- PlayMode: `Tests/PLAY/inonego.Xeri.UI.TEST.PLAY.asmdef`
- Editor-specific EditMode: `Tests/Editor/inonego.Xeri.UI.Editor.TEST.EDIT.asmdef`

## Module-local tests

module test source는 구현 옆 `TEST` 아래에 둡니다.

```text
Runtime/{module}/TEST/EDIT/
Runtime/{module}/TEST/PLAY/
Runtime/{module}/TEST/Editor/
```

각 폴더는 asmdef를 새로 만들지 않고 assembly 이름을 직접 지정하는 asmref를 사용합니다.

```json
{ "reference": "inonego.Xeri.UI.TEST.EDIT" }
{ "reference": "inonego.Xeri.UI.TEST.PLAY" }
{ "reference": "inonego.Xeri.UI.Editor.TEST.EDIT" }
```

GUID 기반 asmref를 사용하지 않습니다.

## Placement rules

- `TEST/EDIT`에는 Edit test asmref만 둡니다.
- `TEST/PLAY`에는 Play test asmref만 둡니다.
- `TEST/Editor`에는 Editor test asmref만 둡니다.
- `Runtime/{module}/Editor`에는 production Editor asmref만 둡니다.
- 빈 TEST/Editor 폴더를 미리 만들지 않습니다.
- 하나의 assembly folder에 asmdef를 둘 이상 만들지 않습니다.
- 테스트 source를 production assembly에 직접 포함시키지 않습니다.

## Validation

구조 변경 후 다음을 확인합니다.

1. 모든 asmref reference 이름이 실제 asmdef name과 일치하는가.
2. `TEST/Editor`가 Play assembly를 가리키지 않는가.
3. Editor production source가 runtime assembly에 들어가지 않는가.
4. tests가 module-local 위치에 있어도 test assembly로 컴파일되는가.
