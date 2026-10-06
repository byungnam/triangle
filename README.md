# Triangle

유닛을 육성하고, 유닛마다 "조건 → 행동" 전술을 짜서 팀을 꾸린 뒤 자동 전투로 승부하는 싱글플레이 RPG. MonoGame으로 개발 중.

2015년 Java/Android 프로토타입을 새로 만드는 프로젝트다. 기존 구현 분석은 [docs/legacy/](docs/legacy/README.md)에, 새 구조는 [docs/architecture.md](docs/architecture.md)에 있다.

## 구조

```
src/Triangle.Core/          게임 규칙 (MonoGame 의존 없음)
src/Triangle.Desktop/       MonoGame DesktopGL 실행 프로젝트
src/Triangle.DataTool/      게임 데이터 검사·조회·수정 명령줄 도구 (triangle-data)
tests/Triangle.Core.Tests/  xUnit 테스트
docs/                       설계 문서
```

## 요구 사항

- .NET SDK 10
- Linux: `libicu` (없으면 `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1`로 우회 가능)

## 빌드 · 실행 · 테스트

```bash
dotnet build
dotnet test
dotnet run --project src/Triangle.Desktop
dotnet run --project src/Triangle.Desktop -- --screenshot shot.png                # 전술 편집 화면을 PNG로 저장하고 종료
dotnet run --project src/Triangle.Desktop -- --combat --screenshot shot.png       # 전투가 끝난 화면
dotnet run --project src/Triangle.Desktop -- --combat --screenshot shot.png 40    # 로그 40줄까지 진행한 중간 화면
dotnet run --project src/Triangle.Desktop -- --save /tmp/test-save.json            # 세이브 위치 바꾸기 (기본: ~/.config/Triangle/save.json)
```

## 데이터 검사 · 조회 · 수정

`data/*.json`은 `triangle-data`로 고치거나, 직접 고친 뒤 검사를 돌린다. 오류(게임이 못 읽음)와 경고(못 쓰는 행동, 얻을 수 없는 아이템 등)를 모두 보여 준다.

```bash
dotnet run --project src/Triangle.DataTool -- validate                      # 오류 있으면 종료 코드 1, --strict면 경고도 1
dotnet run --project src/Triangle.DataTool -- list items --mastery sword    # 종류: masteries skills actions effects items encounters zones recipes recruits
dotnet run --project src/Triangle.DataTool -- list items --type 한손검 --tier 2
dotnet run --project src/Triangle.DataTool -- list actions --mastery fire   # 화염 계열 아이템이 주는 행동
dotnet run --project src/Triangle.DataTool -- show heal                     # 원본 JSON + 쓰는 곳
dotnet run --project src/Triangle.DataTool -- refs poisoned                 # 쓰는 곳만
```

수정 명령은 고친 뒤 검증을 통과해야 저장한다. 실패하면 아무 파일도 바꾸지 않는다. `--dry-run`은 바뀔 내용과 검증 결과만 보여 준다.

```bash
D="dotnet run --project src/Triangle.DataTool --"
$D set items old_sword price 45                                   # 값 바꾸기 (없는 속성은 만든다)
$D set items '*' price '*=1.2' --where tier=2 --dry-run           # 일괄 계산: *= /= += -= (정수는 반올림)
$D set encounters training units.training_soldier_1.stats.str 15  # 경로: 점으로 잇고, 배열 원소는 번호나 id
$D add items '*' actions execute --where type=양손검              # 배열에 넣기 (remove는 빼기)
$D unset actions fireball delay                                   # 속성을 지워 기본값으로
$D copy items old_sword rusty_sword                               # 복사 (원본 바로 뒤), 이어서 set으로 고친다
$D new effects '{"id":"dazed","name":"멍함","kind":"Debuff"}' --after weakened
$D delete items rusty_sword                                       # 가리키는 곳이 남아 있으면 검증에서 막힌다
```

- `<id>`: 쉼표로 여럿, `*`는 아무 글자. 제작법은 결과 아이템 id로 고른다.
- `<값>`: JSON으로 읽히면 JSON(`50`, `true`, `["a"]`, `{"skillId":"archery","level":1}`), 아니면 문자열.
- `--where`: `=` `!=` `>=` `<=` `>` `<`, 여러 번 주면 모두 만족. 배열의 `=`는 "포함". 파일에 적힌 값만 본다.

콘텐츠(`src/Triangle.Desktop/Content/Content.mgcb`)는 빌드할 때 함께 처리된다. 에디터는 `src/Triangle.Desktop`에서 `dotnet tool restore && dotnet mgcb-editor`로 연다.

## 라이선스

Apache License 2.0 — [LICENSE](LICENSE)

나눔고딕 글꼴은 SIL Open Font License 1.1을 따른다 — [src/Triangle.Desktop/Fonts/OFL.txt](src/Triangle.Desktop/Fonts/OFL.txt)
