namespace SneakMart.Api.Dtos;

public record FieldError(string Field, string Message);

/// <summary>The single error shape returned for every 4xx/5xx response.</summary>
public record ErrorResponse(int Status, string Code, string Message, IReadOnlyList<FieldError>? Errors = null);

public class ApiException(int status, string code, string message, IReadOnlyList<FieldError>? errors = null) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
    public IReadOnlyList<FieldError>? Errors { get; } = errors;
}

public static class Naming
{
    public static string ToCamel(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        var parts = s.Split('.');
        return string.Join('.', parts.Select(p => p.Length == 0 ? p : char.ToLowerInvariant(p[0]) + p[1..]));
    }
}
