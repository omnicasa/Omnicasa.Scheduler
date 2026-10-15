namespace Omnicasa.Schedule;

/// <summary>
/// A list one thread rebuilds while another reads it. The writer fills <see cref="Pending"/> and
/// <see cref="Publish"/> swaps it in whole; readers enumerate <see cref="Snapshot"/>, which is never
/// mutated after it is published.
/// </summary>
/// <typeparam name="T">Element type.</typeparam>
public sealed class SnapshotList<T>
{
    // MOBILE-12756 paint runs on the GL thread on Android while touch reads on the UI thread
    private volatile List<T> current = new List<T>();
    private List<T> pending = new List<T>();

    /// <summary>Gets the last published list. Safe to enumerate from any thread.</summary>
    public IReadOnlyList<T> Snapshot => current;

    /// <summary>Gets the list being built. Writer thread only.</summary>
    public List<T> Pending => pending;

    /// <summary>Empties the list being built. Writer thread only.</summary>
    public void Reset() => pending.Clear();

    /// <summary>Makes the list being built the current snapshot and starts a fresh one. Writer thread only.</summary>
    public void Publish()
    {
        current = pending;
        pending = new List<T>();
    }
}
