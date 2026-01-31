using System.Reflection;
using Aco228.WService.Attributes;
using Aco228.WService.Base;

namespace Aco228.WService.Extensions;

internal static class TypeExtensions
{
    public static ApiServiceDecoratorAttribute? FindServiceAttribute(this Type type)
    {
        if (type == typeof(IApiService))
            return null;

        var attribute = type.GetCustomAttribute<ApiServiceDecoratorAttribute>();
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