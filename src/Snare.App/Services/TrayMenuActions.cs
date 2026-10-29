namespace Snare.App.Services;

/// <summary>What the tray menu's entries do.</summary>
public sealed record TrayMenuActions(Action Capture, Action<int> CaptureAfterDelay, Action GrabText, Action RecordVideo, Action OpenLibrary);
