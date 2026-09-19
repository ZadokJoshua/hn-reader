namespace HNReader.Core.Enums;

public enum ApplicationPages
{
    Top,
    Best,
    New,
    Show,
    Ask,
    Favourites,
    Settings,

    // Appended last deliberately: these values are persisted nowhere, but they
    // are used as nav-item Tags, and keeping existing ordinals stable costs
    // nothing. Only the Avalonia shell has a Digest page — WinUI's resolver
    // switches end in a throwing default, which is unreachable there because it
    // has no Digest nav item to produce the Tag.
    Digest
}
