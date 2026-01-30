using System;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

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
}