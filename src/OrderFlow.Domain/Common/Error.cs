namespace OrderFlow.Domain.Common;

public sealed record CustomError(string Code, string Message)
{
    public static readonly CustomError None = new(string.Empty, string.Empty);
}

public sealed class DomainException(CustomError error) : Exception(error.Message)
{
    public CustomError Error { get; } = error;
}
