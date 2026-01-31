using System.Reflection;
using Aco228.WService.Base;

namespace Aco228.WService.Helpers;

public static class ApiServiceHelper
{
    public static T GetApiService<T>(HttpClient httpClient) where T : class, IApiService
    {
        var service = ApiServiceImplementation<T>.Create();
        service.Configure(httpClient);
        
        return service as T;
    }

    public static object GetApiServiceByType(Type type, HttpClient httpClient)
    {
        // Validate that the type is an interface and implements IWService
        if (!type.IsInterface)
            throw new ArgumentException($"Type {type.Name} must be an interface", nameof(type));
        
        if (!typeof(IApiService).IsAssignableFrom(type))
            throw new ArgumentException($"Type {type.Name} must implement IWService", nameof(type));
        
        // Create WApiService<T> type
        var proxyType = typeof(ApiServiceImplementation<>).MakeGenericType(type);
        
        // Get the Create method
        var createMethod = proxyType.GetMethod(
            nameof(ApiServiceImplementation<>.Create), 
            BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public,
            null,
            Type.EmptyTypes,
            null);
        
        if (createMethod == null)
            throw new InvalidOperationException($"Cannot find Create method for {type.Name}");
        
        // Invoke Create()
        var proxy = createMethod.Invoke(null, null);
        
        if (proxy == null)
            throw new InvalidOperationException($"Failed to create proxy for {type.Name}");
        
        // Get the Configure method
        var configureMethod = proxyType.GetMethod(
            nameof(ApiServiceImplementation<>.Configure),
            BindingFlags.NonPublic | BindingFlags.Instance);
        
        if (configureMethod == null)
            throw new InvalidOperationException($"Cannot find Configure method for {type.Name}");
        
        // Call Configure
        configureMethod.Invoke(proxy, new object[] { httpClient });
        return proxy;
    }
}