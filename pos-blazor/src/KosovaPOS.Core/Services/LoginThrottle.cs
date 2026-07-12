using System.Collections.Concurrent;

namespace KosovaPOS.Core.Services;

/// <summary>The verdict on one login attempt, before the password is even checked.</summary>
/// <param name="LockedUntil">When the caller may try again. Null when not locked.</param>
public sealed record ThrottleState(bool IsLocked, DateTime? LockedUntil, int FailuresSoFar)
{
    public int MinutesRemaining => LockedUntil is null
        ? 0
        : Math.Max(1, (int)Math.Ceiling((LockedUntil.Value - DateTime.UtcNow).TotalMinutes));
}

/// <summary>
/// Failed-login lockout. Until now the login form would answer an unlimited number
/// of guesses per second, on a public URL, for every business at once — a four-digit
/// PIN-grade password was one afternoon of curl away.
///
/// Two counters, with very different thresholds and for different attackers:
///
/// <list type="bullet">
/// <item><b>Per account</b> (business + username): 5 failures inside 15 minutes locks
/// that one account for 5 minutes, doubling to a 60-minute cap while failures keep
/// coming. This is the one that stops password guessing. It is short on purpose — the
/// common cause of five bad passwords is a cashier at a till, not an attacker, and a
/// shop that cannot ring up customers for an hour is a worse outcome than the attack
/// this prevents.</item>
/// <item><b>Per IP</b>: 30 failures inside 15 minutes locks the address for 15 minutes,
/// regardless of which accounts were tried. This is the one that stops someone spraying
/// one password across every username. ⚠️ A shop's tills share one NAT address, so this
/// threshold is deliberately far above anything honest use produces — do not lower it
/// without knowing that it can take a whole shop offline.</item>
/// </list>
///
/// State is in memory, so a restart clears every lock. That is acceptable: an attacker
/// cannot force a restart, and the alternative (a table write per failed guess) hands
/// them a way to fill the disk. It is single-instance, like the app.
/// </summary>
public sealed class LoginThrottle
{
    private const int AccountFailuresBeforeLock = 5;
    private const int IpFailuresBeforeLock = 30;
    private static readonly TimeSpan CountingWindow = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan AccountBaseLock = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan AccountMaxLock = TimeSpan.FromMinutes(60);
    private static readonly TimeSpan IpLock = TimeSpan.FromMinutes(15);

    private sealed class Counter
    {
        public int Failures;
        public DateTime FirstFailureUtc;
        public DateTime? LockedUntilUtc;
        public TimeSpan LastLock;
    }

    private readonly ConcurrentDictionary<string, Counter> _counters = new(StringComparer.OrdinalIgnoreCase);
    private long _lastSweepTicks = DateTime.UtcNow.Ticks;

    /// <summary>
    /// How often dead counters are swept, and how many entries force an early sweep.
    /// Every key holds an attacker-supplied username, so without this the defence
    /// against password guessing would itself be a memory-exhaustion vector: a script
    /// guessing a fresh username each time would add a dictionary entry per request,
    /// forever. A counter is dead once it is unlocked and its window has passed —
    /// dropping it is exactly equivalent to letting it expire in place.
    /// </summary>
    private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(5);
    private const int SweepAtEntries = 10_000;

    private static string AccountKey(string businessCode, string username) => $"acct:{businessCode}:{username}";
    private static string IpKey(string ip) => $"ip:{ip}";

    private void SweepIfDue(DateTime now)
    {
        var last = new DateTime(Interlocked.Read(ref _lastSweepTicks), DateTimeKind.Utc);
        if (now - last < SweepInterval && _counters.Count < SweepAtEntries)
            return;

        // One sweeper at a time; a thread that loses the race skips this round rather
        // than blocking a login behind a scan.
        if (Interlocked.CompareExchange(ref _lastSweepTicks, now.Ticks, last.Ticks) != last.Ticks)
            return;

        foreach (var (key, c) in _counters)
        {
            bool dead;
            lock (c)
                dead = (c.LockedUntilUtc is null || c.LockedUntilUtc <= now) && !WithinWindow(c, now);

            // Worst case a failure racing this loses its count — the counter it lands on
            // was already expiring, so the attempt would not have contributed to a lock
            // anyway.
            if (dead)
                _counters.TryRemove(key, out _);
        }
    }

    /// <summary>
    /// Whether this attempt may proceed. Call before verifying the password — the
    /// point is to not do the work, not to hide the answer afterwards.
    /// </summary>
    public ThrottleState Check(string businessCode, string username, string? ip)
    {
        var now = DateTime.UtcNow;
        var account = Peek(AccountKey(businessCode, username), now);
        if (account.IsLocked)
            return account;

        return string.IsNullOrEmpty(ip) ? account : Peek(IpKey(ip), now);
    }

    /// <summary>
    /// Records a failure and returns the resulting state, so the caller can tell a
    /// plain wrong password from the one that tripped a lock (the latter is worth an
    /// audit row).
    /// </summary>
    public ThrottleState Fail(string businessCode, string username, string? ip)
    {
        var now = DateTime.UtcNow;

        // Only failures add keys, so this is the only path that needs to drop dead ones.
        SweepIfDue(now);

        var account = Bump(AccountKey(businessCode, username), AccountFailuresBeforeLock, AccountBaseLock, AccountMaxLock, now);
        var byIp = string.IsNullOrEmpty(ip) ? null : Bump(IpKey(ip), IpFailuresBeforeLock, IpLock, IpLock, now);

        // Report whichever lock is actually in force; the account one is the likelier
        // and the more useful to show the user.
        if (account.IsLocked || byIp is null)
            return account;
        return byIp.IsLocked ? byIp : account;
    }

    /// <summary>
    /// Clears the account's counter after a correct password. The IP counter is left
    /// alone on purpose: one success among a spray of guesses must not reset the
    /// evidence of the spray.
    /// </summary>
    public void Succeed(string businessCode, string username) =>
        _counters.TryRemove(AccountKey(businessCode, username), out _);

    private ThrottleState Peek(string key, DateTime now)
    {
        if (!_counters.TryGetValue(key, out var c))
            return new ThrottleState(false, null, 0);

        lock (c)
        {
            if (c.LockedUntilUtc is { } until && until > now)
                return new ThrottleState(true, until, c.Failures);
            return new ThrottleState(false, null, WithinWindow(c, now) ? c.Failures : 0);
        }
    }

    private ThrottleState Bump(string key, int threshold, TimeSpan baseLock, TimeSpan maxLock, DateTime now)
    {
        var c = _counters.GetOrAdd(key, _ => new Counter { FirstFailureUtc = now });

        lock (c)
        {
            // A lock that has expired starts the count again from this failure —
            // otherwise the 6th ever failure would re-lock instantly, forever.
            if (c.LockedUntilUtc is { } until && until <= now)
            {
                c.LockedUntilUtc = null;
                c.Failures = 0;
                c.FirstFailureUtc = now;
            }
            else if (!WithinWindow(c, now))
            {
                c.Failures = 0;
                c.FirstFailureUtc = now;
                c.LastLock = TimeSpan.Zero;
            }

            c.Failures++;

            if (c.Failures >= threshold)
            {
                // Each further lock doubles, so a patient attacker pays exponentially
                // while a fat-fingered cashier only ever pays the first 5 minutes.
                var duration = c.LastLock == TimeSpan.Zero ? baseLock : c.LastLock * 2;
                if (duration > maxLock) duration = maxLock;

                c.LastLock = duration;
                c.LockedUntilUtc = now + duration;
                c.Failures = 0;              // the lock replaces the count
                c.FirstFailureUtc = now;
                return new ThrottleState(true, c.LockedUntilUtc, threshold);
            }

            return new ThrottleState(false, null, c.Failures);
        }
    }

    private static bool WithinWindow(Counter c, DateTime now) => now - c.FirstFailureUtc <= CountingWindow;
}
