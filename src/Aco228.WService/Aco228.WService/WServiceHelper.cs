using System.Reflection;
using Aco228.WService.Implementation;

namespace Aco228.WService;

public static class WServiceHelper
{
    private static HttpClient _httpClient = new();
    
    public static T GetWebService<T>() where T : class, IWService
    {
        var service = WApiService<T>.Create();
        service.Configure(_httpClient);
        
        return service as T;
    }

    public static object? GetWebServiceByType(Type type, HttpClient? httpClient = null)
    {
        // Validate that the type is an interface and implements IWService
        if (!type.IsInterface)
            throw new ArgumentException($"Type {type.Name} must be an interface", nameof(type));
        
        if (!typeof(IWService).IsAssignableFrom(type))
            throw new ArgumentException($"Type {type.Name} must implement IWService", nameof(type));
        
        // Create WApiService<T> type
        var proxyType = typeof(WApiService<>).MakeGenericType(type);
        
        // Get the Create method
        var createMethod = proxyType.GetMethod(
            nameof(WApiService<>.Create), 
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
            nameof(WApiService<>.Configure),
            BindingFlags.NonPublic | BindingFlags.Instance);
        
        if (configureMethod == null)
            throw new InvalidOperationException($"Cannot find Configure method for {type.Name}");
        
        // Call Configure
        configureMethod.Invoke(proxy, new object[] { httpClient ?? _httpClient });
        return proxy;
    }
}