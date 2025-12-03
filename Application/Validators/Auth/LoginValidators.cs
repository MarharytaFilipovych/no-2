using Application.Api.Users;
using Application.Commands.Auth;

namespace Application.Validators.Auth;

public interface ILoginValidator
{
    Task<LoginError?> Validate(LoginCommand command);
}

public class UserCredentialsValidator(IUsersRepository usersRepository) : ILoginValidator
{
    public async Task<LoginError?> Validate(LoginCommand command)
    {
        var password = await usersRepository.GetUserPassword(command.Email);
        if (password == null)
            return LoginError.InvalidCredentials;

        if (!BCrypt.Net.BCrypt.Verify(command.Password, password))
            return LoginError.InvalidCredentials;

        return null;
    }
}

