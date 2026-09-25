using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ChessStrategyApp.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        // Questo sostituisce InitializeComponent() e non fallisce mai in compilazione
        AvaloniaXamlLoader.Load(this);
    }
}