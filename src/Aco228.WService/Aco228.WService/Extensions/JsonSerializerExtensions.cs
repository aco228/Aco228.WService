using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aco228.WService.Infrastructure;

public static class JsonSerializerExtensions
{
    /// <summary>
    /// Adds support for [JsonObjectProperty] attribute globally
    /// </summary>
    public static void AddJsonObjectPropertySupport(this JsonSerializerOptions options)
    {
        options.Converters.Add(new JsonObjectPropertyConverterFactory());
    }
}

public class JsonObjectPropertyConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        // Don't apply to generic types like List<T>, IEnumerable<T>, etc.
        if (typeToConvert.IsGenericType)
            return false;

        // Don't apply to built-in types
        if (typeToConvert.Namespace?.StartsWith("System") == true)
            return false;

        // Only apply to concrete classes
        return typeToConvert.IsClass && 
               typeToConvert != typeof(string) &&
               !typeToConvert.IsAbstract;
    }

    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var converterType = typeof(JsonObjectPropertyConverter<>).MakeGenericType(typeToConvert);
        return (JsonConverter?)Activator.CreateInstance(converterType);
    }

    private static readonly ConditionalWeakTable<JsonSerializerOptions, JsonSerializerOptions> _optionsWithoutFactory = new();

    /// <summary>
    /// Copy of options without this factory, so a converter can fall back to default serialization
    /// without picking itself again (infinite recursion).
    /// </summary>
    internal static JsonSerializerOptions WithoutFactory(JsonSerializerOptions options)
        => _optionsWithoutFactory.GetValue(options, static source =>
        {
            var copy = new JsonSerializerOptions(source);
            for (var i = copy.Converters.Count - 1; i >= 0; i--)
                if (copy.Converters[i] is JsonObjectPropertyConverterFactory)
                    copy.Converters.RemoveAt(i);
            return copy;
        });
}