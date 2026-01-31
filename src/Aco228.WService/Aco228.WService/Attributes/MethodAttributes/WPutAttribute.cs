namespace Aco228.WService;

public class WPutAttribute : WMethodAttribute
{
    public WPutAttribute(string url) : base(WMethodType.PUT, url)
    {
    }
}