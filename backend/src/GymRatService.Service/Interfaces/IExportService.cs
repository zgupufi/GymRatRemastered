using GymRatService.Common.DTOs.Export;

namespace GymRatService.Service.Interfaces;

public interface IExportService
{
    Task<UserExportDto> GetExportAsync(Guid userId, CancellationToken cancellationToken);

    Task<IAsyncEnumerable<string>> GetSetsCsvLinesAsync(Guid userId, CancellationToken cancellationToken);
}
