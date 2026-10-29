using GymRatService.Common.DTOs.Split;

namespace GymRatService.Service.Interfaces;

public interface ISplitService
{
    Task<IReadOnlyList<SplitDayDto>> GetSplitAsync(Guid userId, CancellationToken cancellationToken);

    Task AssignSplitDayAsync(Guid userId, int day, AssignSplitDayRequest request, CancellationToken cancellationToken);
}
