using GymRatService.Common.DTOs.Users;

namespace GymRatService.Service.Interfaces;

public interface IUsersService
{
    Task<UserProfileDto> GetProfileAsync(Guid userId, CancellationToken cancellationToken);
}
