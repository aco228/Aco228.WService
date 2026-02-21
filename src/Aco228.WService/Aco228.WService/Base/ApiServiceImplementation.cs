using System.Collections.Concurrent;
using System.Reflection;
using Aco228.Common;
using Aco228.WService.Exceptions;
using Aco228.WService.Extensions;
using Aco228.WService.Helpers;
using Aco228.WService.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Aco228.WService.Base;

public class ApiServiceImplementation<T> : DispatchProxy where T : IApiService
{
    private static MethodInfo? _invokeAsyncGenericBase;
    private static readonly ConcurrentDictionary<Type, MethodInfo> _genericMethodCache = new();
    private static readonly ConcurrentDictionary<MethodInfo, (WebApiMethodAttribute Attribute, ParameterInfo[] Parameters)> _methodMetadataCache = new();

    public HttpClient HttpClient { get; private set; }
    internal ApiServiceConf? ServiceConfiguration { get; private set; }
    internal Type ImplementationType { get; private set; }
    internal string BaseUrl => ServiceConfiguration?.BaseUrl ?? "";

    internal void Configure(HttpClient httpClient)
    {
        ImplementationType = typeof(T);
        var attribute = ImplementationType.FindServiceAttribute();
        if (attribute != null)
        {
            ServiceConfiguration = ServiceProviderHelper.ConstructByType(attribute.Type) as ApiServiceConf;
            if (ServiceConfiguration == null)
                throw new InvalidOperationException();
        }
        HttpClient = ServiceConfiguration?.InternalPrepare(httpClient) ?? httpClient;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod == null)
            throw new InvalidOperationException("targetMethod is null");

        // Handle synchronous property getters defined on IApiService
        if (targetMethod.ReturnType != typeof(Task) && !targetMethod.ReturnType.IsGenericType)
            return HandleSynchronousMember(targetMethod);

        var returnType = targetMethod.ReturnType;
        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
        {
            var resultType = returnType.GetGenericArguments()[0];
            var genericMethod = _genericMethodCache.GetOrAdd(resultType, type =>
            {
                _invokeAsyncGenericBase ??= typeof(ApiServiceImplementation<T>)
                    .GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
                    .First(m => m.Name == nameof(InvokeAsyncGeneric) && m.IsGenericMethodDefinition);

                return _invokeAsyncGenericBase.MakeGenericMethod(type);
            });

            return genericMethod.Invoke(this, new object?[] { targetMethod, args });
        }

        if (returnType == typeof(Task))
            return InvokeAsyncVoid(targetMethod, args);

        throw new NotSupportedException($"Return type '{returnType.Name}' is not supported. Only Task, Task<T>, and synchronous properties are supported.");
    }

    private object? HandleSynchronousMember(MethodInfo targetMethod)
    {
        // Property getters follow the convention "get_PropertyName"
        if (!targetMethod.Name.StartsWith("get_"))
            throw new NotSupportedException($"Synchronous method '{targetMethod.Name}' is not supported. Only Task and Task<T> return types are supported.");

        var propertyName = targetMethod.Name["get_".Length..];
        var property = typeof(ApiServiceImplementation<T>)
            .GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);

        if (property == null)
            throw new NotSupportedException($"Property '{propertyName}' is not exposed by {nameof(ApiServiceImplementation<T>)}.");

        return property.GetValue(this);
    }

    internal Task InvokeAsyncVoid(MethodInfo targetMethod, object?[]? args)
        => InvokeAsyncGeneric<IApiService>(targetMethod, args);

    internal async Task<TResult?> InvokeAsyncGeneric<TResult>(MethodInfo? targetMethod, object?[]? args)
    {
        var (methodAttribute, parameters) = _methodMetadataCache.GetOrAdd(targetMethod!, method =>
        {
            var attr = method.GetCustomAttribute<WebApiMethodAttribute>();
            var pars = method.GetParameters();
            return (attr!, pars);
        });

        if (methodAttribute == null)
            throw new InvalidOperationException("Method must have WMethod attribute");

        if (!StringUrlHelper.GetRequestUrl(BaseUrl, methodAttribute.Url, parameters, args, out var url))
            throw new InvalidOperationException($"Url in wrong format: {url}");

        CancellationToken cancellationToken =
            (CancellationToken?)args?.FirstOrDefault(x => x?.GetType() == typeof(CancellationToken))
            ?? ServiceConfiguration?.CancellationToken
            ?? CancellationToken.None;

        WebApiMethodType methodType = methodAttribute.Type;

        var httpContent = HttpContentHelpers.ExtractBodyContent(methodType, parameters, args);
        var httpContentString = httpContent is not MultipartFormDataContent && httpContent != null
            ? await httpContent.ReadAsStringAsync(cancellationToken)
            : string.Empty;

        ServiceConfiguration?.OnBeforeRequest(methodType, ref url!, ref httpContent, httpContentString);

        HttpResponseMessage httpResponseMessage = await ExecuteCommand(methodType, url!, httpContent, cancellationToken);

        var stringResponse = await httpResponseMessage.Content.ReadAsStringAsync(cancellationToken);

        EnsureSuccessStatusCode(httpResponseMessage, url!, httpContentString, stringResponse, httpContent);
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
        return webApiMethodType switch
        {
            WebApiMethodType.GET    => HttpClient.GetAsync(url, cancellationToken),
            WebApiMethodType.POST   => HttpClient.PostAsync(url, content, cancellationToken),
            WebApiMethodType.PUT    => HttpClient.PutAsync(url, content, cancellationToken),
            WebApiMethodType.DELETE => HttpClient.DeleteAsync(url, cancellationToken),
            WebApiMethodType.PATCH  => HttpClient.PatchAsync(url, content, cancellationToken),
            _ => throw new NotImplementedException($"{webApiMethodType} is not implemented")
        };
    }

    private void EnsureSuccessStatusCode(HttpResponseMessage response, string url, string? httpContentString, string? responseString, HttpContent? request)
    {
        try
        {
            response.EnsureSuccessStatusCode();
        }
        catch (Exception exception)
        {
            OnException(new WebApiRequestException(exception, url, request, httpContentString, responseString, response));
        }
    }

    private void OnException(WebApiRequestException exception)
    {
        ServiceConfiguration?.OnException(exception);
        throw exception;
    }

    private static bool IsPrimitiveOrSimpleType(Type type)
    {
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

        if (string.IsNullOrEmpty(stringResponse))
        {
            if (Nullable.GetUnderlyingType(targetType) != null || !targetType.IsValueType)
                return default;

            throw new InvalidOperationException($"Cannot convert empty response to non-nullable type '{targetType.Name}'");
        }

        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        try
        {
            if (underlyingType == typeof(bool))
            {
                if (bool.TryParse(stringResponse, out bool boolResult))
                    return (TResult)(object)boolResult;

                var normalized = stringResponse.Trim().ToLowerInvariant();
                if (normalized == "1" || normalized == "yes" || normalized == "y")
                    return (TResult)(object)true;
                if (normalized == "0" || normalized == "no" || normalized == "n")
                    return (TResult)(object)false;

                throw new FormatException($"Cannot convert '{stringResponse}' to boolean");
            }

            if (underlyingType == typeof(Guid))
                return (TResult)(object)Guid.Parse(stringResponse);

            if (underlyingType == typeof(DateTime))
                return (TResult)(object)DateTime.Parse(stringResponse, System.Globalization.CultureInfo.InvariantCulture);

            if (underlyingType == typeof(DateTimeOffset))
                return (TResult)(object)DateTimeOffset.Parse(stringResponse, System.Globalization.CultureInfo.InvariantCulture);

            if (underlyingType == typeof(TimeSpan))
                return (TResult)(object)TimeSpan.Parse(stringResponse, System.Globalization.CultureInfo.InvariantCulture);

            if (underlyingType == typeof(string))
                return (TResult)(object)stringResponse;

            return (TResult)Convert.ChangeType(stringResponse, underlyingType, System.Globalization.CultureInfo.InvariantCulture);
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