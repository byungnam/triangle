# Triangle 아키텍처 (MonoGame 리라이트)

## 결정 사항

| 항목 | 결정 | 날짜 |
|---|---|---|
| 플레이 형태 | 싱글플레이 (서버·계정·네트워크·DB 없음, 로컬 저장) | 2026-10-02 |
| 플랫폼 | 데스크톱 우선 (MonoGame DesktopGL: Windows/Linux/macOS) | 2026-10-02 |
| 전술 구조 | 레거시와 같이 `우선순위 · 조건 · 값 · 행동`. **전술에는 대상 지정 칸이 없다.** | 2026-10-02 |
| 대상 선택 | **스킬마다 대상 우선순위 규칙을 가진다.** 플레이어가 특정 대상을 직접 지정할 수 없게 해서, 전위/후위 배치가 전략의 중심이 되도록 한다. | 2026-10-02 |
| 진형 | 전위/후위 2줄. 아래 [진형 규칙](#진형-규칙) 참고 | 2026-10-02 |

레거시 분석은 [legacy/](legacy/README.md)에 있다.

## 원칙

1. **게임 규칙은 MonoGame에 의존하지 않는다.** 전투, 전술, 육성 규칙은 순수 C# 라이브러리(`Triangle.Core`)에 둔다. 단위 테스트를 할 수 있고, 나중에 모바일이나 다른 표현 방식으로 옮기기도 쉬워진다.
2. **전투는 결정적이다.** 같은 입력과 같은 시드면 항상 같은 결과가 나온다. 전투 엔진은 화면을 직접 건드리지 않고 **이벤트 목록**만 출력한다. 화면은 그 이벤트를 재생한다. 텍스트 로그로 보여줄지 애니메이션으로 보여줄지는 나중에 바꿀 수 있다.
3. **데이터 주도.** 직업, 스킬, 적 팀, 밸런스 수치는 코드가 아니라 데이터 파일(JSON)에 둔다. 레거시의 `Skills.xml` + UnitManager 역할을 대신한다.
4. **ID는 문자열 키.** `"basic_attack"`, `"soldier"`처럼 쓰고, 레거시처럼 enum 순서값에 의존하지 않는다.

## 솔루션 구조

스캐폴딩 완료 (2026-10-02). 하위 폴더(Units/, Scenes/ 등)와 `data/`는 구현하면서 만든다.

```
triangle/
├─ Triangle.slnx
├─ Directory.Build.props        # 공통 설정 (net10.0, Nullable, ImplicitUsings)
├─ src/
│  ├─ Triangle.Core/            # 순수 C# 클래스 라이브러리 (MonoGame 참조 없음)
│  │  ├─ Units/                 #   Unit, Stats, 파생 스탯 계산
│  │  ├─ Tactics/               #   Tactic, Condition, TargetSelector, 평가기
│  │  ├─ Combat/                #   CombatSimulator, ATB 타임라인, CombatEvent
│  │  ├─ Skills/                #   스킬 정의 (효과, 비용, 대상 규칙)
│  │  ├─ Data/                  #   JSON 정의 로더 (직업, 스킬, 적 팀)
│  │  └─ Progress/              #   로스터, 팀 편성, 세이브 데이터 모델
│  └─ Triangle.Desktop/         # MonoGame DesktopGL 실행 프로젝트
│     ├─ Scenes/                #   타이틀, 로스터, 유닛 상세, 전술 편집, 전투, 결과
│     ├─ UI/                    #   공용 위젯/레이아웃
│     ├─ Rendering/             #   전투 이벤트 재생기
│     ├─ Content/               #   MonoGame Content Pipeline (폰트, 텍스처)
│     └─ Program.cs, TriangleGame.cs
├─ data/                        # (예정) 게임 데이터 JSON (빌드 시 출력 폴더로 복사)
│  ├─ classes.json
│  ├─ skills.json
│  └─ encounters.json
├─ tests/
│  └─ Triangle.Core.Tests/      # xUnit — 전투/전술 규칙 테스트
└─ docs/
```

### 의존 방향

```
Triangle.Desktop ──► Triangle.Core ◄── Triangle.Core.Tests
        │
        └──► MonoGame.Framework.DesktopGL
```

`Triangle.Core`는 아무것도 참조하지 않는다(기본 라이브러리 + System.Text.Json).

## 핵심 설계 스케치

### 전투

```csharp
var result = CombatSimulator.Run(allyTeam, enemyTeam, rules, seed);
// result.Outcome : Victory / Defeat / Draw
// result.Events  : IReadOnlyList<CombatEvent>
//   TurnStarted(unit), TacticChosen(unit, tactic), ActionUsed(unit, skill, targets),
//   Damaged(target, amount, hpAfter), Healed(...), Died(unit), CombatEnded(outcome)
```

- ATB: 레거시와 같은 방식이다. 행동한 유닛의 타이머에 `TIME × 100 / (직업 속도% × speed)`(버림)를 더하고, 타이머가 가장 낮은 유닛이 다음에 행동한다. 동률은 시드 RNG나 고정 규칙으로 깬다.
- 전술: **정렬된 리스트**를 위에서부터 평가한다. 처음 참이 되는 조건의 행동을 실행한다. 전술에는 대상 칸이 없다.
- 대상: 스킬 정의(데이터)에 대상 규칙이 있다. 예: "전위 우선 적", "후위 적 무작위", "HP 비율이 가장 낮은 아군", "자신". 진형(전위/후위)이 어떤 대상을 고를 수 있는지에 영향을 준다.
- 상한에 도달했을 때의 결과(무승부/패배)는 규칙 데이터에서 설정한다.

### 전투 엔진 구현 규칙 (2026-10-02, `Triangle.Core/Combat/CombatSimulator.cs`)

레거시에서 바꾸거나 새로 정한 부분이다. 전부 테스트로 고정되어 있다.

- **전술 선택**: 우선순위 순으로 훑어서 **조건이 참인 첫 전술**을 고른다(레거시와 같음).
  - 그 전술의 비용을 낼 수 없거나 대상이 없으면, 다음 전술로 넘어가지 않고 **턴을 잃는다**.
  - 조건이 참인 전술이 하나도 없어도 턴을 넘긴다.
  - 세 경우 모두 이유와 함께 `Waited` 이벤트로 기록된다.
- **조건 값**: HP/MP 조건의 값은 정수 백분율 그대로 저장한다(30 = 30%). UI에서 10% 단위로 보여줄지는 화면에서 정한다.
- **정수 연산만 사용**: 규칙 계산에는 실수(float/double)를 쓰지 않는다. 경계값 오차를 없애려는 것이다.
  - 비율 비교는 교차 곱셈으로 한다: `현재 × 100 ≥ 값 × 최대`.
  - 아군 평균 조건은 분수를 정확히 더해서 비교한다(BigInteger).
  - 계수는 모두 정수 백분율이다. 직업 속도 110 = 1.10배, 위력 증가 5%/스탯, 경감 2%/방어.
  - 반올림은 사사오입이고, 행동 간격만 버림이다(레거시와 같음).
  - 구현은 `Triangle.Core/Combat/Ratio.cs`에 있다.
- **`MaxUses`**: 최대 N회. 조건이 참으로 판정되어 선택된 횟수를 센다. 비용 부족이나 대상 없음으로 턴을 잃어도 1회로 센다(레거시와 같음).
- **비용**: MP는 전부 있어야 쓸 수 있다. HP 비용은 내고 나서도 살아남을 때만 쓸 수 있다.
- **위력**: `위력 × (100 + 스탯 × 5) / 100`. 물리는 Str, 마법과 회복은 Intel을 쓴다.
- **경감**: `× 100 / (100 + 방어 × 2)`. 물리는 Def(=Str), 마법은 MDef(=Intel)로 경감한다.
- **행동 상한**: 상한에 도달했을 때의 결과는 `CombatRules.ActionLimitOutcome`으로 정하며 기본값은 무승부다. 레거시에서는 패배였다.
- **동률 처리**: 첫 행동 순서는 시드 RNG로 정한다. 그 뒤로는 먼저 예약된 쪽이 먼저 행동한다.

### 진형 규칙

- 각 유닛은 **전위** 또는 **후위**에 선다.
- **엄호**: 적 단일 대상 공격의 대상이 후위이고, 그쪽 진영에 살아있는 전위가 있으면 **전위가 대신 맞는다**. 전위가 여럿이면 그중 무작위 1명이 맞는다.
  - 예외: 엄호 무시 스킬, 전체 공격 스킬.
  - 회복처럼 아군을 대상으로 하는 스킬에는 엄호가 적용되지 않는다.
- 전위가 전멸해도 후위는 그대로 후위에 남는다(밀려나오지 않음). 부활로 전위가 돌아올 수 있다.
- 스킬은 대상 줄을 제한할 수 있다(전위만 / 후위만). 엄호와는 별개 속성이다. 예를 들어 후위만 노리는 스킬에 엄호 무시가 없으면, 전위가 살아있는 동안에는 전위가 대신 맞는다.

### 스킬 대상 규칙

| 규칙 | 의미 |
|---|---|
| 자신 | 시전자 |
| 무작위 | 후보 중 무작위 |
| 전위 우선 | 후보 중 전위에서 무작위. 전위가 없으면 후위에서 |
| 후위 우선 | 후보 중 후위에서 무작위. 후위가 없으면 전위에서 |
| HP 비율 최저 | 후보 중 HP 비율이 가장 낮은 유닛 |

후보 = 살아있는 적(또는 아군) 중 줄 제한을 통과한 유닛. 범위는 단일 또는 전체다.

### 화면

- 간단한 씬 스택(`SceneManager.Push/Pop`)으로 관리한다.
- 레거시 화면 흐름을 기준으로 한다: 타이틀 → 로스터(팀/유닛) → 유닛 상세 → 스탯 배분 / 전술 편집 → 전투 준비 → 전투 재생 → 결과.
- UI 라이브러리: MonoGame에는 UI가 없다. 후보는 **Gum**(MonoGame 공식 튜토리얼에서 사용)과 Myra. 아니면 직접 만든다. 스캐폴딩 단계에서 정한다.

### 저장

- 로컬 JSON 세이브 파일. 위치는 OS별 사용자 데이터 폴더(`Environment.SpecialFolder.ApplicationData/Triangle/`).
- 세이브 데이터에 스키마 버전 필드를 넣는다.

## 기술 스택

| 항목 | 선택 | 비고 |
|---|---|---|
| 언어/런타임 | C# / .NET 10 (LTS) | MonoGame 템플릿 기본값은 net9.0이었지만 net10.0으로 올렸고 빌드도 확인함 |
| 엔진 | MonoGame DesktopGL 3.8.5.1 | csproj는 `3.8.*`, MGCB 도구는 `.config/dotnet-tools.json`에서 3.8.5.1로 고정 |
| 테스트 | xUnit 2.9 | |
| 데이터 | System.Text.Json | (예정) |
| 콘텐츠 | MonoGame Content Builder (MGCB) | |

## 미결정 (→ [legacy/rewrite-notes.md](legacy/rewrite-notes.md#리라이트-전에-결정할-것))

- 전투 표현 (텍스트 로그 / 스프라이트 연출)
- 한 줄당 인원 / 팀 최대 인원
- 직업과 스킬 목록, 밸런스 수치
- 1차 범위에 넣을 시스템 (효과, 경험치, 아이템, 부활 등)
- 테마 유지 여부
- UI 라이브러리
