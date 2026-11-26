namespace Sirh.BuildingBlocks.Domain.Results;

public sealed record Error(string Code, string Message, ErrorType Type)
{
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.Failure);
    public static readonly Error NullValue = new("General.Null", "A null value was provided.", ErrorType.Validation);

    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);
    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);
    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);
    public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);
    public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);
    public static Error Locked(string code, string message) => new(code, message, ErrorType.Locked);
    public static Error UnprocessableEntity(string code, string message) => new(code, message, ErrorType.UnprocessableEntity);
    public static Error Failure(string code, string message) => new(code, message, ErrorType.Failure);
}
