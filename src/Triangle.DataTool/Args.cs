namespace Triangle.DataTool;

/// <summary>명령줄: 위치 인자와 <c>--이름 값</c> 옵션(여러 번 줄 수 있다). 값이 없는 플래그는 <see cref="Flags"/>에 있는 것만.</summary>
internal sealed class Args
{
    private static readonly HashSet<string> Flags = ["strict", "help", "dry-run"];

    private readonly Dictionary<string, List<string?>> _options = [];

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
                if (!parsed._options.TryGetValue(name, out var values))
                {
                    parsed._options[name] = values = [];
                }

                values.Add(value);
            }
            else
            {
                parsed.Positional.Add(args[i]);
            }
        }

        return parsed;
    }

    public bool Has(string name) => _options.ContainsKey(name);

    /// <summary>마지막 값.</summary>
    public string? Get(string name) => _options.TryGetValue(name, out var values) ? values[^1] : null;

    /// <summary>여러 번 준 옵션의 값 전부 (예: --where).</summary>
    public IReadOnlyList<string> GetAll(string name) =>
        _options.TryGetValue(name, out var values) ? values.OfType<string>().ToList() : [];
}
