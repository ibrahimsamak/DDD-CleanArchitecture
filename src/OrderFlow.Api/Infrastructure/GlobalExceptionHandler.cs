// Infrastructure/GlobalExceptionHandler.cs
namespace OrderFlow.Api.Infrastructure;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderFlow.Application.Common.Exceptions;
using OrderFlow.Domain.Common;
using ValidationException = OrderFlow.Application.Common.Exceptions.ValidationException;

public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            ValidationException => (StatusCodes.Status400BadRequest, "Validation failed"),
            NotFoundException => (StatusCodes.Status404NotFound, "Resource not found"),
            DomainException => (StatusCodes.Status409Conflict, "Domain rule violated"),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Concurrent update conflict"),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred")
        };

        if (status >= 500)
        {
           // logger.LogError(ex, "Unhandled exception");
        }

        httpContext.Response.StatusCode = status;

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = status >= 500 ? "See server logs for details." : exception.Message,
            Type = $"https://httpstatuses.io/{status}"
        };

        // Field-keyed errors the client can bind to form inputs.
        if (exception is ValidationException ve)
        {
            problem.Extensions["errors"] = ve.Errors;
        }

        // A stable machine-readable code the client can branch on.
        if (exception is DomainException de)
        {
            problem.Extensions["code"] = de.Error.Code;
        }

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception
        });
    }
}
