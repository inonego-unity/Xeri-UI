# 설치

Xeri UI의 UPM package ID는 `com.inonego.xeri.ui`입니다.

## 요구 사항

- Unity 6000.0 이상
- `com.inonego.xeri`
- Input System
- UGUI
- package가 사용하는 DOTween assembly

정확한 직접 dependency는 package root의 `package.json`을 기준으로 확인합니다.

## Git / UPM

```json
"com.inonego.xeri": "https://github.com/inonego-unity/Xeri.git?path=/com.inonego.xeri#main",
"com.inonego.xeri.ui": "https://github.com/inonego-unity/Xeri-UI.git?path=/com.inonego.xeri.ui#main"
```

릴리스 사용 시에는 branch 대신 서로 호환되는 version tag를 고정합니다.

## 로컬 checkout

```json
"com.inonego.xeri.ui": "file:../../Xeri-UI/com.inonego.xeri.ui"
```

Package Test Runner에 테스트를 노출해야 하는 개발 프로젝트는 `testables`에 package ID를 추가합니다.

```json
"testables": ["com.inonego.xeri.ui"]
```

## 다음 단계

- [구조와 의존 방향](../concepts/architecture.md)
- [UI Core 설정과 시작](../modules/core/setup.md)
- [Window](../modules/window.md)
- [Tray](../modules/tray.md)
