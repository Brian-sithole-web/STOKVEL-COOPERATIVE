using System.Net;
using System.Text.Json;
using Stokvel.Domain;

namespace Stokvel.Api.Middleware;

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            var (status, code) = ex switch
            {
                BusinessRuleException br => (HttpStatusCode.BadRequest, br.Code),
                ForbiddenException => (HttpStatusCode.Forbidden, "FORBIDDEN"),
                NotFoundException => (HttpStatusCode.NotFound, "NOT_FOUND"),
                _ => (HttpStatusCode.InternalServerError, "ERROR")
            };

            context.Response.StatusCode = (int)status;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                code,
                message = ex.Message
            }));
        }
    }
}
