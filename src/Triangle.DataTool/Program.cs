using Triangle.Core.Data;
using Triangle.DataTool;

// 게임 데이터(data/*.json) 검사·조회 도구. 사용법은 Usage 참고.
// 종료 코드: 0 성공, 1 데이터 오류(또는 --strict에서 경고), 2 잘못된 명령.
const string Usage = """
    triangle-data <명령> [옵션]

    명령:
      validate [--strict]        로더 검증(오류)과 추가 검사(경고)를 돌린다. --strict면 경고도 실패로 본다.
      list <종류> [필터…]        종류: masteries skills actions effects items encounters zones recipes recruits
                                 필터: --search <글자> (id·이름·종류에 포함)
                                       items: --mastery <id> --slot <부위> --tier <n> --type <종류>
                                       actions: --mastery <id> (그 계열 아이템이 주거나 그 계열 무기 행동)
                                       skills: --mastery <id>
      show <id>                  그 id의 원본 JSON과 쓰는 곳.
      refs <id>                  그 id를 가리키는 곳만.

    공통 옵션:
      --data <폴더>              데이터 폴더 (기본: 현재 폴더에서 위로 올라가며 data/masteries.json을 찾는다)
    """;

Console.OutputEncoding = System.Text.Encoding.UTF8;

var options = Args.Parse(args);
if (options.Positional.Count == 0 || options.Has("help"))
{
    Console.WriteLine(Usage);
    return options.Positional.Count == 0 && !options.Has("help") ? 2 : 0;
}

var directory = options.Get("data") ?? DataDirectory.Find();
if (directory is null)
{
    Console.Error.WriteLine("데이터 폴더를 찾지 못했다. --data <폴더>로 알려 줘.");
    return 2;
}

GameData data;
try
{
    data = GameDataLoader.LoadDirectory(directory);
}
catch (GameDataException e)
{
    Console.WriteLine($"오류 {e.Errors.Count}개 ({directory}):");
    foreach (var error in e.Errors)
    {
        Console.WriteLine($"  - {error}");
    }

    return 1;
}

var command = options.Positional[0];
var rest = options.Positional.Skip(1).ToList();
switch (command)
{
    case "validate":
    {
        var warnings = DataLint.Check(data);
        Console.WriteLine($"오류 없음 ({directory})");
        if (warnings.Count > 0)
        {
            Console.WriteLine($"경고 {warnings.Count}개:");
            foreach (var warning in warnings)
            {
                Console.WriteLine($"  - {warning}");
            }
        }

        return options.Has("strict") && warnings.Count > 0 ? 1 : 0;
    }

    case "list" when rest.Count == 1:
    {
        List<string>? rows;
        try
        {
            rows = Listing.Rows(data, rest[0], options);
        }
        catch (Exception e) when (e is ArgumentException or FormatException)
        {
            Console.Error.WriteLine($"필터 값이 잘못됐다: {e.Message}");
            return 2;
        }

        if (rows is null)
        {
            Console.Error.WriteLine($"모르는 종류 '{rest[0]}'.");
            return 2;
        }

        foreach (var row in rows)
        {
            Console.WriteLine(row);
        }

        Console.WriteLine($"({rows.Count}개)");
        return 0;
    }

    case "show" when rest.Count == 1:
    {
        var sources = RawData.FindById(directory, rest[0]);
        if (sources.Count == 0)
        {
            Console.Error.WriteLine($"'{rest[0]}'을(를) 찾지 못했다.");
            return 1;
        }

        foreach (var (file, node) in sources)
        {
            Console.WriteLine($"[{file}]");
            Console.WriteLine(node.ToJsonString(GameDataJson.Options));
        }

        PrintRefs(data, rest[0]);
        return 0;
    }

    case "refs" when rest.Count == 1:
        PrintRefs(data, rest[0]);
        return 0;

    default:
        Console.Error.WriteLine(Usage);
        return 2;
}

static void PrintRefs(GameData data, string id)
{
    var refs = DataReferences.Find(data, id);
    Console.WriteLine(refs.Count == 0 ? "쓰는 곳 없음" : $"쓰는 곳 {refs.Count}개:");
    foreach (var where in refs)
    {
        Console.WriteLine($"  - {where}");
    }
}
