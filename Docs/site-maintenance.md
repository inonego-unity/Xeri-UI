# Xeri UI 문서 사이트 유지보수

이 문서는 Xeri UI의 DocFX 사이트 빌드, API Reference snapshot과 GitHub Pages 배포 파이프라인을 설명합니다.
사용자-facing 설명은 루트 README나 `com.inonego.xeri.ui/Documentation~`에 작성하고 운영 절차는 이 문서에 둡니다.

## 구성

```text
Manual Markdown
    +
Generated API metadata snapshot
    ↓
DocFX
    ↓
_site/
    ↓
GitHub Pages
```

관련 파일:

- `docfx.json`: DocFX metadata/build 설정
- `dotnet-tools.json`: 저장소 로컬 DocFX 버전
- `build-docs.ps1`: 로컬 API metadata와 사이트 빌드
- `Docs/api-snapshot.ps1`: metadata project와 snapshot 생성 helper
- `Docs/generated-api.zip`: CI용 API metadata snapshot
- `Docs/generated-api.sha256`: snapshot 최신성 검증값
- `.github/workflows/docs-pages.yml`: GitHub Pages 배포 workflow

## 로컬 빌드

Unity 개발 환경에서 Manual과 API Reference를 함께 갱신합니다.

```pwsh
./build-docs.ps1
```

필요하면 API metadata 생성에 사용할 Unity 프로젝트를 명시합니다.

```pwsh
./build-docs.ps1 -UnityProjectRoot "<UnityProject>"
```

지정한 Unity 프로젝트에는 현재 Xeri UI package가 설치되어 있어야 합니다. 문서 생성기는 `Runtime`과 `Editor`의 **모든 production `.asmdef`**를 대상으로 metadata 프로젝트를 만들기 때문에, 각 assembly의 Unity 생성 `.csproj`가 필요합니다.

현재 필요한 프로젝트는 다음 여섯 개입니다.

- `inonego.Xeri.UI.csproj`
- `inonego.Xeri.UI.Editor.csproj`
- `inonego.Xeri.UI.URP.csproj`
- `inonego.Xeri.UI.URP.Editor.csproj`
- `inonego.Xeri.UI.HDRP.csproj`
- `inonego.Xeri.UI.HDRP.Editor.csproj`

URP/HDRP 통합은 Window 테마 기능과 무관하지만, **전체 API Reference**를 생성하려면 이 assembly의 프로젝트도 준비돼야 합니다. 누락된 `.csproj`가 있으면 생성 스크립트는 오류로 종료합니다. 일부 assembly만 포함한 결과로 전체 API snapshot을 덮어쓰지 않습니다.

metadata helper는 Unity 생성 프로젝트의 define과 assembly reference를 사용하고, API 대상 source를 Xeri UI package의 production Runtime/Editor에서 다시 구성합니다. Unity 프로젝트의 manifest나 source는 변경하지 않습니다.

## API snapshot

GitHub hosted runner에는 Unity 개발 환경이 없으므로 API metadata를 CI에서 직접 재생성하지 않습니다.
개발 환경의 full build가 production Runtime/Editor source와 asmdef를 기준으로 snapshot과 hash를 갱신합니다.

Runtime/Editor의 production source, `.asmdef`/`.asmref` 또는 `package.json`이 바뀌면 source hash가 달라집니다. 이 경우 필요한 Unity 생성 프로젝트를 갖춘 환경에서 `build-docs.ps1`을 실행해 snapshot과 hash를 함께 갱신합니다. `Docs/generated-api.zip`의 항목을 수동으로 제거하거나 `.sha256`만 고치지 않습니다.

## CI와 동일한 검증

```pwsh
pwsh ./build-docs.ps1 -VerifySnapshot
pwsh ./build-docs.ps1 -SkipMetadata
```

첫 명령은 snapshot hash를 검증하고 두 번째 명령은 저장된 snapshot만으로 사이트가 만들어지는지 확인합니다. `-SkipMetadata` 빌드가 성공하더라도 API Reference의 최신성이 보장되지는 않으므로, 배포 전에는 반드시 `-VerifySnapshot` 결과도 확인합니다.

## GitHub Pages

`.github/workflows/docs-pages.yml`은 `main` push 또는 수동 실행에서 다음 순서로 동작합니다.

```text
Checkout
→ API snapshot 최신성 검증
→ snapshot 기반 DocFX build
→ _site artifact 업로드
→ GitHub Pages 배포
```

Pages 주소:

`https://inonego-unity.github.io/Xeri-UI/`

`_site/`와 `.docfx/`는 Git에 포함하지 않습니다.
