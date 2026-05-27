using System;
using System.Windows;
using System.Windows.Controls;

namespace PlutoWindowsScannerGui;

public sealed class RunLogWindow : Window
{
    private readonly TextBox _logBox;
    private readonly Func<string> _getLogText;

    public RunLogWindow(Func<string> getLogText)
    {
        _getLogText = getLogText;

        Title = "Run Log";
        Width = 980;
        Height = 620;
        MinWidth = 720;
        MinHeight = 420;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var root = new DockPanel
        {
            Margin = new Thickness(10)
        };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 8)
        };

        var refreshButton = new Button
        {
            Content = "Refresh",
            Width = 90,
            Margin = new Thickness(0, 0, 8, 0)
        };
        refreshButton.Click += (_, _) => RefreshLog();

        var closeButton = new Button
        {
            Content = "Close",
            Width = 90
        };
        closeButton.Click += (_, _) => Close();

        buttons.Children.Add(refreshButton);
        buttons.Children.Add(closeButton);
        DockPanel.SetDock(buttons, Dock.Top);
        root.Children.Add(buttons);

        _logBox = new TextBox
        {
            Background = System.Windows.Media.Brushes.Black,
            Foreground = System.Windows.Media.Brushes.LightGray,
            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
            FontSize = 12,
            TextWrapping = TextWrapping.NoWrap,
            AcceptsReturn = true,
            IsReadOnly = true,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        root.Children.Add(_logBox);
        Content = root;

        Loaded += (_, _) => RefreshLog();
    }

    public void RefreshLog()
    {
        _logBox.Text = _getLogText();
        _logBox.CaretIndex = _logBox.Text.Length;
        _logBox.ScrollToEnd();
    }
}
