using HNReader.Core.Enums;
using HNReader.Core.Viewmodels;
using Microsoft.UI.Xaml.Controls;
using System;

namespace HNReader.WinUI.Factories;

public class PageFactory(Func<ApplicationPages, BaseViewModel> viewModelResolver, Func<ApplicationPages, Page> pageResolver)
{
    public BaseViewModel GetPageViewModel(ApplicationPages page) => viewModelResolver.Invoke(page);
    public Page GetPage(ApplicationPages page) => pageResolver.Invoke(page);
}
