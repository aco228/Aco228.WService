using System.Collections;
using System.Reflection;

internal class StringUrlExtensions
{
    public static bool GetRequestUrl(
        string? baseUrl, 
        string? concatUrl,
        ParameterInfo[] parameters,
        object?[]? args,
        out string? result)
    {
        result = null;
        
        if (string.IsNullOrEmpty(baseUrl) || string.IsNullOrEmpty(concatUrl))
            return false;
        
        if(baseUrl.EndsWith("/") && concatUrl.StartsWith("/"))
            concatUrl = concatUrl.Substring(1);
        
        if(!baseUrl.EndsWith("/") && !concatUrl.StartsWith("/"))
            concatUrl = "/" + concatUrl;
        
        result = $"{baseUrl}{concatUrl}";
        
        if (!result.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !result.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return false;
        
        if (result.Contains("{"))
        {
            for (int i = 0; i < parameters.Length; i++)
            {
                var paramName = parameters[i].Name;
                var paramType = parameters[i].ParameterType;
                var arg = args?[i];
                
                if (arg == null)
                    continue;
                
                var queryPlaceholder = $"?{{{paramName}}}";
                if (result.Contains(queryPlaceholder))
                {
                    var queryString = BuildQueryStringFromObject(arg);
                    result = result.Replace(queryPlaceholder, queryString);
                }
                else
                {
                    var placeholder = $"{{{paramName}}}";
                    if (result.Contains(placeholder))
                    {
                        if (paramType.IsClass && paramType != typeof(string))
                            continue;
                        
                        result = result.Replace(placeholder, Uri.EscapeDataString(arg.ToString() ?? ""));
                    }
                }
            }
        }
        
        if (!Uri.TryCreate(result, UriKind.Absolute, out Uri? uriResult))
            return false;
        
        if (uriResult.Scheme != Uri.UriSchemeHttp && 
            uriResult.Scheme != Uri.UriSchemeHttps)
            return false;

        return true;
    }
    
    private static string BuildQueryStringFromObject(object obj)
    {
        var queryParams = new List<string>();
        var properties = obj.GetType().GetProperties();
        
        foreach (var prop in properties)
        {
            var value = prop.GetValue(obj);
            
            if (value == null)
                continue;
            
            var propName = prop.Name;
            
            if (value is string strValue)
            {
                queryParams.Add($"{propName}={Uri.EscapeDataString(strValue)}");
            }
            else if (value is IEnumerable enumerable and not string)
            {
                foreach (var item in enumerable)
                {
                    if (item != null)
                    {
                        queryParams.Add($"{propName}={Uri.EscapeDataString(item.ToString() ?? "")}");
                    }
                }
            }
            else if (value is bool boolValue)
            {
                queryParams.Add($"{propName}={boolValue.ToString().ToLower()}");
            }
            else
            {
                queryParams.Add($"{propName}={Uri.EscapeDataString(value.ToString() ?? "")}");
            }
        }
        
        return queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "";
    }
}