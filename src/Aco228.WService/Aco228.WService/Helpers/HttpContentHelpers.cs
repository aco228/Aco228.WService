using System.Reflection;

namespace Aco228.WService.Helpers;

internal static class HttpContentHelpers
{
    public static HttpContent? ExtractBodyContent(WebApiMethodType methodType, MethodInfo method, object?[]? args)
    {
        // GET and DELETE don't have bodies
        if (methodType == WebApiMethodType.GET || methodType == WebApiMethodType.DELETE)
            return null;

        if (args == null || args.Length == 0)
            return new StringContent(string.Empty, System.Text.Encoding.UTF8, "application/json");

        var parameters = method.GetParameters();

        // Collect all class/record parameters (skip primitives, strings, CancellationToken)
        var bodyObjects = new List<object>();

        for (int i = 0; i < parameters.Length; i++)
        {
            var paramType = parameters[i].ParameterType;
            var arg = args[i];

            if (arg == null)
                continue;

            // Skip CancellationToken
            if (paramType == typeof(CancellationToken))
                continue;

            // Skip value types and strings (they should be in URL parameters)
            if (paramType.IsValueType || paramType == typeof(string))
                continue;

            // This is a class/record - add to body
            bodyObjects.Add(arg);
        }

        // No body objects found
        if (bodyObjects.Count == 0)
            return new StringContent(string.Empty, System.Text.Encoding.UTF8, "application/json");

        // Single object - serialize directly
        if (bodyObjects.Count == 1)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(bodyObjects[0]);
            return new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        }

        // Multiple objects - merge their properties into one JSON object
        var mergedObject = new Dictionary<string, object?>();

        foreach (var obj in bodyObjects)
        {
            var properties = obj.GetType().GetProperties();
            foreach (var prop in properties)
            {
                var value = prop.GetValue(obj);
                mergedObject[prop.Name] = value;
            }
        }

        var mergedJson = System.Text.Json.JsonSerializer.Serialize(mergedObject);
        return new StringContent(mergedJson, System.Text.Encoding.UTF8, "application/json");
    }
}