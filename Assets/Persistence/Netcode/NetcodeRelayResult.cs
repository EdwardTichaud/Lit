public readonly struct NetcodeRelayResult
{
    public NetcodeRelayResult(bool succeeded, string joinCode, string error)
    {
        Succeeded = succeeded;
        JoinCode = joinCode ?? string.Empty;
        Error = error ?? string.Empty;
    }

    public bool Succeeded { get; }
    public string JoinCode { get; }
    public string Error { get; }

    public static NetcodeRelayResult Success(string joinCode)
    {
        return new NetcodeRelayResult(true, joinCode, string.Empty);
    }

    public static NetcodeRelayResult Failure(string error)
    {
        return new NetcodeRelayResult(false, string.Empty, error);
    }
}
