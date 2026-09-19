using CommunityToolkit.Mvvm.ComponentModel;

namespace HNReader.Core.Models;

/// <summary>
/// One entry in the category picker: a taxonomy name plus whether the user wants
/// to see it.
/// <para>
/// Separate from <see cref="DigestCategorySection"/> because the two answer
/// different questions — this is "which categories exist and which do I want",
/// which comes from the taxonomy endpoint and survives across digests, while a
/// section is "what was in this particular digest".
/// </para>
/// </summary>
public partial class DigestCategoryOption : ObservableObject
{
    public DigestCategoryOption(string name, bool isSelected)
    {
        Name = name;
        this.isSelected = isSelected;
    }

    public string Name { get; }

    [ObservableProperty]
    private bool isSelected;
}
