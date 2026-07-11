using System.Text.RegularExpressions;

namespace KosovaPOS.Core.Data;

/// <summary>
/// Validation for the two identifiers that reach Postgres as text rather than as
/// parameters: a business code and a database name. <c>CREATE DATABASE</c> and
/// <c>DROP DATABASE</c> take an identifier, not a value, so they cannot be
/// parameterized — the name is concatenated into the statement. This class is
/// therefore the entire defence, and it allowlists rather than escapes.
/// </summary>
public static partial class PgIdentifier
{
    /// <summary>
    /// Lowercase, starts with a letter, then letters/digits/underscore. No quotes,
    /// no dashes, no whitespace, nothing that survives into SQL as syntax.
    /// Lowercase-only because unquoted Postgres identifiers fold to lowercase, so
    /// allowing "Shop" and "shop" would let two rows name the same database.
    /// </summary>
    [GeneratedRegex("^[a-z][a-z0-9_]*$")]
    private static partial Regex Allowed();

    /// <summary>Postgres truncates identifiers past 63 bytes.</summary>
    public const int MaxDatabaseNameLength = 63;

    /// <summary>Leaves room for the "pos_" prefix inside the 63-char database limit.</summary>
    public const int MaxCodeLength = 32;

    /// <summary>
    /// Codes that would collide with the control database, a Postgres template,
    /// or the maintenance database. Checked against the code — "control" would
    /// otherwise provision "pos_control" straight on top of the registry itself.
    /// </summary>
    private static readonly HashSet<string> ReservedCodes =
        new(StringComparer.Ordinal) { "control", "postgres", "template0", "template1", "admin" };

    public static bool IsValidCode(string? code) =>
        !string.IsNullOrEmpty(code)
        && code.Length is >= 2 and <= MaxCodeLength
        && Allowed().IsMatch(code)
        && !ReservedCodes.Contains(code);

    public static bool IsValidDatabaseName(string? name) =>
        !string.IsNullOrEmpty(name)
        && name.Length <= MaxDatabaseNameLength
        && Allowed().IsMatch(name);

    /// <summary>
    /// Throws unless <paramref name="name"/> is safe to concatenate into DDL.
    /// Call this immediately before building any statement that names a database.
    /// </summary>
    public static string RequireDatabaseName(string? name)
    {
        if (!IsValidDatabaseName(name))
            throw new ArgumentException($"Unsafe or invalid Postgres database name: '{name}'.", nameof(name));
        return name!;
    }

    public static string RequireCode(string? code)
    {
        if (!IsValidCode(code))
            throw new ArgumentException(
                $"Invalid business code: '{code}'. Use 2–{MaxCodeLength} chars: a lowercase letter " +
                "followed by lowercase letters, digits or underscores.", nameof(code));
        return code!;
    }

    /// <summary>The database name a business code provisions into.</summary>
    public static string DatabaseNameForCode(string code) => "pos_" + RequireCode(code);
}
