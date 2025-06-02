using FRC_Service.Domain.Enums;

namespace FRC_Service.Application.Dtos;

public record FundingRateDto(
	string Symbol,
	decimal Rate,
	long TsRate,
	long FetchedAt);

public record ExchangeFundingRateDto(
	ExchangeCodeType ExchangeName,
	string Symbol,
	decimal Rate,
	long TsRate,
	long FetchedAt) : FundingRateDto(Symbol, Rate, TsRate, FetchedAt);