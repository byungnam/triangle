using Triangle.Desktop;

// 확인용 실행 옵션:
//   --combat                     마을 대신 시험 전투 화면으로 시작 (출전 명단 대 훈련 부대, 상태는 바꾸지 않는다)
//   --save <path>                세이브 파일 경로 (기본: OS별 사용자 설정 폴더의 Triangle/save.json)
//   --screenshot <path> [줄 수]  첫 화면을 PNG로 저장하고 종료.
//                                전투 화면이면 줄 수만큼 로그를 진행한 상태, 없으면 끝난 상태를 찍는다.
var screenshotPath = args.SkipWhile(a => a != "--screenshot").Skip(1).FirstOrDefault();
var screenshotLines = args.SkipWhile(a => a != "--screenshot").Skip(2).FirstOrDefault() is { } n && int.TryParse(n, out var lines)
    ? lines
    : (int?)null;

var savePath = args.SkipWhile(a => a != "--save").Skip(1).FirstOrDefault();

using var game = new TriangleGame(new LaunchOptions(screenshotPath, screenshotLines, args.Contains("--combat"), savePath));
game.Run();
