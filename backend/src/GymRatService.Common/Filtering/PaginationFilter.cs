namespace GymRatService.Common.Filtering;

public class PaginationFilter
{
    public const int DefaultPageSize = 20;

    public const int MaximumPageSize = 100;

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = DefaultPageSize;

    public int Skip => (Page - 1) * PageSize;
}
