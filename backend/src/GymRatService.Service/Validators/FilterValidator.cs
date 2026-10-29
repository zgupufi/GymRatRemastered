using FluentValidation;
using GymRatService.Common.Filtering;

namespace GymRatService.Service.Validators;

public abstract class FilterValidator<TFilter, TSortField> : AbstractValidator<TFilter>
    where TFilter : Filter<TSortField>
    where TSortField : struct, Enum
{
    private const int MaximumSearchTermLength = 100;

    protected FilterValidator()
    {
        RuleFor(filter => filter.Search.Term)
            .MaximumLength(MaximumSearchTermLength);

        RuleFor(filter => filter.Sort.By)
            .IsInEnum();

        RuleFor(filter => filter.Sort.Direction)
            .IsInEnum();

        RuleFor(filter => filter.Pagination.Page)
            .GreaterThanOrEqualTo(1);

        RuleFor(filter => filter.Pagination.PageSize)
            .InclusiveBetween(1, PaginationFilter.MaximumPageSize);
    }
}
