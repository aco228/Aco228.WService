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
    public virtual ApiServiceDependencyInjectionType InjectionType { get; } =  ApiServiceDependencyInjectionType.SINGLETON;

    public ApiServiceDecoratorAttribute() { }

    public ApiServiceDecoratorAttribute(Type type)
    {
        Type = type;
    }
    
}