namespace Aperture.Api.Infrastructure;
public sealed class ApiError(int status, string message) : Exception(message)
{
    public int Status { get; } = status;
}
