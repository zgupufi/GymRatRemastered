using GymRatService.Common.DTOs.Users;
using GymRatService.Common.Errors;
using GymRatService.Repository.Interfaces;
using GymRatService.Service.Exceptions;
using GymRatService.Service.Interfaces;
using GymRatService.Service.Mapping;

namespace GymRatService.Service.Services;

public class UsersService(IUsersRepository usersRepository) : IUsersService
{
    public async Task<UserProfileDto> GetProfileAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await usersRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new BusinessLogicException(ErrorCodes.UserNotFound);

        return user.ToProfileDto();
    }
}
