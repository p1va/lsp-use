namespace LspUse.Application.Models;

public record ApplicationServiceError
{
    public required string Message { get; init; }
    public ErrorCode ErrorCode { get; init; } = ErrorCode.Unknown;
    public Exception? Exception { get; init; }
}
