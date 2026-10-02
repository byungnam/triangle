# 클라이언트 · 관리 툴 · 리소스 (레거시 구현 기준)

## Android 클라이언트 (`Triangle-Client`)

- Eclipse ADT 프로젝트(Gradle 아님). minSdk 14, target 16, 권한은 INTERNET만 사용.
- 모든 화면이 가로 고정에 타이틀바가 없다. 아트는 800×480 기준이다.
- UI는 전부 Android 기본 위젯(ListView, GridView, TextView, 버튼, AlertDialog)으로 되어 있다.

### 화면 흐름

```
Enterance(스플래시 0.5초) ─► Login ─(신규 가입)─► CreateNewId ─► Login
                              │
                              └─(채널 선택 다이얼로그)─► UnitGrid (허브)
                                                          ├─► UnitInfo ─► EditUnitStat
                                                          │            ├─► EditUnitTactic
                                                          │            └─► 삭제
                                                          ├─► CreateNewUnit
                                                          └─► CombatPreparation ─► CombatResult (빈 화면)
```

| 화면 | 사용자가 보는 것/하는 것 | 메시지 |
|---|---|---|
| **Enterance** | 검은 화면에 한시 "三尺誓天 山河動色 / 一揮掃蕩 血染山河" (`splash.png`) | — |
| **Login** | 배경 `logo.png`, ID/PW, 로그인·신규 가입 버튼. 로그인 성공 후 "채널" 선택 | Authentication, JoinServer |
| **CreateNewId** | 이름, 비밀번호, 생성 버튼 | CreateAccount |
| **UnitGrid** | 상단 버튼(전투 / 새 팀 구성 / 팀 구성 변경 / 새 구성원). 왼쪽에 팀 목록("전체" 포함), 오른쪽에 유닛 그리드. 선택한 팀에 속하지 않은 유닛은 어둡게 표시 | GetTeam/GetUnit…, CreateTeam, Register/UnregisterUnit |
| **UnitInfo** | 이름, 직업, 레벨, 경험치, 5개 스탯. 버튼: 능력치 조정 / 전략 설정 / 삭제 | GetUnitByUnitNumber, DeleteUnit |
| **EditUnitStat** | 스탯 5줄과 +/- 버튼, 남은 포인트 표시 | EditUnit (유닛 전체를 전송) |
| **EditUnitTactic** | 전술 행 목록(우선순위·조건·값·행동을 색으로 구분). 칸을 누르면 선택 다이얼로그가 열리고 고르는 즉시 저장 | GetTactic, EditTactic |
| **CreateNewUnit** | 이름만 입력. 직업 선택은 주석 처리되어 cls는 항상 0 | CreateUnit |
| **CombatPreparation** | "전장 선택"(실제로는 NPC 계정 0의 팀 목록)과 "당신의 팀 선택", 실행 버튼 | GetNPCTeam, CombatRequest |
| **CombatResult** | 아레나 바닥 그림 + 고정된 아이콘 1개 + 빈 텍스트. **구현 안 됨** | — |

### 팀 편성 방식 (UnitGrid)

1. "팀 구성 변경" 버튼을 누르면 편성 모드로 들어간다.
2. 유닛을 탭할 때마다 소속을 토글한다.
3. 버튼을 다시 누르면 처음 상태와 비교해서, 바뀐 유닛마다 Register/Unregister를 보낸다.

### 전투 표현

**없다.** 레이아웃상 의도는 "위에는 아레나 그림과 유닛 아이콘, 아래에는 스크롤되는 텍스트 전투 로그"였던 것으로 보인다. 하지만 서버가 승패 bool만 보내기 때문에 로그를 표시할 데이터가 없었다.

### 네트워킹

- 로그인 서버와는 요청마다 소켓을 새로 연다. 게임 서버와는 연결 하나를 유지한다 (`Global.game_socket`).
- AsyncTask를 쓰지만 호출하는 쪽에서 `.get(1초)`로 **UI 스레드를 블로킹**한다.
- 타임아웃이 나면 늦게 도착한 응답이 다음 요청의 응답으로 읽혀서 스트림이 어긋난다.
- 세션 상태는 `Global.accountNumber` 하나뿐이다.

### 클라이언트 버그 (주요)

- **EditUnitStat**
  - 모든 +/- 버튼이 Str 라벨만 갱신한다.
  - Str의 + 버튼은 포인트를 확인하지 않는다.
  - 백그라운드 스레드에서 UI를 건드린다.
- **EditUnitTactic**
  - 값 다이얼로그가 저장하는 값(인덱스 0–9)과 화면에 보이는 값(1–10)이 하나씩 어긋난다.
  - priority를 0부터 시작하는 인덱스로 다루지만 DB는 1부터 시작한다.
  - 전술 추가·삭제·순서 변경 UI는 주석 처리되어 있다.
- **CreateNewId**: 결과 코드가 틀려서, 가입 후 Login 화면에 ID/PW가 자동으로 채워지지 않는다.
- **유닛 아이콘**: 직업과 관계없이 항상 `warrior.png`를 쓴다.
- **매니페스트**: 존재하지 않는 `UnitList`가 선언되어 있고, 실제로 있는 `CombatList`(CombatResult 복제본)는 빠져 있다.

## UnitManager (Swing 관리 툴)

- 1200×600 창 하나에 NPC 계정, 유저 계정, 팀, 유닛 목록, 유닛 전술 패널이 있다.
- 선택이 계정 → 팀 → 유닛 → 전술 순으로 이어진다.
- **DBServer를 거치지 않고 SQLite 파일을 직접 수정한다.** 서버가 돌고 있으면 충돌할 수 있다.
- 기능:
  - 계정 추가/삭제
  - 팀 추가/이름 변경/삭제
  - 유닛 추가(랜덤 스탯), 삭제, 셀에서 바로 편집(이름, 직업, 레벨, 스탯, 경험치)
  - 전술 추가/편집/삭제
  - 우클릭 메뉴로 팀에 넣기/빼기
- **NPC 데이터를 만들던 도구**다. 리라이트에서는 JSON 데이터 파일과 별도 편집기 도구로 대체한다.
- 버그가 많다:
  - 팀 이름 변경이 저장되지 않는다.
  - 전술 삭제가 동작하지 않는다.
  - "New unit" 메뉴 라벨에 오타("New nnit")가 있어서 메뉴가 동작하지 않는다.
  - 팝업 메뉴가 NPC 계정을 선택한 상태에서도 유저 DB를 수정한다.

## 아트 리소스

| 파일 | 내용 | 재사용 |
|---|---|---|
| `logo.png` 800×480 | 로마 전장 사진풍 (기병, 군기, 연기) | ✗ — 출처 불명, 영화 스틸로 보임 |
| `splash.png` | 한시 | △ — 텍스트는 원작이므로 다시 조판하면 됨 |
| `map.png` | 위성사진 지중해 지도에 ROME/CARTHAGO 손글씨 | ✗ — 테마 참고용 |
| `map2.png` | 픽셀아트 석조 아레나 바닥 | ✗ — 출처 불명 |
| `newheader*.png`, `newbody.png` | 가죽 모서리 종이 패널, 한/영 제목이 이미지에 박혀 있음 | ✗ — 스타일 참고만 |
| `in*.png`, `list*.png`, `but*.png`, `titlebar.png` | 바, 버튼 | ✗ |
| `warrior/mage/archor/oracle.png` 50×50 | 직업 아이콘 | ✗ — 상용 게임에서 가져온 것으로 의심됨 |

전반적으로 2000년대 중반 한국 웹게임풍이다. **출처가 확실하지 않아 리라이트에서는 전부 새로 만드는 것을 권장한다.**
가져갈 만한 것은 무드(고대 로마/포에니 전쟁, 종이·가죽 UI), 한시 문구, "아레나 + 로그" 레이아웃 아이디어 정도다.
