namespace FRC_Service.Application.Dtos;

public record PaginationDto<T>(
	T Data,
	int CurrentPage,
	int PageSize,
	int TotalRecords,
	int TotalPages);
