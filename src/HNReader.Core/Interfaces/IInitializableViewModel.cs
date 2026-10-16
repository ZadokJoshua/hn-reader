namespace HNReader.Core.Interfaces;

/// <summary>
/// A ViewModel that loads its own content when navigated to.
///
/// <para>
/// The navigation service already kicks off <c>PopulateListAsync()</c> for
/// story-list pages by testing for <c>PageViewModel</c>. A page that isn't a
/// story list needs the same "you're on screen now, go load" signal without
/// inheriting 800 lines of story/pagination/comment machinery, so it opts in
/// here instead.
/// </para>
///
/// <para>
/// An opt-in interface rather than a virtual hook on <c>BaseViewModel</c>: a
/// hook on the shared base would fire for every story page too, racing the
/// existing <c>PopulateListAsync()</c> call and double-loading. Implementations
/// are therefore mutually exclusive with <c>PageViewModel</c>.
/// </para>
/// </summary>
public interface IInitializableViewModel
{
    /// <summary>
    /// Called once per navigation to this page. Implementations are expected to
    /// be cheap on repeat calls — the user may navigate back and forth freely.
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);
}
