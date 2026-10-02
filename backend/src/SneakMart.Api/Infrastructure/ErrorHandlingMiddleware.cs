using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using SneakMart.Api.Dtos;

namespace SneakMart.Api.Infrastructure;

/// <summary>
/// Turns exceptions into the single documented error shape. Duplicate keys and CHECK/FK violations
/// that slip past service validation (e.g. race conditions) become clean 4xx responses, never tracebacks.
/// </summary>
public class ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> log)
{
    public async Task Invoke(HttpContext ctx)
    {
        try
        {
            await next(ctx);
        }
        catch (ApiException ex)
        {
            await Write(ctx, ex.Status, ex.Code, ex.Message, ex.Errors);
        }
        catch (DbUpdateException ex) when (MapDbError(ex) is not null)
        {
            var (status, code, message) = MapDbError(ex)!.Value;
            await Write(ctx, status, code, message, null);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Unhandled exception for {Method} {Path}", ctx.Request.Method, ctx.Request.Path);
            await Write(ctx, 500, "INTERNAL_ERROR", "An unexpected error occurred.", null);
        }
    }

    static (int Status, string Code, string Message)? MapDbError(DbUpdateException ex)
    {
        if (ex.InnerException is not MySqlException my) return null;
        var m = my.Message;
        return (int)my.ErrorCode switch
        {
            1062 when m.Contains("ux_skus_sku_code") => (409, "DUPLICATE_SKU_CODE", "A SKU with this code already exists."),
            1062 when m.Contains("ux_skus_combo") => (409, "DUPLICATE_SKU_COMBINATION", "This size already exists for this product/variant."),
            1062 when m.Contains("slug") => (409, "DUPLICATE_SLUG", "This slug is already in use."),
            1062 => (409, "DUPLICATE_VALUE", "A record with the same unique value already exists."),
            3819 => (422, "CONSTRAINT_VIOLATION", "A database constraint rejected the value."),
            1451 => (409, "IN_USE", "The record is referenced by other data and cannot be removed."),
            1452 => (422, "INVALID_REFERENCE", "A referenced record does not exist."),
            _ => null
        };
    }

    static async Task Write(HttpContext ctx, int status, string code, string message, IReadOnlyList<FieldError>? errors)
    {
        if (ctx.Response.HasStarted) return;
        ctx.Response.Clear();
        ctx.Response.StatusCode = status;
        await ctx.Response.WriteAsJsonAsync(new ErrorResponse(status, code, message, errors));
    }
}
