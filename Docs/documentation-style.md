# Xeri UI 문서 작성 규칙

Xeri UI 문서는 완성된 제품의 구조와 사용법을 현재형으로 설명합니다. 변경 이력이나 migration 과정은 공개 Manual에 섞지 않습니다.

## Source of truth

문서는 네 역할로 나눕니다.

```text
Documentation~/
├─ getting-started/   설치와 최초 구성
├─ architecture/      안정된 구조와 invariant
├─ core/              Core 기능 사용법
├─ window/            Window 기능 사용법
├─ tray.md            Tray 사용법
├─ bar.md             Bar 사용법
└─ guides/            특정 작업 절차

Docs/
├─ documentation-style.md
├─ site-maintenance.md
├─ api-bridge/
└─ work/              삭제 가능한 내부 작업 문서

Runtime/**/README.md  코드 폴더 안내판
C# XML docs           API Reference source
```

## 공개 Manual

`com.inonego.xeri.ui/Documentation~/`가 사용자-facing Manual의 source of truth입니다.

공개 문서는 다음 원칙을 따릅니다.

- 완성된 시스템을 현재형으로 설명
- 제거된 API나 이전 구조의 변경 이력을 설명하지 않음
- public entrypoint와 ownership/lifetime을 우선 설명
- 내부 타입 전체를 Manual에 복제하지 않음
- detailed member reference는 DocFX API Reference에 맡김
- 실제 API가 보장하지 않는 사용법을 현재 기능처럼 쓰지 않음

## Architecture

`Documentation~/architecture/`는 구조 계약을 설명합니다.

- responsibility / ownership
- topology / ordering
- lifetime / authority
- public / composition / backend extension boundary
- invariant
- 금지 설계

Architecture 문서는 migration plan이나 작업 완료 상태를 참조하지 않습니다.

## 기능 문서

`core/`, `window/`, `tray.md`, `bar.md`는 사용자가 실제 기능을 사용하는 방법을 설명합니다.

기본 순서:

1. 기능의 의미
2. Common API
3. lifetime / ownership
4. 필요한 advanced composition
5. 관련 문서

## Guides

`guides/`는 하나의 concrete 작업을 처음부터 끝까지 수행하는 절차를 설명합니다.

기능 개념 설명을 Guide에 중복하지 않고 해당 Core/Window 문서로 연결합니다.

## Runtime README

`Runtime/**/README.md`는 코드 폴더 안내판입니다.

다음만 유지합니다.

- 이 폴더가 구현하는 책임
- 주요 코드 영역
- 관련 Manual 링크
- 관련 Architecture 링크

긴 사용 예제나 별도의 architecture 설명을 복제하지 않습니다.

## 내부 작업 문서

`Docs/work/`는 구현 중 필요한 임시 checklist, migration 기록, 조사 메모를 둘 수 있는 내부 영역입니다.

- DocFX 공개 Manual에 포함하지 않음
- 공개 문서가 `Docs/work/`를 참조하지 않음
- 작업 종료 후 삭제 가능

## 용어

- 제품명: `Xeri UI`
- package: `com.inonego.xeri.ui`
- runtime assembly: `inonego.Xeri.UI`
- 코드 타입/멤버/파일명은 백틱으로 표기
- `Runtime`, `Context`, `Session`, `Lease`, `Handle`, `Screen`, `Window`처럼 코드 의미와 직접 대응하는 용어는 억지로 번역하지 않음

동일 semantic에 여러 이름을 만들지 않습니다. 예를 들어 Window application mount root는 `ContentRoot` 하나만 사용합니다.

## Base Xeri와의 경계

범용 `Lease`, primitive, serialization, Bootstrapper, Drag/Drop과 Picker는 base `com.inonego.xeri`가 소유합니다.

Xeri UI 문서는 해당 기능을 자체 소유 API처럼 재정의하지 않고 UI domain에서 어떻게 소비하는지만 설명합니다.

## 링크와 목차

- 공개 navigation은 `Documentation~/toc.yml`이 정의
- 저장소 내부 링크는 상대 경로 우선
- 파일 이동 후 Markdown 상대 링크와 TOC href를 검사
- 공개 Manual에서 `Docs/work/`로 링크하지 않음

## API Reference

public API member 설명은 C# XML documentation과 DocFX generated API를 기준으로 합니다.

public API 또는 assembly 구성이 바뀌면 `build-docs.ps1`로 API snapshot을 갱신합니다.
