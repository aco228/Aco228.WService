using System.Reflection;
using Aco228.WService.Exceptions;
using Aco228.WService.Extensions;
using Aco228.WService.Helpers;
using Aco228.WService.Infrastructure;

namespace Aco228.WService.Base;

public class ApiServiceImplementation<T> : DispatchProxy where T : IApiService
{
    internal HttpClient HttpClient { get; set; }
    internal ApiServiceConf? ServiceConfiguration { get; private set; }
    internal Type ImplementationType { get; private set; }
    internal string BaseUrl => ServiceConfiguration?.BaseUrl ?? ""; 

    internal void Configure(HttpClient httpClient)
    {
        ImplementationType = typeof(T);
        var attribute = ImplementationType.FindServiceAttribute();
        if (attribute != null)
        {
            ServiceConfiguration = Activator.CreateInstance(attribute.Type) as ApiServiceConf;
            if (ServiceConfiguration == null)
                throw new InvalidOperationException();   
        }
        HttpClient = httpClient;
        ServiceConfiguration?.InternalPrepare(HttpClient);
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if(targetMethod == null)
            throw new InvalidOperationException("targetMethod is null");
        
        var returnType = targetMethod.ReturnType;
        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
        {
            var resultType = returnType.GetGenericArguments()[0];
            var method = GetType()
                .GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(m => m.Name == nameof(InvokeAsyncGeneric) && m.IsGenericMethodDefinition);
    
            if (method == null)
                throw new InvalidOperationException("InvokeAsyncGeneric method not found!");
    
            var genericMethod = method.MakeGenericMethod(resultType);
            return genericMethod.Invoke(this, new object?[] { targetMethod, args });
        }

        if (returnType == typeof(Task))
            return InvokeAsyncVoid(targetMethod, args);
        
        throw new NotSupportedException("Only Task and Task<T> return types are supported");
    }

    internal Task InvokeAsyncVoid(MethodInfo targetMethod, object?[]? args)
        => InvokeAsyncGeneric<IApiService>(targetMethod, args);

    internal async Task<TResult?> InvokeAsyncGeneric<TResult>(MethodInfo? targetMethod, object?[]? args)
    {        
        var methodAttribute = targetMethod?.GetCustomAttribute<WebApiMethodAttribute>();
        if(methodAttribute == null)
            throw new InvalidOperationException("Method must have WMethod attribute");

        if (!StringUrlHelper.GetRequestUrl(BaseUrl, methodAttribute.Url, targetMethod!.GetParameters(), args, out var url))
            throw new InvalidOperationException($"Url in wrong format: {url}");
        
        
        CancellationToken cancellationToken = 
            (CancellationToken?)args?.FirstOrDefault(x => x?.GetType() == typeof(CancellationToken)) 
            ?? ServiceConfiguration?.CancellationToken 
            ?? CancellationToken.None;

        WebApiMethodType methodType = methodAttribute.Type;
        
        var httpContent = HttpContentHelpers.ExtractBodyContent(methodType, targetMethod, args);
        var httpContentString = httpContent != null ? await httpContent.ReadAsStringAsync(cancellationToken) : string.Empty;
        
        ServiceConfiguration?.OnBeforeRequest(methodType, ref url!, ref httpContent, httpContentString);
        
        HttpResponseMessage httpResponseMessage = await ExecuteCommand(methodType, url!, httpContent, cancellationToken);
        EnsureSuccessStatusCode(httpResponseMessage, url!, httpContentString, httpContent);
        
        var stringResponse = await httpResponseMessage.Content.ReadAsStringAsync(cancellationToken);
        ServiceConfiguration?.OnResponseReceived(methodType, url!, httpContent, httpContentString, httpResponseMessage, stringResponse);

        if (typeof(TResult) == typeof(IApiService))
            return default;
        
        if (IsPrimitiveOrSimpleType(typeof(TResult)))
            return ConvertPrimitiveType<TResult>(stringResponse);
        
        return System.Text.Json.JsonSerializer.Deserialize<TResult>(stringResponse, WebApiJsonSettings.DefaultOptions) 
               ?? throw new InvalidOperationException("Deserialization returned null");
    }

    private Task<HttpResponseMessage> ExecuteCommand(WebApiMethodType webApiMethodType, string url, HttpContent? content, CancellationToken cancellationToken)
    {
        switch (webApiMethodType)
        {
            case WebApiMethodType.GET:
                return HttpClient.GetAsync(url, cancellationToken);
            case WebApiMethodType.POST:
                return HttpClient.PostAsync(url, content, cancellationToken);
            case WebApiMethodType.PUT:
                return HttpClient.PutAsync(url, content, cancellationToken);
            case WebApiMethodType.DELETE:
                return HttpClient.DeleteAsync(url, cancellationToken);
            case WebApiMethodType.PATCH:
                return HttpClient.PatchAsync(url, content, cancellationToken);
            default:
                throw new NotImplementedException($"{webApiMethodType} is not implemented");
        }
    }

    private void EnsureSuccessStatusCode(HttpResponseMessage response, string url, string? httpContentString, HttpContent? request)
    {
        try
        {
            response.EnsureSuccessStatusCode();
        }
        catch (Exception exception)
        {
            OnException(new WebApiRequestException(exception, url, request, httpContentString, response));
        }
    }
    
    private void OnException(WebApiRequestException exception)
    {
        ServiceConfiguration?.OnException(exception);
        throw exception;
    }

    private static bool IsPrimitiveOrSimpleType(Type type)
    {
        // Get the underlying type if nullable
        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;

        return underlyingType.IsPrimitive
               || underlyingType == typeof(string)
               || underlyingType == typeof(decimal)
               || underlyingType == typeof(Guid)
               || underlyingType == typeof(DateTime)
               || underlyingType == typeof(DateTimeOffset)
               || underlyingType == typeof(TimeSpan);
    }

    private static TResult? ConvertPrimitiveType<TResult>(string? stringResponse)
    {
        var targetType = typeof(TResult);

        // Handle null or empty responses
        if (string.IsNullOrEmpty(stringResponse))
        {
            // If the type is nullable, return null
            if (Nullable.GetUnderlyingType(targetType) != null || !targetType.IsValueType)
                return default;

            throw new InvalidOperationException($"Cannot convert empty response to non-nullable type '{targetType.Name}'");
        }

        // Get the underlying type if nullable (e.g., int? -> int)
        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        try
        {
            // Special handling for common types
            if (underlyingType == typeof(bool))
            {
                // Support case-insensitive boolean parsing
                if (bool.TryParse(stringResponse, out bool boolResult))
                    return (TResult)(object)boolResult;

                // Support common boolean representations
                var normalized = stringResponse.Trim().ToLowerInvariant();
                if (normalized == "1" || normalized == "yes" || normalized == "y")
                    return (TResult)(object)true;
                if (normalized == "0" || normalized == "no" || normalized == "n")
                    return (TResult)(object)false;

                throw new FormatException($"Cannot convert '{stringResponse}' to boolean");
            }

            if (underlyingType == typeof(Guid))
            {
                return (TResult)(object)Guid.Parse(stringResponse);
            }

            if (underlyingType == typeof(DateTime))
            {
                return (TResult)(object)DateTime.Parse(stringResponse, System.Globalization.CultureInfo.InvariantCulture);
            }

            if (underlyingType == typeof(DateTimeOffset))
            {
                return (TResult)(object)DateTimeOffset.Parse(stringResponse, System.Globalization.CultureInfo.InvariantCulture);
            }

            if (underlyingType == typeof(TimeSpan))
            {
                return (TResult)(object)TimeSpan.Parse(stringResponse, System.Globalization.CultureInfo.InvariantCulture);
            }

            if (underlyingType == typeof(string))
            {
                return (TResult)(object)stringResponse;
            }

            // For numeric types and other primitives, use Convert.ChangeType
            var convertedValue = Convert.ChangeType(stringResponse, underlyingType, System.Globalization.CultureInfo.InvariantCulture);
            return (TResult)convertedValue;
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException(
                $"Failed to convert response '{stringResponse}' to type '{targetType.Name}': {ex.Message}", ex);
        }
        catch (OverflowException ex)
        {
            throw new InvalidOperationException(
                $"Value '{stringResponse}' is outside the valid range for type '{targetType.Name}': {ex.Message}", ex);
        }
    }

    internal static ApiServiceImplementation<T> Create()
    {
        var service = Create<T, ApiServiceImplementation<T>>() as ApiServiceImplementation<T>;
        return service;
    }
}