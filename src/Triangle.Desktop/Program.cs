using Triangle.Desktop;

// 창은 QHD(2560×1440)로 열리고 크기를 바꿀 수 있다. F11은 전체 화면 전환.
// 확인용 실행 옵션:
// 기본은 원정(판) 화면이다 (로그라이트 재편). 판 세이브는 회사 세이브와 같은 폴더의 run.json.
//   --combat                     시험 전투 화면으로 시작 (옛 출전 명단 대 훈련 부대, 상태는 바꾸지 않는다)
//   --summary                    --combat과 함께: 전투 화면의 요약 탭을 연다
//   --exploration                옛 탐사 기록 화면으로 시작
//   --scene <이름>               이 화면으로 시작: village(옛 마을), editor, editor:equipment, editor:stats, editor:picker,
//                                editor:preview, training, shop, infirmary,
//                                run:map, run:reward, run:shop, run:rest, run:event, run:editor, run:equipment, run:picker, run:end,
//                                run:party, run:unlocks
//                                (run:*은 임시 폴더의 시험 판이다)
//   --save <path>                옛 회사 세이브 경로 (기본: OS별 사용자 설정 폴더의 Triangle/save.json). 판 세이브는 같은 폴더에 둔다
//   --screenshot <path> [줄 수]  첫 화면을 QHD PNG로 저장하고 종료.
//                                전투 화면이면 줄 수만큼 로그를 진행한 상태, 없으면 끝난 상태를 찍는다.
var screenshotPath = args.SkipWhile(a => a != "--screenshot").Skip(1).FirstOrDefault();
var screenshotLines = args.SkipWhile(a => a != "--screenshot").Skip(2).FirstOrDefault() is { } n && int.TryParse(n, out var lines)
    ? lines
    : (int?)null;

var savePath = args.SkipWhile(a => a != "--save").Skip(1).FirstOrDefault();
var startScene = args.SkipWhile(a => a != "--scene").Skip(1).FirstOrDefault();

using var game = new TriangleGame(new LaunchOptions(
    screenshotPath, screenshotLines, args.Contains("--combat"), savePath, args.Contains("--exploration"), startScene,
    args.Contains("--summary")));
game.Run();
