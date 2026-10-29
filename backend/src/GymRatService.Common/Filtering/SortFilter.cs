using GymRatService.Common.Enums;

namespace GymRatService.Common.Filtering;

public class SortFilter<TField>
    where TField : struct, Enum
{
    public TField? By { get; set; }

    public SortDirection Direction { get; set; } = SortDirection.Ascending;
}
