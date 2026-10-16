using QuickHired.BOL;

namespace QuickHired.API.Interfaces.Services
{
    public interface ITokenService
    {
        string CreateToken(AppUsers user);
    }
}
