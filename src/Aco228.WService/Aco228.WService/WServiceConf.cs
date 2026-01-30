using Aco228.WService.Attributes;
using Aco228.WService.Exceptions;

namespace Aco228.WService;

public class WServiceConf
{
    public virtual string BaseUrl { get; }
    public virtual CancellationToken CancellationToken { get; }

    public virtual void OnUrlCreated(WMethodType methodType, string url, HttpContent? httpContent) { }
    public virtual void OnException(RequestException exception){}

}