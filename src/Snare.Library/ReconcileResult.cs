namespace Snare.Library;

/// <summary>What startup reconciliation changed.</summary>
public sealed record ReconcileResult(int Reexported, int Trashed, int Imported)
{
    public bool Changed => Reexported + Trashed + Imported > 0;
}
