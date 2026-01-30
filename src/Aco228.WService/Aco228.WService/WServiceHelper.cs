using Aco228.WService.Implementation;

namespace Aco228.WService;

public static class WServiceHelper
{
    private static HttpClient _httpClient = new();
    
    public static T GetWebService<T>() where T : class, IWService
    {
        var service = WApiService<T>.Create<T>();
        service.Configure(_httpClient);
        
        return service as T;
    }
}