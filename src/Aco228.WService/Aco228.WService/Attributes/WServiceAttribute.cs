namespace Aco228.WService.Attributes;

public enum WServiceDependencyInjectionType
{
    SINGLETON,
    TRANSIENT,
    SCOPED,
}

public class WServiceAttribute : Attribute
{
    public Type Type { get; set; }
    public WServiceDependencyInjectionType InjectionType { get; set; } =  WServiceDependencyInjectionType.SINGLETON;
    
}