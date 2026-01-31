namespace Aco228.WService.Attributes;

public enum WebServiceDependencyInjectionType
{
    SINGLETON,
    TRANSIENT,
    SCOPED,
}

public class WebApiServiceDecoratorAttribute : Attribute
{
    public Type Type { get; set; }
    public WebServiceDependencyInjectionType InjectionType { get; set; } =  WebServiceDependencyInjectionType.SINGLETON;

    public WebApiServiceDecoratorAttribute() { }

    public WebApiServiceDecoratorAttribute(Type type)
    {
        Type = type;
    }
    
}