namespace BuildingBlocks.Contracts;

/// <summary>
/// Deserialization mirror of <see cref="ApiResponse{T}"/> for reading downstream
/// internal (service-to-service) responses. Shared so aggregators and services that
/// call internal endpoints don't each redefine it.
/// </summary>
public record InternalEnvelope<T>(bool Success, T? Data, string? Error);
