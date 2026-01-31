namespace Aco228.WService;

public class ApiGetAttribute : WebApiMethodAttribute
{
    public ApiGetAttribute(string url) : base(WebApiMethodType.GET, url)
    {
    }
}