# 서버 · 프로토콜 · 데이터 (레거시 구현 기준)

리라이트에서 서버 구조를 그대로 가져갈 일은 없겠지만, **어떤 데이터와 기능이 있었는지**를 파악하기 위한 기록이다.

## 서버 구성

| 포트 | 리스너 | 상대 |
|---|---|---|
| 20000 | LoginServer | 클라이언트 |
| 20002 | LoginServer (서버 간 통신) | GameServer |
| 20003 | DBServer | GameServer |
| 20010 | GameServer "One" | 클라이언트 |

- 주소는 전부 `61.33.38.198`로 하드코딩되어 있다 (`TriangleConf.java`).
- 기동 순서는 DBServer → LoginServer → GameServer.
- 스레드 구조:
  - 각 서버에 Netty boss/worker와 100스레드 풀이 있다(실제로 쓰는 건 1–3개).
  - DBServer는 단일 `DBTaskRunner`가 큐를 소비하면서 모든 SQL을 직렬화한다. 이것이 사실상 SQLite 동시성 제어 역할을 한다.
  - GameServer는 전투를 **Netty I/O 스레드에서 동기 실행**한다.

### GameServer는 프록시다

- 클라이언트가 접속하면 클라이언트마다 DBServer로 TCP 연결을 하나씩 열고, 모든 `GameMessage`를 그대로 전달한다 (`FrontendHandler.java`).
- DB 응답 중 `CombatRequest`만 가로채서 전투를 돌리고, 나머지는 클라이언트에 그대로 돌려준다 (`BackendHandler.java`).
- **검증 로직이 전혀 없다.** 모든 CRUD는 DBServer가 처리한다.

## 프로토콜

- protobuf 2 (proto2), varint32 길이 접두 프레이밍. 포트마다 엔벨로프 타입 하나.
- **요청과 응답이 같은 메시지다.** 서버가 받은 엔벨로프에 결과 필드를 채워서 되돌려준다.
- 요청-응답 상관 ID가 없어서 순서로만 매칭한다. `taskNumber`가 있지만 쓰이지 않는다.

### LoginProtocol (`LoginMessage`)

| 타입 | 필드 | 동작 |
|---|---|---|
| CreateAccount | id, password, success? | 계정 생성 |
| Authentication | id, password, accountNumber?, serverName[] | 성공 시 accountNumber ≥ 0. -1은 id/비밀번호 불일치, -3은 SQL 오류 |
| JoinServer | accountNumber, serverName, serverInformation? | 게임 서버 주소/포트 반환 |
| LeaveServer / Disconnect | accountNumber | 메모리상 위치 맵만 갱신 |
| CheckIdConflict | id, success? | true면 사용 가능 |
| DeleteAccount | id, password | **버그로 아무것도 지우지 않고 성공을 반환** |

### InterServerProtocol (`InterServerMessage`)

- `ServerUp{serverName}`: GameServer가 접속 시 1회 보낸다.
- `HeartBeat`: LoginServer가 1초마다 보내고 GameServer가 이름을 담아 응답한다. 5회 이상 누락되면 로그만 남기고 목록에서 제거하지 않는다.
- `Join{accountNumber}`: LoginServer가 보내지만 GameServer는 무시한다.

### GameProtocol (`GameMessage`)

엔벨로프 공통 필드: `gameMessageType`, `accountNumber` (required, **클라이언트가 직접 채움**), `result?`.

데이터 타입:
- `ProtocolUnit{unitNumber, unitName, cls, level, point, str, dex, intel, vital, speed, exp, tactics[], skills[], items[]}`
- `ProtocolTeam{teamNumber, teamName}`
- `ProtocolTactic{priority, condition, value, action, actionLevel}`
- `ProtocolSkill{skillNumber, skillLevel, exp?}`
- `ProtocolItem{itemNumber, quantity}`

| 범주 | 메시지 (id) |
|---|---|
| 유닛 | CreateUnit(11) `{name, cls}`, DeleteUnit(12), EditUnit(13) `{unitNumber, unit}` — 스탯 전체를 덮어씀, GetUnitByAccountNumber(14), GetUnitByTeamNumber(15), GetUnitByUnitNumber(16) |
| 팀 | CreateTeam(17), DeleteTeam(18), GetTeamByAccountNumber(19), GetTeamByUnitNumber(20) — 버그, EditTeam(21) — 이름만 |
| 전술 | CreateTactic(22), DeleteTactic(23), GetTactic(24), EditTactic(25) — 순서 변경 불가 |
| 편성 | RegisterUnit(26), UnregisterUnit(27) `{unitNumber, teamNumber}` |
| 전투 | CombatRequest(40) `{allyTeamNumber, enemyTeamNumber, allyUnit[], enemyUnit[]}`, CombatResult(41) `{combatResult?, combatReward?}`, GetNPCTeam(42) `{npcNumber, team[]}` |
| 미사용 | LearnSkill, getSkill, gainItem, dropItem, getItem (엔벨로프에 연결 안 됨) |

### 주요 흐름

1. **가입**: `CheckIdConflict` → `CreateAccount` (LoginServer).
2. **로그인**: `Authentication` → accountNumber와 서버 이름 목록을 받는다. 서버 목록은 `TriangleConf`의 정적 값이라 서버가 꺼져 있어도 보인다.
3. **서버 입장**: `JoinServer` → 주소/포트를 받는다. 클라이언트는 GameServer와 연결을 유지한다.
4. **관리**: 유닛/팀/전술 CRUD는 GameServer를 거쳐 DBServer로 전달된다.
5. **전투**: [game-rules.md](game-rules.md#전투) 참고.

## 데이터베이스 (SQLite)

- 파일: `jdbc:sqlite:/D:/Triangle/` 아래의 `Triangle.Account.db`, `Triangle.Data.db` (테스트용은 `Triangle.TestAccount.db`, `Triangle.TestData.db`).
- 스키마는 `Triangle-DBTool/.../ResetTables.java:165-289`에 있다. 컬럼은 전부 INT 또는 TEXT이고 외래키·인덱스는 없다.

| DB | 테이블 | 컬럼 |
|---|---|---|
| Account | `Account` | accountNumber PK, id UNIQUE, password (**평문**) |
| Account | `NextNumber` | nextAccountNumber (1행) |
| Account | `NPCAccount`, `NPCNextNumber` | NPC용 |
| Data | `Team` | teamNumber PK, accountNumber, teamname |
| Data | `Regiment` | unitNumber, teamNumber (PK 없음, 중복 허용) |
| Data | `Unit` | unitNumber PK, accountNumber, unitname, level, cls, str, dex, vital, intel, speed, point, exp |
| Data | `Tactic` | unitNumber, priority, condition, value, action, actionLevel. PK(unitNumber, priority) |
| Data | `Skill` | unitNumber, skillNumber, level, exp. PK(unitNumber, skillNumber) |
| Data | `Item` | accountNumber, itemNumber, quantity. 아이템은 **계정 소유** |
| Data | `NextNumber` | nextTeamNumber, nextUnitNumber (1행) |

- 모든 테이블에 `NPC` 접두어가 붙은 복제본이 있다(NPCTeam, NPCUnit, …). 같은 `DataDB` 클래스가 SQL 문자열에 접두어를 붙이는 방식으로 두 세트를 다룬다.
- 트리거가 하는 일:
  - 유닛/팀/계정을 추가하면 ID 카운터를 증가시킨다.
  - 유닛을 삭제하면 Tactic과 Regiment도 지운다(Skill은 남는다).
  - 팀을 삭제하면 Regiment도 지운다.
  - 전술을 추가·삭제·수정하면 priority를 다시 번호 매긴다.
  - 아이템 수량이 0이 되면 행을 삭제한다.
- **시드 데이터는 없다.** NPC는 UnitManager로 수동 입력한 것으로 보인다.
- `NoWorkbookException`/`NoSheetException`은 미사용이다. 예전에는 엑셀로 데이터를 관리했던 흔적으로 보인다.

## 테스트 (`TriangleTester`, JUnit 4)

모두 통합 테스트다. 실제 서버를 프로세스 안에서 띄우고 고정 포트·고정 IP로 통신한다. `D:/Triangle/`와 해당 IP가 필요해서 사실상 그 당시 개발 PC에서만 돌았다.

| 테스트 | 범위 |
|---|---|
| LoginServerTest | 가입, ID 중복, 인증 성공/실패, 삭제(버그가 있어도 통과함), join/leave |
| GameServerTest | 유닛/팀/전술 CRUD, 편성. `UtilityFunctions`로 DB 상태를 직접 확인 |
| CombatTest | 4 대 4 전투. **`result==true`만 확인**하고 전투 결과는 검증하지 않음 |
| DBServerTest | CreateUnit 왕복 |
| NettyTest | 단언 없음 |

## LoginServer 세션

- `UserPlacement: HashMap<accountNumber, serverName>`만 메모리에 둔다. 토큰은 없고 연결과 묶여 있지도 않다.
- 기동할 때 모든 id와 비밀번호를 INFO 로그로 출력한다 (`AccountDB.java:83-88`).
