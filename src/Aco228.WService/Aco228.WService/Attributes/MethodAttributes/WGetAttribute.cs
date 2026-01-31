namespace Aco228.WService;

public class WGetAttribute : WMethodAttribute
{
    public WGetAttribute(string url) : base(WMethodType.GET, url)
    {
    }
}