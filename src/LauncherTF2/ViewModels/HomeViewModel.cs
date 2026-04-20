using LauncherTF2.Core;
using System.Windows.Input;

namespace LauncherTF2.ViewModels;

public class HomeViewModel : ViewModelBase
{
    public ICommand PlayCommand { get; }

    public HomeViewModel()
    {
        PlayCommand = new RelayCommand(o => ServiceLocator.Game.LaunchTF2());
    }
}
