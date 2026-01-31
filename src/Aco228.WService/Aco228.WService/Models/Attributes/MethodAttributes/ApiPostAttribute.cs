namespace Aco228.WService;

public class ApiPostAttribute : WebApiMethodAttribute
{
    public ApiPostAttribute(string url) : base(WebApiMethodType.POST, url)
    {
    }
}