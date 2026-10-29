namespace Snare.Core.Recording;

/// <summary>A microphone or camera. <paramref name="Id"/> is the device path the recorder needs.</summary>
public sealed record MediaDevice(string Id, string Name, bool IsDefault);
