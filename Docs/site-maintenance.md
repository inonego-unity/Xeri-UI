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

지정한 Unity 프로젝트는 `inonego.Xeri.csproj`, Unity/package reference와 compiled base Xeri assembly를 제공해야 합니다.
빌드 스크립트는 해당 Unity 프로젝트의 manifest나 source를 수정하지 않습니다.

## API snapshot

GitHub hosted runner에는 Unity 개발 환경이 없으므로 API metadata를 CI에서 직접 재생성하지 않습니다.
개발 환경의 full build가 Xeri UI의 production Runtime/Editor source와 asmdef를 기준으로 snapshot과 hash를 갱신합니다.

public API 또는 assembly/package 구성이 바뀌면 개발 환경에서 `build-docs.ps1`을 실행해 snapshot을 갱신합니다.

## CI와 동일한 검증

```pwsh
pwsh ./build-docs.ps1 -VerifySnapshot
pwsh ./build-docs.ps1 -SkipMetadata
```

첫 명령은 snapshot hash를 검증하고 두 번째 명령은 저장된 snapshot만으로 사이트가 만들어지는지 확인합니다.

## GitHub Pages

`.github/workflows/docs-pages.yml`은 `main` push 또는 수동 실행에서 다음 순서로 동작합니다.

```text
Checkout
→ API snapshot 최신성 검증
→ snapshot 기반 DocFX build
→ _site artifact 업로드
→ GitHub Pages 배포
```

저장소가 `inonego-unity/Xeri-UI`로 게시되면 Pages 기본 주소는 다음 형식을 사용합니다.

`https://inonego-unity.github.io/Xeri-UI/`

`_site/`와 `.docfx/`는 Git에 포함하지 않습니다.
