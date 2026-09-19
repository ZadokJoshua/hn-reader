namespace HNReader.Server.Configuration;

/// <summary>
/// The initial digest taxonomy, used only to populate an empty database.
///
/// <para>
/// Every category is deliberately specific: there is no "Other / General Tech"
/// catch-all. A junk bucket is the path of least resistance for a classifier,
/// and it showed: it collected unrelated stories under a heading no reader can
/// act on, and its section summary could only be a list of unrelated things.
/// A story the classifier can't place in a real category is dropped from the
/// digest instead, which is why the taxonomy is wide — the specific buckets
/// have to actually cover HN's range. Empty categories cost nothing: a category
/// with no stories staged against it is skipped without an LLM call.
/// </para>
///
/// <para>
/// <b>This is seed data, not configuration.</b> The database is authoritative
/// once seeded, so editing this array does not change a running deployment's
/// taxonomy — categories are managed through the admin endpoints instead. To
/// re-seed from scratch, delete the <c>digest_categories</c> collection.
/// </para>
/// </summary>
public static class DigestCategorySeed
{
    public static readonly string[] Names =
    [
        "Programming & Software Engineering",
        "Artificial Intelligence & Machine Learning",
        "Systems, Infrastructure & Databases",
        "Developer Tools & Open Source",
        "Security & Privacy",
        "Startups, Funding & Business",
        "Internet Policy, Law & Regulation",
        "Hardware, Chips & Electronics",
        "Science & Research",
        "Space & Aerospace",
        "Health, Medicine & Biotech",
        "Climate & Energy",
        "Mathematics & Theory",
        "Design & User Experience",
        "Career, Work & Culture",
        "History & Retrocomputing"
    ];

    /// <summary>
    /// Gap between consecutive seeded sort orders. Leaves room to insert a
    /// category between two existing ones without renumbering the rest.
    /// </summary>
    public const int SortOrderStep = 10;
}
