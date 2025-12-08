using LauncherTF2.Core;
using System;

namespace LauncherTF2.ViewModels;

public class BlogViewModel : ViewModelBase
{
    public Uri NewsUrl => new Uri("https://www.teamfortress.com/?tab=news");

    public BlogViewModel()
    {
    }
}
