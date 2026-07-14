using Xunit;

namespace KosovaPOS.Tests;

/// <summary>
/// These tests all drive the one shared QA Postgres, and several of them deliberately
/// fire many writers at once to prove the locking. Two such classes running at the same
/// time would pile far more concurrent transactions onto that single database than any
/// real deployment ever does — one app, serialised through the very locks under test —
/// and the extra contention shows up as the occasional command timeout, not a real
/// defect. Sharing one collection makes xUnit run them one class at a time; each still
/// exercises concurrency internally, which is the concurrency that matters.
/// </summary>
[CollectionDefinition("db")]
public sealed class DbCollection;
