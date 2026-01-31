namespace Aco228.WService;

public class ApiPatchAttribute : WebApiMethodAttribute
{
    public ApiPatchAttribute(string url) : base(WebApiMethodType.PATCH, url)
    {
    }
}