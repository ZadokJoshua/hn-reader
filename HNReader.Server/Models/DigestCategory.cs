using LiteDB;

namespace HNReader.Server.Models;

/// <summary>
/// One category in the digest taxonomy.
/// <para>
/// The taxonomy used to live in <c>appsettings.json</c>. It moved here because a
/// list that can only change by editing a config file and redeploying isn't
/// manageable — and because binding a <c>List&lt;string&gt;</c> from
/// configuration is what produced the 16→32 category duplication bug
/// (ConfigurationBinder appends to a non-empty list default instead of
/// replacing it, which needed an explicit <c>Clear()</c> workaround).
/// </para>
/// <para>
/// Plain mutable class (not a record) so LiteDB's BSON mapper has no ambiguity
/// about which constructor to use — same reason as <see cref="StagedCandidate"/>
/// and <see cref="CachedStorySummary"/>.
/// </para>
/// </summary>
public class DigestCategory
{
    public ObjectId Id { get; set; } = ObjectId.NewObjectId();

    /// <summary>The canonical display spelling, as stored and as served.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// <see cref="Name"/> lowercased with the invariant culture, uniquely
    /// indexed. Deliberately not relying on LiteDB's own collation for
    /// case-insensitive uniqueness: it is culture-aware, so "probably unique
    /// depending on the process's ambient culture" is not a guarantee. Every
    /// other name comparison in this codebase uses
    /// <see cref="StringComparison.OrdinalIgnoreCase"/>, and this matches it.
    /// </summary>
    public string NameLower { get; set; } = string.Empty;

    /// <summary>
    /// Explicit ordering, because category order is load-bearing: the digest
    /// returns its sections in taxonomy order. Seeded in steps of 10 so a future
    /// insert-between never has to renumber its neighbours.
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Whether new digest runs classify into this category. Disabling retires a
    /// category from future runs without deleting it, so digests that already
    /// reference the name stay coherent.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }
}
