# 설치

`com.inonego.xeri.ui`는 Unity Package Manager에서 사용하는 Xeri UI package입니다.

## 요구 사항

- Unity 7000.x (Unity 7)
- `com.inonego.xeri`
- Input System
- UGUI 7.0
- 고정밀 출력 사용 시 URP 17.7 또는 HDRP 17.7을 프로젝트에 설치
- DOTween / `DOTween.Modules`

정확한 package dependency와 버전은 `package.json`을 기준으로 확인합니다.
일반 Native UI에는 SRP 패키지가 필수가 아니며, 설치된 파이프라인의 통합 모듈만 컴파일됩니다.

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

## Native UI 출력 설정 (선택)

고정밀 출력 또는 UI 후처리가 필요하면 사용하는 파이프라인의 `Tools/Xeri UI/URP` 또는 `Tools/Xeri UI/HDRP`에서 **출력 설정 미리보기 → 출력 설정 적용**을 선택하고 `UISettingsAsset`에서 출력 정책을 설정합니다. 지원 조건이 맞지 않으면 Unity Native 출력이 유지됩니다.

같은 메뉴의 **출력 설정 적용 해제**는 설치기가 소유한 변경을 현재 설정과 대조해 반환하고 사용자 편집을 보존합니다. HDRP는 `Assets/`에 저장된 기본 Volume Profile과 활성화된 Camera Postprocess/Custom Post Process가 필요합니다. 공통 정책과 파이프라인별 지원 범위는 [Presentation](../core/presentation.md#native-ui-출력-정책)을 참고합니다.

## 다음 단계

1. [설정과 시작](setup.md)
2. [전체 Architecture](../architecture/overview.md)
3. [Core](../core/index.md)
4. 필요하면 [Window](../window/index.md), [Tray](../tray.md), [Bar](../bar.md)를 사용합니다.
