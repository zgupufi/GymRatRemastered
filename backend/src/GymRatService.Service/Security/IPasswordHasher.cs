namespace GymRatService.Service.Security;

public interface IPasswordHasher
{
    HashedPassword Hash(string password);

    bool Verify(string password, string hash, string salt);
}
