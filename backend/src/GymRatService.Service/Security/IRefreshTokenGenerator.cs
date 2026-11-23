namespace GymRatService.Service.Security;

public interface IRefreshTokenGenerator
{
    GeneratedRefreshToken Generate();

    string Hash(string token);
}
