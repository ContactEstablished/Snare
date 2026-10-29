namespace Snare.Core.Captures;

/// <summary>Grabs every monitor at physical resolution before any capture UI appears.</summary>
public interface IScreenFreezer
{
    FrozenDesktop Freeze();
}
