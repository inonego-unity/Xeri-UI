# Xeri UI 문서 작성 규칙

이 문서는 Xeri UI 저장소의 README, 공개 Manual, 개념 문서, 사용 가이드와 유지보수 문서의 공통 작성 기준을 정의합니다.
표시 제목과 본문은 한글을 기본으로 하며 코드 식별자와 고유 기술 용어는 원래 표기를 유지합니다.

## 문서 종류

- **모듈 개요**: 모듈의 목적, 책임 경계, 핵심 개념과 구조를 설명합니다.
- **사용 가이드**: 특정 작업을 실제로 수행하는 절차를 설명합니다.
- **개념 문서**: 여러 모듈에 공통으로 적용되는 설계 개념과 규칙을 설명합니다.
- **유지보수 문서**: 내부 구현, 검증 기준과 변경 제약을 설명합니다.

`Runtime/**/README.md`는 기본적으로 모듈 개요로 사용합니다.
사용 절차가 길거나 여러 모듈을 가로지르면 `Documentation~` 사용 가이드로 분리합니다.
내부 구현 규칙과 테스트 경로는 일반 사용자 개요 문서와 분리합니다.

## 문서 위치

- `Docs/`: 문서 작성 규칙과 문서 사이트 유지보수 자료
- `Docs/api-bridge/`: DocFX API Reference 진입 페이지
- `com.inonego.xeri.ui/Documentation~/`: 공개 Manual의 source of truth
- `com.inonego.xeri.ui/Runtime/**/README.md`: 해당 코드 영역의 모듈 개요
- `index.md`, `toc.yml`: GitHub Pages/DocFX 최상위 진입점

## 제목과 용어

- 제품명은 `Xeri UI`로 통일합니다.
- package ID는 `com.inonego.xeri.ui`, runtime assembly는 `inonego.Xeri.UI`로 표기합니다.
- `Runtime`, `Context`, `Lease`, `Handle`, `Screen`, `Window`처럼 코드와 직접 대응하는 용어는 억지로 번역하지 않습니다.
- public 타입, 멤버와 파일명은 백틱으로 감쌉니다.
- Window와 Screen처럼 수명 계약이 다른 개념은 유사하다는 이유로 같은 용어로 합치지 않습니다.

## 내용 작성 원칙

- 폴더와 파일 목록보다 모듈이 제공하는 계약과 책임을 먼저 설명합니다.
- 주요 시스템 문서는 최소한 `왜 필요한가`, `언제 사용하는가`, `기본 사용`에 답해야 합니다.
- 코드가 실제로 보장하지 않는 사용법이나 미래 계획을 현재 기능처럼 서술하지 않습니다.
- API 멤버 전체를 Manual에 복제하지 않고 상세 멤버는 API Reference에 맡깁니다.
- 소유권과 종료 순서가 중요한 기능은 누가 획득하고 누가 종료하는지 명시합니다.
- callback/부분 생성 실패가 중요한 시스템은 rollback과 실패 후 상태를 함께 설명합니다.
- base Xeri 기능과 Xeri UI 기능의 책임을 혼동하지 않습니다.

## Base Xeri와의 경계

범용 `Lease`, primitive, serialization, bootstrapper, Drag/Drop과 Picker는 base Xeri 문서를 참조합니다.
Xeri UI 문서는 이 기능을 자체 소유 API처럼 설명하지 않고 UI 모듈이 어떻게 소비하는지만 설명합니다.

## 파일명과 링크

- `README.md` 외 공개 문서 파일명은 영문 kebab-case를 기본으로 합니다.
- 같은 저장소 안에서는 상대 링크를 우선합니다.
- base Xeri 문서는 해당 Xeri 저장소 또는 공개 문서 사이트로 명시적으로 연결합니다.
- 문서를 이동하거나 분할한 뒤에는 저장소 전체 Markdown 상대 링크를 검사합니다.

## 공개 목차

`com.inonego.xeri.ui/Documentation~/toc.yml`이 공개 Manual의 탐색 구조를 정의합니다.
Runtime README는 모듈 개요 원본으로 유지하고 상세 절차는 Manual 페이지로 분리합니다.

## API Reference

public API 멤버 설명은 C# XML documentation과 DocFX API Reference를 기준으로 합니다.
Manual은 사용 목적, 계약, 흐름과 주의사항에 집중합니다.
