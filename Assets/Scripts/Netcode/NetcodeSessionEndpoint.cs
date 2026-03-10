using System;

[Serializable]
public struct NetcodeSessionEndpoint
{
    public string Code;
    public string Address;
    public ushort Port;

    public NetcodeSessionEndpoint(string code, string address, ushort port)
    {
        Code = code ?? string.Empty;
        Address = address ?? string.Empty;
        Port = port;
    }

    public bool IsValid
    {
        get
        {
            return !string.IsNullOrWhiteSpace(Code)
                && !string.IsNullOrWhiteSpace(Address)
                && Port != 0;
        }
    }

    public string EndpointLabel
    {
        get
        {
            return $"{Address}:{Port}";
        }
    }
}
