# Triangle

유닛을 육성하고, 유닛마다 "조건 → 행동" 전술을 짜서 팀을 꾸린 뒤 자동 전투로 승부하는 싱글플레이 RPG. MonoGame으로 개발 중.

2015년 Java/Android 프로토타입을 새로 만드는 프로젝트다. 기존 구현 분석은 [docs/legacy/](docs/legacy/README.md)에, 새 구조는 [docs/architecture.md](docs/architecture.md)에 있다.

## 구조

```
src/Triangle.Core/          게임 규칙 (MonoGame 의존 없음)
src/Triangle.Desktop/       MonoGame DesktopGL 실행 프로젝트
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
dotnet run --project src/Triangle.Desktop -- --screenshot shot.png      # 전투가 끝난 화면을 PNG로 저장하고 종료
dotnet run --project src/Triangle.Desktop -- --screenshot shot.png 40   # 로그 40줄까지 진행한 중간 화면
```

콘텐츠(`src/Triangle.Desktop/Content/Content.mgcb`)는 빌드할 때 함께 처리된다. 에디터는 `src/Triangle.Desktop`에서 `dotnet tool restore && dotnet mgcb-editor`로 연다.

## 라이선스

Apache License 2.0 — [LICENSE](LICENSE)

나눔고딕 글꼴은 SIL Open Font License 1.1을 따른다 — [src/Triangle.Desktop/Fonts/OFL.txt](src/Triangle.Desktop/Fonts/OFL.txt)
