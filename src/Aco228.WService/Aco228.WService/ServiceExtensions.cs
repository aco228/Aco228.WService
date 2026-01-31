using System.Reflection;
using Aco228.WService.Attributes;
using Aco228.WService.Implementation;
using Microsoft.Extensions.DependencyInjection;

namespace Aco228.WService;

public static class ServiceExtensions
{
    public static void RegisterWebServices(
        this IServiceCollection services, 
        Assembly assembly,
        HttpClient? httpClient = null)
    {
        var assemblyTypes = assembly.GetTypes();
        foreach (var assemblyType in assemblyTypes)
        {
            if (!assemblyType.IsInterface)
                continue;
            
            if (!typeof(IWService).IsAssignableFrom(assemblyType))
                continue;
            
            if (assemblyType == typeof(IWService))
                continue;

            WServiceDependencyInjectionType injectionType = WServiceDependencyInjectionType.SINGLETON;
            var serviceConfiguration = assemblyType.GetCustomAttribute<WServiceConfigurationAttribute>();
            
            if (serviceConfiguration != null)
            {
                injectionType = serviceConfiguration.InjectionType;
            }

            Func<IServiceProvider, object>  implementationFactory = (provider =>
            {
                var client = httpClient ?? new HttpClient();
                var serviceByType = WServiceHelper.GetWebServiceByType(assemblyType, client);
                if(serviceByType == null)
                    throw new InvalidOperationException($"Cannot find Create method for {assemblyType.Name}");
                
                Console.WriteLine("Registered.WebService::" +  assemblyType.Name);
                return serviceByType;
            });

            switch (injectionType)
            {
                case WServiceDependencyInjectionType.SINGLETON:
                    services.AddSingleton(assemblyType, implementationFactory);
                    break;
                case WServiceDependencyInjectionType.SCOPED:
                    services.AddScoped(assemblyType, implementationFactory);
                    break;
                case WServiceDependencyInjectionType.TRANSIENT:
                    services.AddTransient(assemblyType, implementationFactory);
                    break;
                default:
                    throw new InvalidOperationException($"Cannot register {assemblyType.Name} for  {injectionType}");
            }
        }
    }
}