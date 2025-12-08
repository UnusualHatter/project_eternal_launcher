using LauncherTF2.Core;
using LauncherTF2.Services;
using System.Windows.Input;

namespace LauncherTF2.ViewModels;

public class HomeViewModel : ViewModelBase
{
    private readonly GameService _gameService;

    public ICommand PlayCommand { get; }

    public HomeViewModel()
    {
        _gameService = new GameService();
        PlayCommand = new RelayCommand(o => _gameService.LaunchTF2());
    }
}
