using Xunit;

// Run tests in a single thread to avoid LiteDB file lock issues when
// multiple FavoritesService instances are created/destroyed in parallel.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
