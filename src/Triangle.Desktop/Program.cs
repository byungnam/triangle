using Triangle.Desktop;

// 확인용 실행 옵션:
//   --combat                     전술 편집 대신 전투 화면으로 시작
//   --screenshot <path> [줄 수]  첫 화면을 PNG로 저장하고 종료.
//                                전투 화면이면 줄 수만큼 로그를 진행한 상태, 없으면 끝난 상태를 찍는다.
var screenshotPath = args.SkipWhile(a => a != "--screenshot").Skip(1).FirstOrDefault();
var screenshotLines = args.SkipWhile(a => a != "--screenshot").Skip(2).FirstOrDefault() is { } n && int.TryParse(n, out var lines)
    ? lines
    : (int?)null;

using var game = new TriangleGame(new LaunchOptions(screenshotPath, screenshotLines, StartInCombat: args.Contains("--combat")));
game.Run();
