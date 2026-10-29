namespace Snare.Core.Captures;

/// <summary>Grabs every monitor at physical resolution before any capture UI appears.</summary>
public interface IScreenFreezer
{
    /// <param name="includeCursor">Draw the mouse pointer into the frozen frames.</param>
    FrozenDesktop Freeze(bool includeCursor = false);
}
