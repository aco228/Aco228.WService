using System.Reflection;
using Aco228.WService.Attributes;
using Aco228.WService.Implementation;

namespace Aco228.WService.Extensions;

internal static class TypeExtensions
{
    public static WebApiServiceDecoratorAttribute? FindServiceAttribute(this Type type)
    {
        if (type == typeof(IWebApiService))
            return null;

        var attribute = type.GetCustomAttribute<WebApiServiceDecoratorAttribute>();
        if (attribute != null)
            return attribute;

        foreach (var inter in type.GetInterfaces())
        {
            var interAttr = FindServiceAttribute(inter);
            if (interAttr != null)
                return interAttr;
        }

        return null;
    }
}