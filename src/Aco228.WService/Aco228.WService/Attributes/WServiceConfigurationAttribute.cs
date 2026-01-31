namespace Aco228.WService.Attributes;

public enum WServiceDependencyInjectionType
{
    SINGLETON,
    TRANSIENT,
    SCOPED,
}

public class WServiceConfigurationAttribute : Attribute
{
    public Type Type { get; set; }
    public WServiceDependencyInjectionType InjectionType { get; set; } =  WServiceDependencyInjectionType.SINGLETON;

    public WServiceConfigurationAttribute() { }

    public WServiceConfigurationAttribute(Type type)
    {
        Type = type;
    }
    
}