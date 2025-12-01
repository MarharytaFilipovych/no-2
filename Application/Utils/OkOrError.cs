namespace Application.Utils;

public readonly struct OkOrError<TError>
{
    private readonly TError _error;
    private readonly bool _isError;

    public bool IsOk => !_isError;
    public bool IsError => _isError;

    public TError GetError() => _isError ? _error : 
        throw new InvalidOperationException("No error available");

    private OkOrError(TError error, bool isError)
    {
        _error = error;
        _isError = isError;
    }

    public static OkOrError<TError> Ok() => new(default!, false);

    public static OkOrError<TError> Error(TError error) => new(error, true);

    public override string ToString() => 
        IsOk ? "IsOk" : _error.ToString();
}