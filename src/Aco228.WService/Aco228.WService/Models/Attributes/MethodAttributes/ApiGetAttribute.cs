namespace Aco228.WService.Models.Attributes.MethodAttributes;

public class ApiGetAttribute : WebApiMethodAttribute
{
    public ApiGetAttribute(string url) : base(WebApiMethodType.GET, url)
    {
    }
}