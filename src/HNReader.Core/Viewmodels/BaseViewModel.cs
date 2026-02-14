using CommunityToolkit.Mvvm.ComponentModel;

namespace HNReader.Core.Viewmodels;

/// <summary>
/// Base class for all ViewModels in the application.
/// Provides common functionality needed by all pages.
/// </summary>
public abstract partial class BaseViewModel : ObservableObject
{
    // Common properties for all ViewModels can be added here
    // For now, this serves as the navigation contract
}
