namespace Aco228.WService;

public class WPostAttribute : WMethodAttribute
{
    public WPostAttribute(string url) : base(WMethodType.POST, url)
    {
    }
}