using System.Globalization;
using System.Reflection;
using Aco228.WService.Infrastructure;
using Aco228.WService.Models.Attributes.ParameterAttributes;

namespace Aco228.WService.Helpers;

internal static class HttpContentHelpers
{
    public static HttpContent? ExtractBodyContent(WebApiMethodType methodType, ParameterInfo[] parameters, object?[]? args)
    {
        if (methodType == WebApiMethodType.GET || methodType == WebApiMethodType.DELETE)
            return null;

        if (args == null || args.Length == 0)
            return new StringContent(string.Empty, System.Text.Encoding.UTF8, "application/json");

        var formParams = new List<(string Name, object Value)>();
        var bodyParams = new List<(string Name, object Value)>();

        ClassifyParameters(parameters, args, formParams, bodyParams);

        if (formParams.Count > 0)
            return BuildMultipartContent(formParams, bodyParams);

        return BuildJsonContent(bodyParams);
    }

    private static void ClassifyParameters(
        ParameterInfo[] parameters,
        object?[] args,
        List<(string Name, object Value)> formParams,
        List<(string Name, object Value)> bodyParams)
    {
        for (int i = 0; i < parameters.Length; i++)
        {
            var param = parameters[i];
            var paramType = param.ParameterType;
            var paramName = param.Name ?? $"param{i}";
            var arg = args[i];

            if (arg == null)
                continue;

            if (paramType == typeof(CancellationToken))
                continue;

            // Explicit [ApiToForm] → multipart form part
            if (param.GetCustomAttribute<ApiToFormAttribute>() != null)
            {
                formParams.Add((paramName, arg));
                continue;
            }

            // [ApiToQuery] → handled in URL building, skip here
            if (param.GetCustomAttribute<ApiToQueryAttribute>() != null)
                continue;

            // Skip value types and strings without explicit attribute (URL params)
            if (paramType.IsValueType || paramType == typeof(string))
                continue;

            // [ApiToBody] or unannotated class/record → JSON body
            bodyParams.Add((paramName, arg));
        }
    }

    private static bool IsFileType(Type type)
        => type == typeof(FileInfo)
        || type == typeof(byte[])
        || typeof(Stream).IsAssignableFrom(type);

    private static HttpContent BuildJsonContent(List<(string Name, object Value)> bodyParams)
    {
        if (bodyParams.Count == 0)
            return new StringContent(string.Empty, System.Text.Encoding.UTF8, "application/json");

        if (bodyParams.Count == 1)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(bodyParams[0].Value, WebApiJsonSettings.SerializerOptions);
            return new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        }

        var merged = new Dictionary<string, object?>();
        foreach (var (_, value) in bodyParams)
        {
            foreach (var prop in value.GetType().GetProperties())
                merged[prop.Name] = prop.GetValue(value);
        }

        var mergedJson = System.Text.Json.JsonSerializer.Serialize(merged, WebApiJsonSettings.SerializerOptions);
        return new StringContent(mergedJson, System.Text.Encoding.UTF8, "application/json");
    }

    private static HttpContent BuildMultipartContent(
        List<(string Name, object Value)> formParams,
        List<(string Name, object Value)> bodyParams)
    {
        var multipart = new MultipartFormDataContent();

        foreach (var (name, value) in formParams)
        {
            if (IsFileType(value.GetType()))
                AddFileContent(multipart, name, value);
            else
                AddFormField(multipart, name, value);
        }

        foreach (var (name, value) in bodyParams)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(value, WebApiJsonSettings.SerializerOptions);
            multipart.Add(new StringContent(json, System.Text.Encoding.UTF8, "application/json"), name);
        }

        return multipart;
    }

    private static void AddFileContent(MultipartFormDataContent multipart, string name, object value)
    {
        switch (value)
        {
            case FileInfo fileInfo:
                multipart.Add(new StreamContent(fileInfo.OpenRead()), name, fileInfo.Name);
                break;
            case Stream stream:
                multipart.Add(new StreamContent(stream), name, name);
                break;
            case byte[] bytes:
                multipart.Add(new ByteArrayContent(bytes), name, name);
                break;
        }
    }

    private static void AddFormField(MultipartFormDataContent multipart, string name, object value)
    {
        var stringValue = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
        multipart.Add(new StringContent(stringValue), name);
    }
}
