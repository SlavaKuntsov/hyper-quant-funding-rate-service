namespace FRC_Service.Application.Dtos;

public record SymbolFundingRatesDto(
	string Symbol,
	List<ExchangeFundingRateDto> Exchanges);