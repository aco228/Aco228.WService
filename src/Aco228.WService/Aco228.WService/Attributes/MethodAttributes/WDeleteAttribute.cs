namespace Aco228.WService;

public class WDeleteAttribute : WMethodAttribute
{
    public WDeleteAttribute(string url) : base(WMethodType.DELETE,url)
    {
    }
}