using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;

internal class StringUrlHelper
{
    public static bool GetRequestUrl(
        string? baseUrl, 
        string? concatUrl,
        ParameterInfo[] parameters,
        object?[]? args,
        out string? result)
    {
        result = null;
        
        if (string.IsNullOrEmpty(baseUrl) || string.IsNullOrEmpty(concatUrl) || concatUrl.Length < 3)
            return false;

        concatUrl = concatUrl.Replace("//", "/");
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
        var visitedObjects = new HashSet<object>(ReferenceEqualityComparer.Instance);

        BuildQueryStringFromObjectInternal(obj, queryParams, visitedObjects, depth: 0, prefix: null);

        return queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "";
    }

    private static void BuildQueryStringFromObjectInternal(
        object obj,
        List<string> queryParams,
        HashSet<object> visitedObjects,
        int depth,
        string? prefix)
    {
        const int MaxDepth = 3; // Prevent excessive nesting in query strings

        if (depth > MaxDepth)
            return;

        // Circular reference detection
        if (obj.GetType().IsClass && obj is not string)
        {
            if (!visitedObjects.Add(obj))
                return; // Already visited, skip to prevent circular reference
        }

        PropertyInfo[] properties;
        try
        {
            properties = obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
        }
        catch
        {
            // GetProperties can fail for some types, skip them
            return;
        }

        foreach (var prop in properties)
        {
            // Skip indexed properties (they throw when calling GetValue without index)
            if (prop.GetIndexParameters().Length > 0)
                continue;

            object? value;
            try
            {
                value = prop.GetValue(obj);
            }
            catch
            {
                // Skip properties that throw on access
                continue;
            }

            if (value == null)
                continue;

            var propName = string.IsNullOrEmpty(prefix) ? prop.Name : $"{prefix}.{prop.Name}";

            // Handle different value types
            if (IsSimpleType(value.GetType()))
            {
                var formattedValue = FormatSimpleValue(value);
                if (formattedValue != null)
                {
                    queryParams.Add($"{propName}={Uri.EscapeDataString(formattedValue)}");
                }
            }
            else if (value is IEnumerable enumerable and not string)
            {
                // Handle collections (arrays, lists, etc.)
                foreach (var item in enumerable)
                {
                    if (item != null && IsSimpleType(item.GetType()))
                    {
                        var formattedValue = FormatSimpleValue(item);
                        if (formattedValue != null)
                        {
                            queryParams.Add($"{propName}={Uri.EscapeDataString(formattedValue)}");
                        }
                    }
                }
            }
            // Skip complex nested objects - query strings should only contain simple types
        }
    }

    private static bool IsSimpleType(Type type)
    {
        return type.IsPrimitive
               || type.IsEnum
               || type == typeof(string)
               || type == typeof(decimal)
               || type == typeof(DateTime)
               || type == typeof(DateTimeOffset)
               || type == typeof(TimeSpan)
               || type == typeof(Guid)
               || Nullable.GetUnderlyingType(type) != null && IsSimpleType(Nullable.GetUnderlyingType(type)!);
    }

    private static string? FormatSimpleValue(object value)
    {
        return value switch
        {
            bool boolValue => boolValue ? "true" : "false",
            DateTime dateTime => dateTime.ToString("o", System.Globalization.CultureInfo.InvariantCulture), // ISO 8601
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("o", System.Globalization.CultureInfo.InvariantCulture),
            TimeSpan timeSpan => timeSpan.ToString("c", System.Globalization.CultureInfo.InvariantCulture), // Constant format
            Guid guid => guid.ToString("D"), // Standard format with hyphens
            Enum enumValue => enumValue.ToString(),
            decimal decimalValue => decimalValue.ToString(System.Globalization.CultureInfo.InvariantCulture),
            double doubleValue => doubleValue.ToString(System.Globalization.CultureInfo.InvariantCulture),
            float floatValue => floatValue.ToString(System.Globalization.CultureInfo.InvariantCulture),
            _ => value.ToString()
        };
    }

    private class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        public static readonly ReferenceEqualityComparer Instance = new();

        public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);

        public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
    }
}