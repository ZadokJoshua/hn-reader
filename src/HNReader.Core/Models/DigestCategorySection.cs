using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using HNReader.Shared.Models;

namespace HNReader.Core.Models;

/// <summary>
/// One category section of the digest, prepared for binding.
/// <para>
/// Named "Section" rather than "Category" because it is not the taxonomy entry
/// — it is a taxonomy entry <i>as it appears in a particular digest</i>, which
/// may be empty. A section can be built from a digest's category (with stories)
/// or from just a taxonomy name (without), so a category the server lists but
/// today's digest didn't fill still appears in the UI instead of silently
/// vanishing.
/// </para>
/// </summary>
public partial class DigestCategorySection : ObservableObject
{
    public string Name { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public ObservableCollection<DigestItem> Items { get; init; } = [];

    public int ItemCount => Items.Count;

    public bool HasItems => Items.Count > 0;

    public bool HasSummary => !string.IsNullOrWhiteSpace(Summary);

    public static DigestCategorySection From(DigestCategoryDto dto) => new()
    {
        Name = dto.Name,
        Summary = dto.Summary,
        Items = new ObservableCollection<DigestItem>(dto.Items.Select(DigestItem.From))
    };

    /// <summary>A category the taxonomy lists but this digest has no stories for.</summary>
    public static DigestCategorySection Empty(string name) => new() { Name = name };
}
