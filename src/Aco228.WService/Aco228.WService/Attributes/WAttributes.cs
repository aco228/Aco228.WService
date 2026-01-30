namespace Aco228.WService.Attributes;

public enum WMethodType
{
    GET,
    POST,
    PUT,
    DELETE,
    PATCH,
}


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

public class WGetAttribute : WMethodAttribute
{
    public WGetAttribute(string url) : base(WMethodType.GET, url)
    {
    }
}

public class WPostAttribute : WMethodAttribute
{
    public WPostAttribute(string url) : base(WMethodType.POST, url)
    {
    }
}

public class WPutAttribute : WMethodAttribute
{
    public WPutAttribute(string url) : base(WMethodType.PUT, url)
    {
    }
}

public class WPatchAttribute : WMethodAttribute
{
    public WPatchAttribute(string url) : base(WMethodType.PATCH, url)
    {
    }
}

public class WDeleteAttribute : WMethodAttribute
{
    public WDeleteAttribute(string url) : base(WMethodType.DELETE,url)
    {
    }
}