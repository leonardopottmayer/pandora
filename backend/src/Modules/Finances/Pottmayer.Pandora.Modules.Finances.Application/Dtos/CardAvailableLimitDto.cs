namespace Pottmayer.Pandora.Modules.Finances.Application.Dtos;

public sealed record CardAvailableLimitDto(Guid CardId, decimal? CreditLimit, decimal? AvailableLimit);
