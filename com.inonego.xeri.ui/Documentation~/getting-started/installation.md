# 설치

`com.inonego.xeri.ui`는 Unity Package Manager에서 사용하는 Xeri UI package입니다.

## 요구 사항

- Unity 6000.x
- `com.inonego.xeri`
- Input System
- UGUI
- DOTween / `DOTween.Modules`

정확한 package dependency와 버전은 `package.json`을 기준으로 확인합니다.

## Git / UPM

```json
"com.inonego.xeri": "https://github.com/inonego-unity/Xeri.git?path=/com.inonego.xeri#main",
"com.inonego.xeri.ui": "https://github.com/inonego-unity/Xeri-UI.git?path=/com.inonego.xeri.ui#main"
```

배포 프로젝트에서는 사용하는 release tag나 commit을 고정합니다.

## 로컬 checkout

```json
"com.inonego.xeri.ui": "file:../../Xeri-UI/com.inonego.xeri.ui"
```

package tests를 Unity Test Runner에 노출하는 개발 프로젝트는 manifest의 `testables`에 package ID를 추가합니다.

```json
"testables": ["com.inonego.xeri.ui"]
```

## 다음 단계

1. [설정과 시작](setup.md)
2. [전체 Architecture](../architecture/overview.md)
3. [Core](../core/index.md)
4. 필요하면 [Window](../window/index.md), [Tray](../tray.md), [Bar](../bar.md)를 사용합니다.
