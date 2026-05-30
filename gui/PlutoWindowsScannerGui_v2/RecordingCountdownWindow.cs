using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows.Threading;

namespace PlutoWindowsScannerGui;

public sealed class RecordingCountdownWindow : Window
{
    public event EventHandler? StopRequested;

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
        Width = 390;
        Height = 240;
        MinHeight = 240;
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
            Value = 0,
            Margin = new Thickness(0, 0, 0, 14)
        };
        root.Children.Add(_progressBar);

        var stopButton = new Button
        {
            Content = "Stop Recording",
            Height = 34,
            MinWidth = 150,
            HorizontalAlignment = HorizontalAlignment.Center,
            FontWeight = FontWeights.SemiBold
        };

        stopButton.Click += (_, _) =>
        {
            stopButton.IsEnabled = false;
            stopButton.Content = "Stopping...";
            _countdownText.Text = "Stopping and finalizing WAV...";
            StopRequested?.Invoke(this, EventArgs.Empty);
        };

        root.Children.Add(stopButton);

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
    public void ConfigureAudioModeText(string windowTitle, string actionText, string stopButtonText)
    {
        Title = windowTitle;
        _audioModeActionText = actionText;
        _audioModeStopButtonText = stopButtonText;

        Loaded += (_, _) => ApplyAudioModeText();
        ApplyAudioModeText();
    }

    private string _audioModeActionText = "Recording";
    private string _audioModeStopButtonText = "Stop Recording";

    private void ApplyAudioModeText()
    {
        try
        {
            foreach (var textBlock in FindPopupVisualChildren<TextBlock>(this))
            {
                if (string.IsNullOrWhiteSpace(textBlock.Text))
                {
                    continue;
                }

                textBlock.Text = textBlock.Text
                    .Replace("Recording", _audioModeActionText)
                    .Replace("recording", _audioModeActionText.ToLowerInvariant());
            }

            foreach (var button in FindPopupVisualChildren<Button>(this))
            {
                string content = button.Content?.ToString() ?? string.Empty;

                if (content.Contains("Stop Recording", StringComparison.OrdinalIgnoreCase) ||
                    content.Contains("Stop", StringComparison.OrdinalIgnoreCase))
                {
                    button.Content = _audioModeStopButtonText;
                }
            }
        }
        catch
        {
            // Best effort only; popup still works if text replacement fails.
        }
    }

    private static IEnumerable<T> FindPopupVisualChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        if (parent == null)
        {
            yield break;
        }

        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, i);

            if (child is T typed)
            {
                yield return typed;
            }

            foreach (T descendant in FindPopupVisualChildren<T>(child))
            {
                yield return descendant;
            }
        }
    }


}
