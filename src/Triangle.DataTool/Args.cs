namespace Triangle.DataTool;

/// <summary>명령줄: 위치 인자와 <c>--이름 값</c> 옵션. 값이 없는 플래그는 <see cref="Flags"/>에 있는 것만.</summary>
internal sealed class Args
{
    private static readonly HashSet<string> Flags = ["strict", "help"];

    private readonly Dictionary<string, string?> _options = [];

    public List<string> Positional { get; } = [];

    public static Args Parse(IReadOnlyList<string> args)
    {
        var parsed = new Args();
        for (var i = 0; i < args.Count; i++)
        {
            if (args[i].StartsWith("--", StringComparison.Ordinal))
            {
                var name = args[i][2..];
                var value = !Flags.Contains(name) && i + 1 < args.Count ? args[++i] : null;
                parsed._options[name] = value;
            }
            else
            {
                parsed.Positional.Add(args[i]);
            }
        }

        return parsed;
    }

    public bool Has(string name) => _options.ContainsKey(name);

    public string? Get(string name) => _options.GetValueOrDefault(name);
}
