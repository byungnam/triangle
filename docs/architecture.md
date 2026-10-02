# Triangle 아키텍처 (MonoGame 리라이트)

## 결정 사항

| 항목 | 결정 | 날짜 |
|---|---|---|
| 플레이 형태 | 싱글플레이 (서버·계정·네트워크·DB 없음, 로컬 저장) | 2026-10-02 |
| 플랫폼 | 데스크톱 우선 (MonoGame DesktopGL: Windows/Linux/macOS) | 2026-10-02 |

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
│  │  ├─ Actions/               #   스킬 정의와 실행
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

- ATB: 레거시와 같은 방식이다. 행동한 유닛의 타이머에 `TIME / (계수 × speed)`를 더하고, 타이머가 가장 낮은 유닛이 다음에 행동한다. 동률은 시드 RNG나 고정 규칙으로 깬다.
- 전술: **정렬된 리스트**를 위에서부터 평가한다. 레거시에서 빠져 있던 **대상 선택자** 슬롯을 추가할지 검토한다.
- 상한에 도달했을 때의 결과(무승부/패배)는 규칙 데이터에서 설정한다.

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
- 전술 대상 슬롯 여부
- 직업과 스킬 목록, 밸런스 수치
- 1차 범위에 넣을 시스템 (효과, 경험치, 아이템, 진형 등)
- 테마 유지 여부
- UI 라이브러리
