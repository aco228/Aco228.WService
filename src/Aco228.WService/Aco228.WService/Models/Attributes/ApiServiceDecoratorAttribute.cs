namespace Aco228.WService.Attributes;

public enum ApiServiceDependencyInjectionType
{
    SINGLETON,
    TRANSIENT,
    SCOPED,
}

public class ApiServiceDecoratorAttribute : Attribute
{
    public Type Type { get; set; }
    public ApiServiceDependencyInjectionType InjectionType { get; set; } =  ApiServiceDependencyInjectionType.SINGLETON;

    public ApiServiceDecoratorAttribute() { }

    public ApiServiceDecoratorAttribute(Type type)
    {
        Type = type;
    }
    
}