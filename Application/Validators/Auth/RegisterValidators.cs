namespace Application.Validators.Auth;

using Api.Users;
using Commands.Auth;

public interface IRegisterValidator
{
    Task<RegistrationError?> Validate(RegisterCommand command);
}

public class EmailAlreadyExistsValidator(IUsersRepository usersRepository) : IRegisterValidator
{
    public async Task<RegistrationError?> Validate(RegisterCommand command)
    {
        var exists = await usersRepository.UserExists(command.Email);
        return exists ? RegistrationError.AlreadyExists : null;
    }
}




