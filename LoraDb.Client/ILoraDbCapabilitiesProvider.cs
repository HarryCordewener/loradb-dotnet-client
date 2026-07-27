namespace LoraDb.Client;

public interface ILoraDbCapabilitiesProvider
{
    LoraDbClientCapabilities Capabilities { get; }
}
