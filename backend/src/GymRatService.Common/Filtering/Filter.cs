namespace GymRatService.Common.Filtering;

public class Filter<TSortField>
    where TSortField : struct, Enum
{
    public SearchFilter Search { get; set; } = new();

    public SortFilter<TSortField> Sort { get; set; } = new();

    public PaginationFilter Pagination { get; set; } = new();
}
