using Aco228.WService.Attributes;
using Aco228.WService.Exceptions;

namespace Aco228.WService;

public abstract class WServiceConf
{
    public abstract string BaseUrl { get; }
    public virtual string UserAgent { get; }
    public virtual CancellationToken CancellationToken { get; }


    internal void InternalPrepare(HttpClient httpClient)
    {
        if(!string.IsNullOrEmpty(UserAgent))
            httpClient.DefaultRequestHeaders.Add("User-Agent", UserAgent);
        
        Prepare(httpClient);
    }
    
    public virtual void Prepare(HttpClient httpClient) { }

    
    public virtual void OnBeforeRequest(WMethodType methodType, ref string url, ref HttpContent? httpContent, string? httpContentString) { }
    
    public virtual void OnException(RequestException exception){}
    public virtual void OnResponseReceived(WMethodType methodType, string url, HttpContent? httpContent, string? httpContentString, HttpResponseMessage response, string stringResponse) { }
    

}