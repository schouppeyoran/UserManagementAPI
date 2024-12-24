using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Text;
using System.Threading.Tasks;

public class RequestResponseLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestResponseLoggingMiddleware> _logger;

    public RequestResponseLoggingMiddleware(RequestDelegate next, ILogger<RequestResponseLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Log the request
        _logger.LogInformation(await FormatRequest(context.Request));

        // Copy a pointer to the original response body stream
        var originalBodyStream = context.Response.Body;

        // Create a new memory stream to hold the response
        using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        // Continue down the middleware pipeline
        await _next(context);

        // Log the response
        _logger.LogInformation(await FormatResponse(context.Response));

        // Copy the contents of the new memory stream (which contains the response) to the original stream
        await responseBody.CopyToAsync(originalBodyStream);
    }

    private async Task<string> FormatRequest(HttpRequest request)
    {
        var body = request.Body;

        // Allow the body to be read multiple times
        request.EnableBuffering();

        // Read the request stream
        var buffer = new byte[Convert.ToInt32(request.ContentLength)];
        await request.Body.ReadAsync(buffer, 0, buffer.Length);

        // Convert the byte array to a string
        var bodyAsText = Encoding.UTF8.GetString(buffer);

        // Reset the request body stream position so the next middleware can read it
        request.Body.Position = 0;

        return $"HTTP Request Information:{Environment.NewLine}" +
               $"Schema:{request.Scheme} {Environment.NewLine}" +
               $"Host: {request.Host} {Environment.NewLine}" +
               $"Path: {request.Path} {Environment.NewLine}" +
               $"QueryString: {request.QueryString} {Environment.NewLine}" +
               $"Request Body: {bodyAsText}";
    }

    private async Task<string> FormatResponse(HttpResponse response)
    {
        // Read the response stream
        response.Body.Seek(0, SeekOrigin.Begin);
        var text = await new StreamReader(response.Body).ReadToEndAsync();
        response.Body.Seek(0, SeekOrigin.Begin);

        return $"HTTP Response Information:{Environment.NewLine}" +
               $"StatusCode: {response.StatusCode} {Environment.NewLine}" +
               $"Response Body: {text}";
    }
}