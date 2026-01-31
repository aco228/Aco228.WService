namespace Aco228.WService;

public class WebApiMethodAttribute : Attribute
{
    public string Url { get; private set; }
    public WebApiMethodType Type { get; private set; }

    public WebApiMethodAttribute(WebApiMethodType methodType, string url)
    {
        Url = url;
        Type = methodType;
    }
}