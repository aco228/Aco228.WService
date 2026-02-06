using System.Collections.Concurrent;
using System.Reflection;
using Aco228.WService.Attributes;
using Aco228.WService.Base;
using Aco228.WService.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace Aco228.WService;

public static class ServiceExtensions
{
    private static IHttpClientFactory? _httpClientFactory;
    private static ConcurrentDictionary<Type, HttpClient> _httpClientCache = new();
    
    public static void RegisterApiServices(
        this IServiceCollection services, 
        Assembly assembly)
    {
        var assemblyTypes = assembly.GetTypes();
        var defaultHttpClient = new HttpClient();
        
        foreach (var assemblyType in assemblyTypes)
        {
            if (!assemblyType.IsInterface)
                continue;
            
            if (!typeof(IApiService).IsAssignableFrom(assemblyType))
                continue;
            
            if (assemblyType == typeof(IApiService))
                continue;

            ApiServiceDependencyInjectionType injectionType = ApiServiceDependencyInjectionType.SINGLETON;
            var serviceConfiguration = assemblyType.GetCustomAttribute<ApiServiceDecoratorAttribute>();
            
            if (serviceConfiguration != null)
            {
                injectionType = serviceConfiguration.InjectionType;
            }

            Func<IServiceProvider, object>  implementationFactory = (provider =>
            {
                var httpClient = GetHttpClient(assemblyType, defaultHttpClient);
                var serviceByType = ApiServiceHelper.GetApiServiceByType(assemblyType, httpClient);
                if(serviceByType == null)
                    throw new InvalidOperationException($"Cannot find Create method for {assemblyType.Name}");
                
                // Console.WriteLine("Registered.WebService::" +  assemblyType.Name);
                return serviceByType;
            });

            switch (injectionType)
            {
                case ApiServiceDependencyInjectionType.SINGLETON:
                    services.AddSingleton(assemblyType, implementationFactory);
                    break;
                case ApiServiceDependencyInjectionType.SCOPED:
                    services.AddScoped(assemblyType, implementationFactory);
                    break;
                case ApiServiceDependencyInjectionType.TRANSIENT:
                    services.AddTransient(assemblyType, implementationFactory);
                    break;
                default:
                    throw new InvalidOperationException($"Cannot register {assemblyType.Name} for  {injectionType}");
            }
        }
    }

    private static HttpClient GetHttpClient(Type type, HttpClient defaultHttpClient)
    {
        var attr = GetApiServiceDecoratorAttribute(type);
        if (attr == null)
            return defaultHttpClient;
        
        if (_httpClientCache.TryGetValue(type, out var httpClient))
            return httpClient;

        var client = new HttpClient();
        _httpClientCache.TryAdd(type, client);
        return client;
    }

    private static ApiServiceDecoratorAttribute? GetApiServiceDecoratorAttribute(Type type)
    {
        var attr = type.GetCustomAttribute<ApiServiceDecoratorAttribute>();
        if (attr != null) return attr;
        foreach (var interfaceImpl in type.GetInterfaces())
        {
            var interfaceAttr = GetApiServiceDecoratorAttribute(interfaceImpl);
            if(interfaceAttr != null)
                return interfaceAttr;
        }

        return null;
    }
}