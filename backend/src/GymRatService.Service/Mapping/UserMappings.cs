using GymRatService.Common.DTOs.Users;
using GymRatService.Repository.Entities;

namespace GymRatService.Service.Mapping;

internal static class UserMappings
{
    public static UserProfileDto ToProfileDto(this User user) => new(
        user.FirstName,
        user.LastName,
        user.Email,
        user.WeightUnit,
        user.DefaultRestTimeSeconds);
}
