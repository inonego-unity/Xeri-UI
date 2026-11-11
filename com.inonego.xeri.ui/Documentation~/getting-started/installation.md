# 설치와 의존성

Xeri UI의 Unity Package Manager ID는 **`com.inonego.xeri.ui`**입니다. 배포 단위는 저장소의 `com.inonego.xeri.ui` 폴더입니다.

## 지원 환경

| 항목 | 요구 또는 적용 범위 |
| --- | --- |
| Unity | Unity 7 (`7000.x`) |
| 기반 패키지 | `com.inonego.xeri` `0.0.1` |
| Input System | `com.unity.inputsystem` `6.7.0` |
| UGUI | `com.unity.ugui` `7.0.0` |
| DOTween | `DOTween` 및 `DOTween.Modules` 어셈블리 |
| URP / HDRP | 선택적 Native UI 고정밀·후처리 통합에만 해당 |

UPM dependency 버전은 `package.json`이 기준입니다. DOTween은 이 파일의 UPM dependency에 없지만, Runtime 전환 구현은 `DG.Tweening`을 사용하고 `inonego.Xeri.UI.asmdef`는 `DOTween.Modules`를 직접 참조합니다. **소비 Unity 프로젝트에 DOTween Runtime과 Modules를 별도로 설치해야 합니다.**

`DOTWEEN` 컴파일 심볼은 Bar의 Tween 기반 증감 전이를 활성화합니다. 심볼만 추가해서 어셈블리 누락을 해결할 수는 없습니다. 구형 DOTween Editor 확장의 Unity 7 예외와 Runtime/Modules 어셈블리 누락 오류는 별개로 진단합니다.

## Git / UPM 설치

프로젝트 `Packages/manifest.json`의 `dependencies`에 패키지 경로를 추가합니다.

```json
{
  "dependencies": {
    "com.inonego.xeri": "https://github.com/inonego-unity/Xeri.git?path=/com.inonego.xeri#main",
    "com.inonego.xeri.ui": "https://github.com/inonego-unity/Xeri-UI.git?path=/com.inonego.xeri.ui#main"
  }
}
```

위 코드는 dependency 항목의 **삽입 형식 예시**입니다. 기존 Manifest 전체를 교체하지 말고 기존 `dependencies`에 두 패키지를 병합하세요. 배포 프로젝트에서는 검증한 tag 또는 commit을 고정하는 것을 권장합니다.

로컬 checkout을 사용하는 경우:

```json
"com.inonego.xeri.ui": "file:../../Xeri-UI/com.inonego.xeri.ui"
```

이 경로는 소비 프로젝트의 `Packages` 위치에 따라 달라집니다. 기반 `com.inonego.xeri` 역시 소비 프로젝트에서 해석 가능해야 합니다.

## 설치 확인

1. Package Manager에서 Xeri UI, Xeri 기반 패키지, Input System, UGUI를 확인합니다.
2. Unity가 `DOTween`과 `DOTween.Modules` 어셈블리를 참조할 수 있는지 확인합니다.
3. Console의 C# 컴파일 오류를 해결합니다.
4. 패키지 내부 테스트를 실행하는 개발 프로젝트에서만 `"testables": ["com.inonego.xeri.ui"]`를 Manifest에 추가합니다.

## Native UI 출력 통합(선택)

일반 Native UI에는 별도 URP/HDRP 설치기가 필요하지 않습니다. FP16 출력이나 UI의 후처리 참여 시점을 변경하는 프로젝트에서만 `Tools > Xeri UI > URP/HDRP`의 **출력 설정 미리보기 → 출력 설정 적용**을 사용합니다.

**출력 설정 적용 해제**는 설치기가 소유한 변경을 현재 자산과 대조해 반환합니다. 카메라·컬러 버퍼·HDR 지원 조건, 미지원 환경의 Native fallback은 [Presentation 출력 정책](../core/presentation.md#native-ui-출력-정책)에 명시돼 있습니다.

설치를 마쳤으면 [프로젝트 설정과 시작](setup.md)으로 이동합니다.
