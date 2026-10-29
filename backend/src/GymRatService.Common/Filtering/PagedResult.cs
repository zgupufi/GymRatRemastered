namespace GymRatService.Common.Filtering;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public PagedResult<TResult> Map<TResult>(Func<T, TResult> map) => new([.. Items.Select(map)], Page, PageSize, TotalCount);
}
