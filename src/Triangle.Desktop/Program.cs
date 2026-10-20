using Triangle.Desktop;

// 창은 QHD(2560×1440)로 열리고 크기를 바꿀 수 있다. F11은 전체 화면 전환. F8 음악 끄기, F9·F10 음량.
// 게임은 원정(판)으로 시작한다: 진행 중인 판이 있으면 이어 하고, 없으면 파티를 고른다.
// 실행 옵션:
//   --save <폴더>                판 세이브(run.json)와 기록(profile.json)을 둘 폴더 (기본: OS별 사용자 설정 폴더의 Triangle)
//   --scene run:<화면>           확인용 시험 판으로 시작 (임시 폴더를 쓰고 실제 세이브는 건드리지 않는다):
//                                map, reward, shop, rest, event, editor, equipment, picker, end, party, unlocks, combat
//   --summary                    run:combat과 함께: 전투 기록의 요약 탭을 연다
//   --screenshot <path> [줄 수]  첫 화면을 QHD PNG로 저장하고 종료.
//                                run:combat이면 줄 수만큼 로그를 진행한 상태, 없으면 끝난 상태를 찍는다.
var screenshotPath = args.SkipWhile(a => a != "--screenshot").Skip(1).FirstOrDefault();
var screenshotLines = args.SkipWhile(a => a != "--screenshot").Skip(2).FirstOrDefault() is { } n && int.TryParse(n, out var lines)
    ? lines
    : (int?)null;

var saveDirectory = args.SkipWhile(a => a != "--save").Skip(1).FirstOrDefault();
var startScene = args.SkipWhile(a => a != "--scene").Skip(1).FirstOrDefault();

using var game = new TriangleGame(new LaunchOptions(screenshotPath, screenshotLines, saveDirectory, startScene, args.Contains("--summary")));
game.Run();
