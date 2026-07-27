namespace LoraDb.Client.Native;

public sealed class LoraDbNativeException : Exception
{
    public LoraDbNativeException(
        int status,
        string message,
        string? code = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Status = status;
        Code = code;
    }

    public int Status { get; }

    public string? Code { get; }
}
