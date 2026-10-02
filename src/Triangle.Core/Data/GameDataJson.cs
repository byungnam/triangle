using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Triangle.Core.Data;

/// <summary>게임 데이터 JSON 직렬화 설정. 게임, 편집기, 테스트가 함께 쓴다.</summary>
public static class GameDataJson
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,

            // 한글 이름이 \uXXXX로 바뀌지 않게 한다 (편집기가 쓴 파일의 가독성, git diff).
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,

            // 오타나 잘못된 값을 조용히 무시하지 않는다.
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            RespectNullableAnnotations = true,
            RespectRequiredConstructorParameters = true,
        };
        options.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
