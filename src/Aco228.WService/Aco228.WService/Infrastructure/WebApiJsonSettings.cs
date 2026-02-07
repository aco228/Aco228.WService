using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aco228.WService.Infrastructure;

public static class WebApiJsonSettings
{
    public static JsonSerializerOptions DefaultOptions { get; } = CreateDefaultOptions();
    public static JsonSerializerOptions SerializerOptions { get; } = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static JsonSerializerOptions CreateDefaultOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        options.AddJsonObjectPropertySupport();
        return options;
    }
}