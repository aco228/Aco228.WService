using System.Reflection;
using Aco228.WService.Attributes;
using Aco228.WService.Base;
using Aco228.WService.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace Aco228.WService;

public static class ServiceExtensions
{
    public static void RegisterApiServices(
        this IServiceCollection services, 
        Assembly assembly)
    {
        var assemblyTypes = assembly.GetTypes();
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

            var httpClient = new HttpClient();
            Func<IServiceProvider, object>  implementationFactory = (provider =>
            {
                var serviceByType = ApiServiceHelper.GetApiServiceByType(assemblyType, httpClient);
                if(serviceByType == null)
                    throw new InvalidOperationException($"Cannot find Create method for {assemblyType.Name}");
                
                Console.WriteLine("Registered.WebService::" +  assemblyType.Name);
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
}