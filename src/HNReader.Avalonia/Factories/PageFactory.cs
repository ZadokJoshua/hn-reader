using System;
using Avalonia.Controls;
using HNReader.Core.Enums;
using HNReader.Core.Viewmodels;

namespace HNReader.Avalonia.Factories;

public class PageFactory(Func<ApplicationPages, BaseViewModel> viewModelResolver, Func<ApplicationPages, UserControl> pageResolver)
{
    public BaseViewModel GetPageViewModel(ApplicationPages page) => viewModelResolver.Invoke(page);
    public UserControl GetPage(ApplicationPages page) => pageResolver.Invoke(page);
}
