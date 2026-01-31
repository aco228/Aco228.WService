namespace Aco228.WService.Models.Attributes.MethodAttributes;

public class ApiDeleteAttribute : WebApiMethodAttribute
{
    public ApiDeleteAttribute(string url) : base(WebApiMethodType.DELETE,url)
    {
    }
}