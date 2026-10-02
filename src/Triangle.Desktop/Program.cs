// --screenshot <path> [줄 수]: 첫 화면을 PNG로 저장하고 종료한다 (확인용).
// 줄 수를 주면 로그를 그만큼만 진행한 중간 상태를, 없으면 전투가 끝난 상태를 찍는다.
var screenshotPath = args is ["--screenshot", var path, ..] ? path : null;
var screenshotLines = args is ["--screenshot", _, var n, ..] && int.TryParse(n, out var lines) ? lines : (int?)null;

using var game = new Triangle.Desktop.TriangleGame(screenshotPath, screenshotLines);
game.Run();
