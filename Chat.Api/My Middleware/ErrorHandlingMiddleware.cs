
namespace Chat.Api.MyMiddleware;

public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        _logger.LogError(exception, "An unhandled exception occurred: {Message}", exception.Message);

        var response = new
        {
            statusCode = exception switch
            {
                UnauthorizedAccessException => 401,
                KeyNotFoundException => 404,
                InvalidOperationException => 400,
                _ => 500
            },
            message = exception switch
            {
                UnauthorizedAccessException => "Unauthorized access.",
                KeyNotFoundException => "Resource not found.",
                InvalidOperationException => "Invalid operation.",
                _ => "An unexpected error occurred."
            },
            details = context.RequestServices.GetService<IWebHostEnvironment>().IsDevelopment() ? exception.Message : null
        };

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = response.statusCode;
        await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response));
    }
}

public static class ErrorHandlingMiddlewareExtensions
{
    public static IApplicationBuilder ErrorHandlingMiddleware(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ErrorHandlingMiddleware>();
    }
}