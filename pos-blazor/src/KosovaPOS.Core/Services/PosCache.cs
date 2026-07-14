using System.Collections.Concurrent;
using KosovaPOS.Core.Data;

namespace KosovaPOS.Core.Services;

/// <summary>
/// Holds the handful of things every screen reads and almost nothing writes: the article
/// table, the category list, the shop's settings row.
///
/// Six screens open by loading all ~1,600 articles, the layout re-reads the settings row on
/// every navigation, and each of those ran as a fresh query against Postgres — so simply
/// walking around the app re-read the whole catalogue several times a minute, for data that
/// had not changed since the shop opened.
///
/// Correctness comes from <see cref="DataVersions"/>: an entry is stamped with the version
/// it was read at, and is ignored the moment a save moves that version. Stock quantities live
/// in here, so a stale entry would show a cashier stock that has already been sold — which is
/// why invalidation is driven by the DbContext and not by anyone remembering to call it.
///
/// Two circuits missing at once both load, and the last one wins. That is a duplicated query,
/// not a wrong answer, and it is cheaper than holding a lock across a database call.
/// </summary>
public sealed class PosCache
{
    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    private sealed record Entry(long Version, object Value);

    public async Task<T> GetOrLoadAsync<T>(string database, string topic, Func<Task<T>> load) where T : class
    {
        var key = $"{database} {topic}";
        var version = DataVersions.Current(database, topic);

        if (_entries.TryGetValue(key, out var hit) && hit.Version == version)
            return (T)hit.Value;

        var value = await load();

        // Stamped with the version read BEFORE the load: if a save landed while the query was in
        // flight, the version has already moved past this one and the entry is dead on arrival —
        // which is exactly right, because the rows it holds are the old ones.
        _entries[key] = new Entry(version, value);
        return value;
    }
}
