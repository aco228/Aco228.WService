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
    public ApiServiceDependencyInjectionType InjectionType { get; } =  ApiServiceDependencyInjectionType.SINGLETON;

    public ApiServiceDecoratorAttribute() { }

    public ApiServiceDecoratorAttribute(Type type)
    {
        Type = type;
    }
    
    public ApiServiceDecoratorAttribute(Type type, ApiServiceDependencyInjectionType injectionType)
    {
        InjectionType = injectionType;
        Type = type;
    }
    
}