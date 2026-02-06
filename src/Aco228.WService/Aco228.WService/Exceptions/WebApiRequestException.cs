using System.Net;

namespace Aco228.WService.Exceptions;

public class WebApiRequestException : Exception
{
    public Exception Original { get; init; }
    public string Url { get; init; }
    public HttpContent? Request { get; init; }
    public string? RequestContent { get; init; }
    public string? ResponseContent { get; init; }
    public HttpResponseMessage Response { get; init; }

    public HttpRequestException HttpRequestException => Original as HttpRequestException;
    public HttpStatusCode? HttpStatusCode => HttpRequestException?.StatusCode;
        
    public WebApiRequestException(
        Exception original, 
        string url, 
        HttpContent? request,
        string? requestContent,
        string? responseContent,
        HttpResponseMessage responseMessage)
        : base(null, original)
    {
        Original = original;
        Url = url;
        Request = request;
        ResponseContent = responseContent;
        RequestContent = requestContent;
        Response = responseMessage;
    }

    public override string Message
    {
        get
        {
            var msg = 
                Environment.NewLine + Environment.NewLine + Environment.NewLine
                + $"URL = {Url}"
                + Environment.NewLine + Environment.NewLine + Environment.NewLine
                + $"REQUEST = {RequestContent}"
                + Environment.NewLine + Environment.NewLine + Environment.NewLine
                + $"RESPONSE = {ResponseContent}"
                + Environment.NewLine + Environment.NewLine + Environment.NewLine;
            return msg;
        }
    }
}