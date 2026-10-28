using GymRatService.Common.DTOs.Users;
using GymRatService.Common.Errors;
using GymRatService.Repository.Interfaces;
using GymRatService.Service.Exceptions;
using GymRatService.Service.Interfaces;
using GymRatService.Service.Mapping;
using Microsoft.Extensions.Localization;

namespace GymRatService.Service.Services;

public class UsersService(
    IUsersRepository usersRepository,
    IStringLocalizer<UsersService> localizer) : IUsersService
{
    public async Task<UserProfileDto> GetProfileAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await usersRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new ServiceException(ErrorCodes.UserNotFound, localizer["UserNotFound"]);

        return user.ToProfileDto();
    }
}
