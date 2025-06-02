namespace FRC_Service.Application.Dtos;

public record FundingInfoSymbol(
	string SymbolName,
	int? FundingIntervalHours,
	DateTime? LaunchTime);