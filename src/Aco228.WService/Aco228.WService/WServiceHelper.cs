using System.Reflection;
using Aco228.WService.Implementation;

namespace Aco228.WService;

public static class WServiceHelper
{
    public static T GetWebService<T>(HttpClient httpClient) where T : class, IWebApiService
    {
        var service = WebApiServiceImplementation<T>.Create();
        service.Configure(httpClient);
        
        return service as T;
    }

    public static object GetWebServiceByType(Type type, HttpClient httpClient)
    {
        // Validate that the type is an interface and implements IWService
        if (!type.IsInterface)
            throw new ArgumentException($"Type {type.Name} must be an interface", nameof(type));
        
        if (!typeof(IWebApiService).IsAssignableFrom(type))
            throw new ArgumentException($"Type {type.Name} must implement IWService", nameof(type));
        
        // Create WApiService<T> type
        var proxyType = typeof(WebApiServiceImplementation<>).MakeGenericType(type);
        
        // Get the Create method
        var createMethod = proxyType.GetMethod(
            nameof(WebApiServiceImplementation<>.Create), 
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
            nameof(WebApiServiceImplementation<>.Configure),
            BindingFlags.NonPublic | BindingFlags.Instance);
        
        if (configureMethod == null)
            throw new InvalidOperationException($"Cannot find Configure method for {type.Name}");
        
        // Call Configure
        configureMethod.Invoke(proxy, new object[] { httpClient });
        return proxy;
    }
}