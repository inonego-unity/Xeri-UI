# Xeri UI Core

이 폴더는 Xeri UI의 Presentation, Screen, Modal, Focus/Input와 Runtime composition을 구현합니다.

주요 코드 영역:

```text
Runtime/Core/Runtime        UIRuntime / UIContext
Runtime/Core/Presentation   Layout / Plan / Session / Host
Runtime/Core/Policy         Screen / Modal
Runtime/Core/Interaction    Focus / input policy
Runtime/Core/Backends       UGUI / UITK / InputSystem adapter
```

사용법:

- [Core 개요](../../Documentation~/core/index.md)
- [Screen과 입력](../../Documentation~/core/screens.md)
- [Presentation](../../Documentation~/core/presentation.md)

구조 계약:

- [Presentation Architecture](../../Documentation~/architecture/presentation.md)
