namespace HolyChecker.Models;

public sealed class VirtualMachineInfo
{
    public bool IsVirtualMachine { get; init; }
    public string DetectedPlatform { get; init; } = "Physical Machine";
    public string BiosInfo { get; init; } = string.Empty;
    public string Manufacturer { get; init; } = string.Empty;
    public string MacAddress { get; init; } = string.Empty;
    public bool HyperVDetected { get; init; }
}