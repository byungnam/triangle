# Triangle 레거시 프로토타입 분석 (2015, Java)

이 디렉터리는 MonoGame 기반 리라이트에 앞서, 기존 Java 프로토타입이 **실제로 무엇을 하고 있었는지**를 기록한 문서다.
설계 의도가 아니라 코드에서 확인된 동작을 기준으로 작성했고, 의도와 구현이 어긋나는 곳은 따로 표시했다.

| 문서 | 내용 |
|---|---|
| [game-rules.md](game-rules.md) | 유닛·스탯·전술(갬빗)·전투 루프·액션·효과 — 리라이트에 그대로 가져갈 핵심 |
| [protocol-and-data.md](protocol-and-data.md) | 서버 구성, protobuf 메시지, 메시지 흐름, SQLite 스키마, 테스트 |
| [client.md](client.md) | Android 클라이언트 화면 흐름, UnitManager(관리 툴), 아트 리소스 |
| [rewrite-notes.md](rewrite-notes.md) | 버그·미완성 목록, 리라이트 전에 결정해야 할 사항 |

> **레거시 Java 코드는 저장소에서 삭제되었다.** 문서에 나오는 모든 `파일:줄` 참조는 커밋 `db2a5f1` 기준이다.
> 원본 확인: `git show db2a5f1:Triangle-GameServer/src/Triangle/Unit/UnitCombatable.java`, 또는 `git worktree add ../triangle-legacy db2a5f1`.
> Java 소스는 CP949 인코딩이므로 `| iconv -f cp949`를 붙여서 읽는다.

## 한 줄 요약

**자동 전투 RPG.** 플레이어는 유닛을 모으고, 스탯을 배분하고, 유닛마다 "조건 → 행동" 전술 목록(FF12 갬빗 방식)을 짜고, 팀을 꾸려 NPC 팀과 싸운다.
전투는 서버에서 완전히 자동으로 진행된다. UI는 Android 기본 위젯(리스트·텍스트·버튼)뿐이라 "텍스트 기반"이라 불렀지만, 전투 로그 화면 자체는 구현되지 않았다.

## 구성 요소

```
                    ┌──────────────────────┐
  Android Client ──►│ LoginServer  :20000  │  계정 생성/인증, 서버 목록, 서버 입장
        │           │   (inter)    :20002  │◄── GameServer 하트비트/ServerUp
        │           └──────────────────────┘
        │  GameMessage
        ▼
  ┌──────────────────────┐   GameMessage 그대로 중계   ┌──────────────────────┐
  │ GameServer  :20010   │ ─────────────────────────► │ DBServer    :20003   │──► SQLite
  │ (전투 계산만 수행)    │ ◄───────────────────────── │ (모든 CRUD)           │
  └──────────────────────┘                             └──────────────────────┘
```

| 모듈 | 상태 | 역할 |
|---|---|---|
| `Triangle-Common` | 사용 | `.proto` 원본 + protoc 2.5 생성 코드, 상수(`TriangleValues`), 설정(`TriangleConf`) |
| `Triangle-LoginServer` | 사용 | 계정(평문 비밀번호), 서버 목록, 게임 서버 하트비트 |
| `Triangle-GameServer` | 사용 | 클라이언트↔DB 중계 프록시 + **전투 시뮬레이션** |
| `Triangle-DBServer` | 사용 | SQLite CRUD 전담, 단일 스레드 큐 |
| `Triangle-DBTool` | 부분 | 테이블 리셋/ID 카운터 초기화 (대화형 `main`은 실제로 아무것도 안 함) |
| `Triangle-BattleServer` | **죽은 코드** | 전체 주석 처리. 전투는 GameServer로 이전됨 |
| `Triangle-Client` | 사용 | Android(Eclipse ADT, API 14/16), 가로 800×480 |
| `UnitManager` | 사용 | Swing 관리 툴. SQLite 파일을 직접 수정 (NPC 데이터 입력용) |
| `TriangleTester` | 사용 | JUnit4 통합 테스트 (실제 서버를 프로세스 안에서 띄움) |

## 기술 스택 (빌드 파일이 없어 코드로 추정)

- Java 7, Eclipse 프로젝트 (`.classpath`/`.project`는 gitignore)
- Netty **5.0.0 Alpha** (폐기된 API), protobuf-java 2.5.0 (proto2)
- SQLite (Xerial sqlite-jdbc), log4j 1.2, JUnit 4
- 소스 인코딩 혼재: Java 소스는 CP949/EUC-KR (`iconv -f cp949`로 읽어야 함)

## 규모

직접 작성한 코드 약 1만 줄 (protoc 생성 코드 약 3.6만 줄 제외). 이 중 실제 게임 규칙에 해당하는 전투 핵심 로직은 **약 300줄**이다.

## 저장소에 없는 것 (복원 불가)

- **`D:\Triangle\Skills.xml`** — 모든 액션의 비용·수치. 공식과 XML 스키마만 남아 있다 ([game-rules.md](game-rules.md#액션)).
- **SQLite DB 파일** — NPC 유닛/팀/전술 데이터. 시드 스크립트도 없다(UnitManager로 수동 입력했던 것으로 보임).
- 빌드 설정, 외부 라이브러리 jar (클라이언트 `bin/`의 protobuf jar 제외).

## 실행에 필요했던 환경 (참고용, 현재는 실행 불가에 가까움)

- `D:/Triangle/` 디렉터리 (DB 파일, `Skills.xml`, 로그)
- 모든 서버 주소가 공인 IP `61.33.38.198`로 하드코딩 (`TriangleConf.java`)
- 기동 순서: DBServer → LoginServer → GameServer. 각 서버는 콘솔에서 Enter를 누르면 종료.
