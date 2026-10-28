using GymRatService.Common.DTOs.Auth;
using GymRatService.Repository.Entities;

namespace GymRatService.Service.Security;

public interface ITokenGenerator
{
    LoginResponse Generate(User user);
}
