using System.Reflection;
using Aco228.WService.Attributes;
using Aco228.WService.Implementation;
using Microsoft.Extensions.DependencyInjection;

namespace Aco228.WService;

public static class ServiceExtensions
{
    public static void RegisterWebServices(
        this IServiceCollection services, 
        Assembly assembly)
    {
        var assemblyTypes = assembly.GetTypes();
        foreach (var assemblyType in assemblyTypes)
        {
            if (!assemblyType.IsInterface)
                continue;
            
            if (!typeof(IWebApiService).IsAssignableFrom(assemblyType))
                continue;
            
            if (assemblyType == typeof(IWebApiService))
                continue;

            WebServiceDependencyInjectionType injectionType = WebServiceDependencyInjectionType.SINGLETON;
            var serviceConfiguration = assemblyType.GetCustomAttribute<WebApiServiceDecoratorAttribute>();
            
            if (serviceConfiguration != null)
            {
                injectionType = serviceConfiguration.InjectionType;
            }

            var httpClient = new HttpClient();
            Func<IServiceProvider, object>  implementationFactory = (provider =>
            {
                var serviceByType = WServiceHelper.GetWebServiceByType(assemblyType, httpClient);
                if(serviceByType == null)
                    throw new InvalidOperationException($"Cannot find Create method for {assemblyType.Name}");
                
                Console.WriteLine("Registered.WebService::" +  assemblyType.Name);
                return serviceByType;
            });

            switch (injectionType)
            {
                case WebServiceDependencyInjectionType.SINGLETON:
                    services.AddSingleton(assemblyType, implementationFactory);
                    break;
                case WebServiceDependencyInjectionType.SCOPED:
                    services.AddScoped(assemblyType, implementationFactory);
                    break;
                case WebServiceDependencyInjectionType.TRANSIENT:
                    services.AddTransient(assemblyType, implementationFactory);
                    break;
                default:
                    throw new InvalidOperationException($"Cannot register {assemblyType.Name} for  {injectionType}");
            }
        }
    }
}