using System.Reflection;

namespace Aco228.WService.Helpers;

internal static class HttpContentHelpers
{
    public static HttpContent? ExtractBodyContent(WebApiMethodType methodType, MethodInfo method, object?[]? args)
    {
        if (methodType == WebApiMethodType.GET || methodType == WebApiMethodType.DELETE)
            return null;

        if (args == null || args.Length == 0)
            return new StringContent(string.Empty, System.Text.Encoding.UTF8, "application/json");

        var parameters = method.GetParameters();
        var fileParams = new List<(string Name, object Value)>();
        var bodyParams = new List<(string Name, object Value)>();

        ClassifyParameters(parameters, args, fileParams, bodyParams);

        if (fileParams.Count > 0)
            return BuildMultipartContent(fileParams, bodyParams);

        return BuildJsonContent(bodyParams);
    }

    private static void ClassifyParameters(
        ParameterInfo[] parameters,
        object?[] args,
        List<(string Name, object Value)> fileParams,
        List<(string Name, object Value)> bodyParams)
    {
        for (int i = 0; i < parameters.Length; i++)
        {
            var paramType = parameters[i].ParameterType;
            var paramName = parameters[i].Name ?? $"param{i}";
            var arg = args[i];

            if (arg == null)
                continue;

            if (paramType == typeof(CancellationToken))
                continue;

            if (paramType.IsValueType || paramType == typeof(string))
                continue;

            if (IsFileType(paramType))
                fileParams.Add((paramName, arg));
            else
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
            var json = System.Text.Json.JsonSerializer.Serialize(bodyParams[0].Value);
            return new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        }

        var merged = new Dictionary<string, object?>();
        foreach (var (_, value) in bodyParams)
        {
            foreach (var prop in value.GetType().GetProperties())
                merged[prop.Name] = prop.GetValue(value);
        }

        var mergedJson = System.Text.Json.JsonSerializer.Serialize(merged);
        return new StringContent(mergedJson, System.Text.Encoding.UTF8, "application/json");
    }

    private static HttpContent BuildMultipartContent(
        List<(string Name, object Value)> fileParams,
        List<(string Name, object Value)> bodyParams)
    {
        var multipart = new MultipartFormDataContent();

        foreach (var (name, value) in fileParams)
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

        foreach (var (name, value) in bodyParams)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(value);
            multipart.Add(new StringContent(json, System.Text.Encoding.UTF8, "application/json"), name);
        }

        return multipart;
    }
}
