namespace FRC_Service.Application.Dtos;

public sealed record FuturesDataDto(
	decimal FundingRate,
	DateTime FundingTime,
	int? FundingIntervalHours = null);