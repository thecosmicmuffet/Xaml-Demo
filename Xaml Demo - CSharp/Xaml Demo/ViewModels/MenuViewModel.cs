using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using Xaml_Demo.Services;

namespace Xaml_Demo.ViewModels;

public sealed class MenuItemVM
{
    public string Key { get; }
    public string Title { get; }
    public ICommand Command { get; }

    public MenuItemVM(string key, string title)
    {
        Key = key;
        Title = title;
        Command = new Command(() => NavigationHub.RequestNavigate(Key));
    }
}

public sealed class MenuViewModel
{
    public static MenuViewModel Instance { get; } = new();

    public ObservableCollection<MenuItemVM> Items { get; } = new();
    public ICommand NavigateCommand { get; }

    private MenuViewModel()
    {
        NavigateCommand = new Command<string>(key => NavigationHub.RequestNavigate(key));

        foreach (var (key, title) in ScenarioCatalog.Items)
            Items.Add(new MenuItemVM(key, title));
    }
}
