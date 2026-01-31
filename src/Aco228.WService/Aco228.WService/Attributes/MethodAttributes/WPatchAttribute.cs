namespace Aco228.WService;

public class WPatchAttribute : WMethodAttribute
{
    public WPatchAttribute(string url) : base(WMethodType.PATCH, url)
    {
    }
}