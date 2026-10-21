using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Rag.Application.Common.Exceptions;
using Rag.Domain.Common;

namespace Rag.Api.Middleware;

internal sealed class ExceptionHandler(IProblemDetailsService problemDetails, ILogger<ExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            DomainException or ValidationException => (StatusCodes.Status400BadRequest, exception.Message),
            BadHttpRequestException e => (e.StatusCode, e.Message),
            UnauthorizedException => (StatusCodes.Status401Unauthorized, exception.Message),
            NotFoundException => (StatusCodes.Status404NotFound, exception.Message),
            ConflictException => (StatusCodes.Status409Conflict, exception.Message),
            ServiceUnavailableException => (StatusCodes.Status503ServiceUnavailable, exception.Message),
            HttpRequestException => (StatusCodes.Status503ServiceUnavailable, "Tashqi servis (embedding/LLM) javob bermadi."),
            _ => (StatusCodes.Status500InternalServerError, "Kutilmagan xato yuz berdi.")
        };

        if (status >= StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "So'rov xato bilan tugadi: {Status}", status);

        httpContext.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails { Status = status, Title = title },
            Exception = exception
        });
    }
}
