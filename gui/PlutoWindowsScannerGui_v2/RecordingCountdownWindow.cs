using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace PlutoWindowsScannerGui;

public sealed class RecordingCountdownWindow : Window
{
    private readonly TextBlock _countdownText;
    private readonly ProgressBar _progressBar;
    private readonly DispatcherTimer _timer;
    private readonly DateTime _startUtc;
    private readonly DateTime _endUtc;
    private readonly int _totalSeconds;

    public RecordingCountdownWindow(int seconds, object frequencyMhz)
    {
        _totalSeconds = Math.Max(1, seconds);
        _startUtc = DateTime.UtcNow;
        _endUtc = _startUtc.AddSeconds(_totalSeconds);

        Title = "Recording Audio";
        Width = 360;
        Height = 175;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        Topmost = true;

        var root = new StackPanel
        {
            Margin = new Thickness(18)
        };

        root.Children.Add(new TextBlock
        {
            Text = $"Recording {_totalSeconds} seconds...",
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8)
        });

        root.Children.Add(new TextBlock
        {
            Text = $"Frequency: {frequencyMhz} MHz",
            FontSize = 13,
            Margin = new Thickness(0, 0, 0, 12)
        });

        _countdownText = new TextBlock
        {
            Text = "",
            FontSize = 15,
            Margin = new Thickness(0, 0, 0, 10)
        };
        root.Children.Add(_countdownText);

        _progressBar = new ProgressBar
        {
            Minimum = 0,
            Maximum = _totalSeconds,
            Height = 20,
            Value = 0
        };
        root.Children.Add(_progressBar);

        Content = root;

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _timer.Tick += (_, _) => UpdateCountdown();

        Loaded += (_, _) =>
        {
            UpdateCountdown();
            _timer.Start();
        };

        Closed += (_, _) => _timer.Stop();
    }

    private void UpdateCountdown()
    {
        double elapsed = Math.Max(0, (DateTime.UtcNow - _startUtc).TotalSeconds);
        int remaining = Math.Max(0, (int)Math.Ceiling((_endUtc - DateTime.UtcNow).TotalSeconds));

        _countdownText.Text = remaining == 1
            ? "1 second remaining"
            : $"{remaining} seconds remaining";

        _progressBar.Value = Math.Min(_totalSeconds, elapsed);
    }

    public void CloseSafely()
    {
        try
        {
            _timer.Stop();
            Close();
        }
        catch
        {
            // Best effort only.
        }
    }
}
