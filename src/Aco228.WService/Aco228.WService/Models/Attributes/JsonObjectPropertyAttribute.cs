namespace Aco228.WService.Attributes;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public class JsonObjectPropertyAttribute : Attribute
{
    public string Name { get; set; }

    public JsonObjectPropertyAttribute(string name)
    {
        Name = name;
    }
}