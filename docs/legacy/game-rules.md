# 게임 규칙 (레거시 구현 기준)

경로 약어: `GS/` = `Triangle-GameServer/src/Triangle/`, `TV` = `Triangle-Common/src/Triangle/TriangleValues/TriangleValues.java`.

리라이트 관점의 결론부터: 실제로 동작하던 규칙은 **ATB 행동 순서 + 갬빗 전술 + 단일 대상 액션 + 방어력 경감 공식**뿐이다.
효과(버프/디버프), 보상/경험치, 진형, 스킬 레벨, 특성(Speciality), 직업별 차이는 설계 흔적만 있고 구현되지 않았다.

---

## 유닛

### 직업 (`UnitClass`, TV:172-206)

| 값 | 이름 | 비고 |
|---|---|---|
| 0 | `BEGGINER` (오타) | 클라이언트에서 만든 유닛은 항상 이 값 (직업 선택 UI 주석 처리됨) |
| 1 | `SOLDIER` | |
| 2 | `MAGE` | |
| 3 | `MEDIC` | |
| 4 | `PRIEST` | |

- 클래스 구현은 `Soldier`, `Mage` 두 개뿐이고 스탯 공식이 동일하다.
- 전투 준비 시 **모든 유닛을 `new Soldier(...)`로 생성**하고, 생성자가 직업을 SOLDIER로 덮어쓴다 (`GS/GameServer/CombatTaskRunner.java:41-89`, `GS/Unit/Soldier.java:21`). 즉 전투에서는 전원이 Soldier다.
- 아이콘 리소스는 warrior / mage / archor(archer) / oracle. 주석 처리된 옛 목록에는 soldier / specialist / magician / shaman. **직업 구성은 끝까지 확정되지 않았다.**
- TV:241-280에 직업별 스킬 계획(전사·궁수·마법사·사제의 공격/회복/마나 스킬)이 주석으로 남아 있다.

### 스탯

기본 스탯 5종 (`GS/Unit/UnitSpec.java`):

| 필드 | UI 이름 | 생성 시 랜덤 범위 | 사용처 |
|---|---|---|---|
| `str` | 근력 | 10–16 | 물리 방어(def), 공격 보정 |
| `dex` | 민첩 | 10–16 | **사용처 없음** |
| `vital` | 체력 | 20–26 | 최대 HP |
| `intel` | 지능 | 20–26 | 마법 방어(mdef), 최대 MP, (클라이언트) 전술 슬롯 수 |
| `speed` | 신속함 | 10–16 | 행동 빈도 |

생성 시 level 1, point 0, exp 0 (`Triangle-DBServer/.../DataDB.java:421-428`). 직업과 무관하게 같은 범위.

파생 스탯 (`GS/Unit/Soldier.java:15-20`):

```
def   = str
mdef  = intel
maxHp = vital * 28
maxMp = intel * 10
(HP/MP는 가득 찬 상태로 시작)
```

- `ki`, `ammo`(기력/탄약) 필드가 있으나 항상 0.
- `hpDamageReduce`, `mpDamageReduce`(% 경감)는 설정되는 곳이 없어 항상 0.
- `level`, `exp`, `point`는 전투에 쓰이지 않는다.
- 경험치 테이블 `expTable = {200, 400, 800, …, 102400}` (200·2ⁿ, TV:169)이 있지만 미사용. 레벨업·포인트 획득 로직 없음.
- 클라이언트의 스탯 배분 화면은 남은 `point`를 +/-로 분배해 유닛 전체를 서버에 덮어쓴다 ([client.md](client.md#editunitstat)).
- 클라이언트 `UnitInfo`에 `전술 슬롯 수 = min(10, 3 + intel/15)` 계산이 있으나 아무 데서도 쓰지 않는다. 설계 의도로만 참고.

### 팀

- 팀 = 전투 단위. 유닛은 여러 팀에 동시에 속할 수 있다 (`Regiment` 다대다 테이블).
- 인원 제한, 진형, 위치 없음.
- 주석 처리된 조건 `COND_AT_FRONT`, `COND_NUM_ENEMYTEAM_AT_BEHIND_*` (`GS/Unit/UnitCombatable.java:354-364`)로 보아 **전열/후열**이 계획되었던 것으로 보인다.

---

## 전술 (갬빗 시스템)

`GS/Unit/Tactic/Tactic.java`. 유닛마다 전술 목록을 가진다.

| 필드 | 의미 |
|---|---|
| `priority` | 낮은 숫자가 먼저 평가됨 (`compareTo`: `this.priority - o.priority`) |
| `condition` | 아래 조건 표 |
| `value` | 조건 파라미터 (UI상 1–10) |
| `action` | 아래 액션 표 |
| `actionLevel` | 저장·전송되지만 **무시됨** (클라이언트는 항상 1을 보냄) |

동작: 우선순위 순으로 훑어서 **조건이 처음 참이 되는 전술의 액션**을 실행한다. 아무것도 참이 아니면 그 턴은 넘어간다.
**대상 지정 슬롯은 없다.** 대상은 액션마다 하드코딩되어 있다.

새 전술 기본값: `ALWAYS / 0 / BasicAttack`, priority = 개수+1.

### 조건 (TV:59-77)

`x` = value. UI 문구는 "x0%" 형식이다.

| # | 이름 | UI 문구 (의미) | 실제 구현 |
|---|---|---|---|
| 0 | ALWAYS | 항상 | true |
| 1 / 2 | HP_UPPER / LOWER_THAN | 자신 HP x0% 이상 / 이하 | `hp/maxHp >= / <= x/100.0` |
| 3 / 4 | MP_UPPER / LOWER_THAN | 자신 MP | 동일 |
| 5–8 | TEAM_HP/MP_UPPER/LOWER | 아군 "한 명이라도" | 살아있는 아군(자신 포함) 중 하나라도 해당하면 true |
| 9–12 | TEAM_MEAN_HP/MP_UPPER/LOWER | 아군 평균 | 생존 아군의 비율 평균 |
| 13 | AT_MOST | "적어도 x회" | **실제로는 "최대 x회"**: 전술별 카운터 `< x`이면 true 후 증가 |
| 14 | AFTER_TURN | 자신의 x번째 행동부터 | `elapsedTurn >= x` |
| 15 | BEFORE_TURN | x번째 행동까지 | `elapsedTurn <= x` |
| 16 | AT_TURN | x번째 행동에 | `==` |
| 17 | FOR_EACH_TURN | x번째 행동마다 | `elapsedTurn % x == 0` (x=0이면 예외) |

`elapsedTurn`은 해당 유닛의 행동 횟수(1부터 시작)이며, 전체 턴 수가 아니다.

> ⚠️ **스케일 불일치.** UI는 value 1–10을 "10%–100%"로 보여주지만 코드는 `/100`을 한다. 즉 UI의 5는 실제로 5%다. 의도는 x×10%였을 가능성이 높다.

---

## 전투

### 흐름

1. 클라이언트가 `CombatRequest{allyTeamNumber, enemyTeamNumber}`를 보낸다. 상대는 NPC 팀(클라이언트 UI상 "전장 선택").
2. DBServer가 양 팀의 유닛과 전술을 채워서 돌려준다.
3. GameServer가 이 응답을 가로채서 전투를 동기 실행하고 `CombatResult{combatResult: bool}`만 클라이언트에 보낸다.

### 행동 순서: ATB (`GS/GameServer/CombatTaskRunner.java`)

- 모든 유닛은 `timer = 0`으로 시작하며 timer 오름차순 우선순위 큐에 들어간다. 동률은 무작위라서 첫 행동 순서는 랜덤이다.
- 루프 1회 = **유닛 한 명의 행동 1회**:
  1. `unit = 큐의 head` (timer 최소)
  2. **startup**: 효과 재계산 (실제로는 효과가 없으므로 무의미)
  3. **main**: 전술 선택 → 액션 실행. 비용이 모자라면 "Not enough resource"로 턴 소모
  4. **end**: 모든 유닛 timer에서 행동한 유닛의 timer를 빼고, 행동한 유닛에 `TIME / (factor × speed)` 를 더함
     - `TIME = 1000` (TV:167)
     - factor: SOLDIER 1.10, MAGE 0.90, MEDIC 1.23, PRIEST 1.10, BEGGINER 1.00 (`UnitCombatable.java:194-219`). 전원이 Soldier이므로 실질적으로 항상 1.10
     - 예: speed 13 → +69. **speed가 높을수록 자주 행동한다.**
  5. **turnEnd**: HP ≤ 0인 유닛을 생존 목록과 큐에서 제거
  6. 한쪽 생존자가 0이면 종료
- 루프 상한 **100회 (개별 행동 100번, 라운드 아님)**. 8명이면 1인당 약 12회 행동.
- 결과: `result = 적 전멸 여부`. **상한 도달 시 플레이어 패배**이고 무승부는 없다.

### 피해/회복 계산 (`UnitCombatable.take`, :483-522)

액션은 `Give{hpMod, mpMod, kiMod, ammoMod, effect[]}` 묶음을 대상에게 전달한다. **음수는 피해, 양수는 회복**이다.

```
HP:  hpMod < 0 이면  실효 = hpMod / (1 + 0.02 × def)  × (1 - hpDamageReduce × 0.01)
     hpMod ≥ 0 이면  실효 = hpMod   (회복은 경감 없음)
MP:  같은 공식, def 대신 mdef / mpDamageReduce
```

방어력 1당 2%씩 쌍곡선형으로 경감된다 (def 50 → 피해 1/2).

### 비용 지불 (`payCost`, :445-468)

- hpCost와 mpCost를 각각 따로 검사하기 때문에, 하나만 감당할 수 있으면 그것만 차감된다(버그).
- kiCost, ammoCost는 무시된다.

### 결과/보상

- 전투 로그는 서버 `System.out`에만 남는다. 클라이언트에는 승패 bool 하나만 간다.
- 프로토콜에 `CombatReward{exp, item[]}`가 있으나 채우지 않는다. 경험치·아이템·결과 저장 모두 없음.

---

## 액션

액션 enum (TV:121-126). **enum 이름이 Skills.xml의 `skillName` 키로 쓰인다.**

| # | 이름 | UI | 대상 | hpMod 계산 |
|---|---|---|---|---|
| 0 | BasicAttack | 기본 공격 | 무작위 생존 적 1명 | `hpMod × (1 + 효과str × 0.05) + 기본str × 0.05` |
| 1 | FireArrow | 화시 | 무작위 생존 적 1명 | BasicAttack과 **완전히 동일** (복붙) |
| 2 | Artillery | 포대 사격 | 무작위 생존 적 1명 | 고정 hpMod |
| 3 | Heal | 치료 | 자신 포함 아군 중 **HP 절대값이 가장 낮은** 1명 (비율 아님) | 고정 hpMod |
| 4 | StrUp | 힘 증가 | 자신 | 고정 hpMod (버프는 효과 시스템 미구현으로 무효) |

- 모두 단일 대상이다. `numberOfTargets`, `targetable`은 파싱만 하거나 선언만 되어 있고 쓰이지 않는다.
- BasicAttack 공식의 문제: 효과가 없으면 배율이 항상 1이고, `+str×0.05`는 양수라서 (피해는 음수이므로) **str이 높을수록 피해가 미세하게 줄어든다.** 의도는 `hpMod × (1 + str×0.05)`였을 것이다.

### Skills.xml 스키마 (`GS/GameServer/DataInitializer.java`, `GS/Actions/ActionDescriptor.java`)

파일은 없고 구조만 복원 가능하다. `<skillData id=..>` 요소 하나가 스킬 하나다.

| 필드 | 사용 여부 |
|---|---|
| `skillName`, `toolTip` | 이름은 키로 사용 |
| `hpCost`, `mpCost`, `kiCost`, `ammoCost` | hp/mp만 사용 |
| `hpMod`, `mpMod`, `kiMod`, `ammoMod` | 사용 |
| `effect` (반복) | 문자열만 전달, 효과 미구현 |
| `castingDelay`, `parentSkill`, `afterEffect`, `duration` | 미사용 |
| `skillType` (ACTIVE/AURA/PASSIVE) | 미사용 |
| `numberOfTargets`, `targetable` | 미사용 |

`parentSkill`, `skillType`, `castingDelay`로 보아 **스킬 트리, 패시브/오라, 시전 지연**이 계획되어 있었다.

---

## 효과 (버프/디버프) — 미구현

- `take()`에서 효과를 생성하는 코드가 주석 처리되어 있어, 효과 목록은 항상 비어 있다.
- 구현된 클래스:
  - `Strength`: str + 기본 str (×2)
  - `Weak`: str −10
  - `Burn`, `CommonDefense`: 비어 있음. 주석으로 보아 피해량 배율(`hpMod *= x`)을 의도한 듯하다.
- `EffectDescriptor`는 abstract라 인스턴스화가 불가능하다. 선언된 필드: isBuff, isRemovable, isPreemptive, isModifier, isEoT(지속 피해), 각 스탯 mod, elapsedTime, extinctTime.
  → 지속시간, 해제 가능 여부, 선제 발동, 턴마다 적용(DoT)을 계획했던 것으로 보인다.
- `TV.Effect` enum (SLOW, QUICK, SILENCE, WOUNDED, POISONED, UNARMORED, HEAVYARMORED)은 별도의 미사용 목록이다. 상태이상 아이디어 참고용.

---

## 기타 미사용 정의 (아이디어 참고)

- `AttackType`: NON, ELECTROMAGNETIC, THERMAL, KINETIC, EXPLOSION — 속성 상성
- `BattleField`: "레벨1..4" — 난이도별 전장
- `StatusType`: MANA, HEALTH, KI, AMMO — 직업별 자원(마나/기력/탄약)
- 버려진 월드맵 화면(`fieldselect_bak.xml` + 로마/카르타고 지중해 지도) — **포에니 전쟁/고대 로마 테마**
