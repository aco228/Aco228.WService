using System.Reflection;
using Aco228.WService.Attributes;
using Aco228.WService.Exceptions;
using Aco228.WService.Extensions;
using Aco228.WService.Implementation;
using Aco228.WService.Infrastructure;

namespace Aco228.WService;

public class WApiService<T> : DispatchProxy where T : IWService
{
    internal HttpClient HttpClient { get; set; }
    internal WServiceConf? ServiceConfiguration { get; private set; }
    internal Type ImplementationType { get; private set; }
    internal string BaseUrl => ServiceConfiguration?.BaseUrl ?? ""; 

    internal void Configure(HttpClient httpClient)
    {
        ImplementationType = typeof(T);
        var attribute = ImplementationType.FindServiceAttribute();
        if (attribute != null)
        {
            ServiceConfiguration = Activator.CreateInstance(attribute.Type) as WServiceConf;
            if (ServiceConfiguration == null)
                throw new InvalidOperationException();   
        }
        HttpClient = httpClient;
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
        => InvokeAsyncGeneric<IWService>(targetMethod, args);

    internal async Task<TResult?> InvokeAsyncGeneric<TResult>(MethodInfo? targetMethod, object?[]? args)
    {        
        var methodAttribute = targetMethod?.GetCustomAttribute<WMethodAttribute>();
        if(methodAttribute == null)
            throw new InvalidOperationException("Method must have WMethod attribute");

        if (!StringUrlExtensions.GetRequestUrl(BaseUrl, methodAttribute.Url, targetMethod!.GetParameters(), args, out var url))
            throw new InvalidOperationException($"Url in wrong format: {url}");
        
        CancellationToken cancellationToken = 
            (CancellationToken?)args?.FirstOrDefault(x => x?.GetType() == typeof(CancellationToken)) 
            ?? ServiceConfiguration?.CancellationToken 
            ?? CancellationToken.None;

        WMethodType methodType = methodAttribute.Type;
        
        var httpContent = HttpContentExtensions.ExtractBodyContent(methodType, targetMethod, args);
        ServiceConfiguration?.OnUrlCreated(methodType, url!, httpContent);
        
        var httpResponseMessage = await ExecuteCommand(methodType, url!, httpContent, cancellationToken);
        EnsureSuccessStatusCode(httpResponseMessage, url!, httpContent);
        httpResponseMessage.EnsureSuccessStatusCode();
        
        var stringResponse = await httpResponseMessage.Content.ReadAsStringAsync(cancellationToken);
        ServiceConfiguration?.OnStringReceived(methodType, url!, httpContent, stringResponse);

        if (typeof(TResult) == typeof(IWService))
            return default;
        
        if (typeof(TResult).IsPrimitive || typeof(TResult) == typeof(string) || typeof(TResult) == typeof(decimal))
            return (TResult)Convert.ChangeType(stringResponse, typeof(TResult));
        
        return System.Text.Json.JsonSerializer.Deserialize<TResult>(stringResponse, WJsonSettings.DefaultOptions) 
               ?? throw new InvalidOperationException("Deserialization returned null");
    }

    private Task<HttpResponseMessage> ExecuteCommand(WMethodType wMethodType, string url, HttpContent? content, CancellationToken cancellationToken)
    {
        if(!string.IsNullOrEmpty(ServiceConfiguration?.UserAgent))
            HttpClient.DefaultRequestHeaders.Add("User-Agent", ServiceConfiguration.UserAgent);
        
        switch (wMethodType)
        {
            case WMethodType.GET:
                return HttpClient.GetAsync(url, cancellationToken);
            case WMethodType.POST:
                return HttpClient.PostAsync(url, content, cancellationToken);
            case WMethodType.PUT:
                return HttpClient.PutAsync(url, content, cancellationToken);
            case WMethodType.DELETE:
                return HttpClient.DeleteAsync(url, cancellationToken);
            case WMethodType.PATCH:
                return HttpClient.PatchAsync(url, content, cancellationToken);
            default:
                throw new NotImplementedException($"{wMethodType} is not implemented");
        }
    }

    internal void EnsureSuccessStatusCode(HttpResponseMessage response, string url, HttpContent? request)
    {
        try
        {
            response.EnsureSuccessStatusCode();
        }
        catch (Exception exception)
        {
            OnException(new RequestException(exception, url, request, response));
        }
    }
    
    protected virtual void OnException(RequestException exception)
    {
        ServiceConfiguration?.OnException(exception);
        throw exception;
    }

    internal static WApiService<T> Create<T>() where T : IWService
    {
        var service = Create<T, WApiService<T>>() as WApiService<T>;
        return service;
    }
}