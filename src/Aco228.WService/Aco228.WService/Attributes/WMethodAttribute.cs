namespace Aco228.WService;

public class WMethodAttribute : Attribute
{
    public string Url { get; private set; }
    public WMethodType Type { get; private set; }

    public WMethodAttribute(WMethodType methodType, string url)
    {
        Url = url;
        Type = methodType;
    }
}