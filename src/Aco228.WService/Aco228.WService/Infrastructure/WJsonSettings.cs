using System.Text.Json;

namespace Aco228.WService.Infrastructure;

public static class WJsonSettings
{
    public static JsonSerializerOptions DefaultOptions { get; } = CreateDefaultOptions();

    private static JsonSerializerOptions CreateDefaultOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        options.AddJsonObjectPropertySupport();
        return options;
    }
}