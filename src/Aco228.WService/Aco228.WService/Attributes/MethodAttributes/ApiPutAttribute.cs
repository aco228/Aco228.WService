namespace Aco228.WService;

public class ApiPutAttribute : WebApiMethodAttribute
{
    public ApiPutAttribute(string url) : base(WebApiMethodType.PUT, url)
    {
    }
}