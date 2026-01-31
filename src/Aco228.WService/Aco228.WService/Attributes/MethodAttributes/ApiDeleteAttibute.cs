namespace Aco228.WService;

public class ApiDeleteAttibute : WebApiMethodAttribute
{
    public ApiDeleteAttibute(string url) : base(WebApiMethodType.DELETE,url)
    {
    }
}