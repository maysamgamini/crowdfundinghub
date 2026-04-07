namespace CrowdFunding.Modules.Identity.Application.Abstractions.Services;

public interface IPasswordHasher
{
    string HashPassword(string password);
    bool VerifyPassword(string passwordHash, string password);
}
