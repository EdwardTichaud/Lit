public readonly struct NetcodeRelayResult
{
    public NetcodeRelayResult(bool succeeded, string joinCode, string error, PrivateSessionError errorKind = PrivateSessionError.Unavailable)
    {
        Succeeded = succeeded;
        ErrorKind = succeeded ? PrivateSessionError.None : errorKind;
        JoinCode = joinCode ?? string.Empty;
        Error = error ?? string.Empty;
    }

    public PrivateSessionError ErrorKind { get; }
    public bool Succeeded { get; }
    public string JoinCode { get; }
    public string Error { get; }

    public static NetcodeRelayResult Success(string joinCode)
    {
        return new NetcodeRelayResult(true, joinCode, string.Empty);
    }

    public static NetcodeRelayResult Failure(string error, PrivateSessionError errorKind = PrivateSessionError.Unavailable)
    {
        return new NetcodeRelayResult(false, string.Empty, error, errorKind);
    }
}
