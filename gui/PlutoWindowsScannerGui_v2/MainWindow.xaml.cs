using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using IoPath = System.IO.Path;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace PlutoWindowsScannerGui;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<ActiveChannel> ActiveChannels = new();
    private readonly List<BandDefinition> Bands = new();
    private readonly List<List<ScanPoint>> WaterfallHistory = new();
    private readonly List<ScanPoint> LastScanPoints = new();
    private Process? _scanProcess;
    private Process? _spectrumProcess;
    private CancellationTokenSource? _scanLoopCts;
    private CancellationTokenSource? _spectrumCts;
    private bool _scanLoopActive;
    private bool _liveSpectrumActive;
    private int _scanNumber;
    private int _liveSpectrumFrames;
    private int _liveRenderQueued;
    private long _lastLiveRenderTick;
    private AppConfig _config = new();
    private string _repoRoot = string.Empty;
    private string _lastScanCsv = string.Empty;
    private const int MaxWaterfallRows = 60;
    private const int LiveRenderIntervalMs = 250;
    private const int MaxWaterfallBitmapWidth = 900;
    private const int MaxWaterfallBitmapHeight = 260;
    private const long DefaultSampleRateHz = 1000000;
    private const long KnownInvalidSampleRateHz = 960000;

    public MainWindow()
    {
        InitializeComponent();
        ActiveChannelsGrid.ItemsSource = ActiveChannels;
        _repoRoot = FindRepoRoot();
        RepoRootText.Text = _repoRoot;
        LoadConfig();
        ApplyConfigToUi();
        LoadBands();
        DrawEmptyCharts();
        Closed += (_, _) =>
        {
            TryKillProcess(_scanProcess);
            TryKillProcess(_spectrumProcess);
        };
        Log("Windows Pluto SDR Scanner GUI v2.0 Phase 2 loaded.");
        Log($"Project root: {_repoRoot}");
    }

    private void StartScanButton_Click(object sender, RoutedEventArgs e)
    {
        _ = StartScanLoopAsync();
    }

    private async Task StartScanLoopAsync()
    {
        if (_scanLoopActive)
        {
            Log("A scan loop is already running.");
            return;
        }

        SaveUiToConfig();
        SaveConfig();
        EnsureSessionsDir();

        string scannerExe = FindTool("pluto_dual_rx_power_scan.exe");
        if (string.IsNullOrWhiteSpace(scannerExe))
        {
            MessageBox.Show("pluto_dual_rx_power_scan.exe was not found in bin/. Run ./tools/sync_backend_from_v1_repo.sh or reinstall the new-folder package.", "Scanner missing", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        _scanLoopCts = new CancellationTokenSource();
        _scanLoopActive = true;
        _scanNumber = 0;
        ActiveChannels.Clear();
        LastScanPoints.Clear();
        WaterfallHistory.Clear();
        DrawEmptyCharts();
        StartScanButton.IsEnabled = false;
        StopScanButton.IsEnabled = true;
        SpectrumCaption.Visibility = Visibility.Collapsed;
        WaterfallCaption.Visibility = Visibility.Collapsed;

        try
        {
            do
            {
                _scanLoopCts.Token.ThrowIfCancellationRequested();
                _scanNumber++;
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
                _lastScanCsv = IoPath.Combine(_config.SessionsDir, $"gui_scan_{timestamp}_{_scanNumber:000}.csv");

                string args;
                try
                {
                    args = BuildScanArguments(_lastScanCsv);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Invalid scan settings", MessageBoxButton.OK, MessageBoxImage.Warning);
                    break;
                }

                bool ok = await RunOneScanAsync(scannerExe, args, _lastScanCsv, _scanLoopCts.Token);
                if (!ok)
                    break;

                if (RepeatScanCheck.IsChecked != true)
                    break;

                int delaySeconds = Math.Clamp((int)ParseLongOrDefault(RepeatDelayText.Text, 2), 0, 3600);
                StatusText.Text = delaySeconds > 0
                    ? $"Waiting {delaySeconds}s before next scan. Active channels: {ActiveChannels.Count}."
                    : $"Repeating immediately. Active channels: {ActiveChannels.Count}.";
                if (delaySeconds > 0)
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds), _scanLoopCts.Token);
            }
            while (!_scanLoopCts.IsCancellationRequested);
        }
        catch (OperationCanceledException)
        {
            Log("Scan loop stopped by user.");
            StatusText.Text = "Stopped.";
        }
        finally
        {
            StartScanButton.IsEnabled = true;
            StopScanButton.IsEnabled = false;
            _scanLoopActive = false;
            _scanLoopCts?.Dispose();
            _scanLoopCts = null;
            _scanProcess = null;
            if (!StatusText.Text.StartsWith("Stopped", StringComparison.OrdinalIgnoreCase))
                StatusText.Text = $"Ready. Active channels: {ActiveChannels.Count}.";
        }
    }

    private async Task<bool> RunOneScanAsync(string scannerExe, string args, string csvPath, CancellationToken token)
    {
        StatusText.Text = $"Scanning pass {_scanNumber}...";
        Log($"Starting scan pass {_scanNumber}:");
        Log($"  {scannerExe}");
        Log($"  {args}");

        var psi = new ProcessStartInfo(scannerExe, args)
        {
            WorkingDirectory = _repoRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        _scanProcess = new Process { StartInfo = psi, EnableRaisingEvents = true };
        _scanProcess.OutputDataReceived += (_, ev) => { if (!string.IsNullOrWhiteSpace(ev.Data)) Dispatcher.Invoke(() => Log(ev.Data)); };
        _scanProcess.ErrorDataReceived += (_, ev) => { if (!string.IsNullOrWhiteSpace(ev.Data)) Dispatcher.Invoke(() => Log("ERR: " + ev.Data)); };

        try
        {
            _scanProcess.Start();
            _scanProcess.BeginOutputReadLine();
            _scanProcess.BeginErrorReadLine();
            await _scanProcess.WaitForExitAsync(token);
            int exitCode = _scanProcess.ExitCode;
            Log($"Scan pass {_scanNumber} exited with code {exitCode}.");

            if (File.Exists(csvPath))
            {
                ParseScanCsv(csvPath);
                StatusText.Text = $"Scan pass {_scanNumber} complete. Active channels: {ActiveChannels.Count}.";
                return exitCode == 0;
            }

            StatusText.Text = "Scan finished, but no CSV output was found.";
            Log($"No CSV found at {csvPath}");
            return false;
        }
        catch (OperationCanceledException)
        {
            TryKillProcess(_scanProcess);
            throw;
        }
        catch (Exception ex)
        {
            StatusText.Text = "Scan failed.";
            Log("Scan failed: " + ex.Message);
            MessageBox.Show(ex.Message, "Scan failed", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
        finally
        {
            _scanProcess = null;
        }
    }

    private string BuildScanArguments(string csvPath)
    {
        var args = new List<string>
        {
            "--uri", Q(UriText.Text.Trim()),
            "--rx-mode", Q(ComboText(RxModeCombo)),
            "--rx-combine", Q(ComboText(RxCombineCombo)),
            "--threshold-dbfs", SquelchText.Text.Trim(),
            "--csv", Q(csvPath)
        };

        long safeRateHz = GetSafeRateHzFromUi();
        args.Add("--rate");
        args.Add(safeRateHz.ToString(CultureInfo.InvariantCulture));
        AddOptionalNumericArg(args, "--bw", BwText.Text);

        string mode = ComboText(ScanModeCombo);
        if (mode.Equals("Single Frequency", StringComparison.OrdinalIgnoreCase))
        {
            long freq = ParseLong(SingleFreqText.Text, "single frequency Hz");
            string freqFile = IoPath.Combine(_config.SessionsDir, "gui_single_frequency.csv");
            File.WriteAllText(freqFile, "frequency_hz,label" + Environment.NewLine + $"{freq},GUI Single {freq / 1000000.0:F6} MHz" + Environment.NewLine);
            args.Add("--freq-file");
            args.Add(Q(freqFile));
        }
        else if (mode.Equals("Frequency Range", StringComparison.OrdinalIgnoreCase))
        {
            args.Add("--start"); args.Add(ParseLong(StartFreqText.Text, "start Hz").ToString(CultureInfo.InvariantCulture));
            args.Add("--stop"); args.Add(ParseLong(StopFreqText.Text, "stop Hz").ToString(CultureInfo.InvariantCulture));
            args.Add("--step"); args.Add(ParseLong(StepHzText.Text, "step Hz").ToString(CultureInfo.InvariantCulture));
        }
        else
        {
            if (BandCombo.SelectedItem is not BandDefinition band)
                throw new InvalidOperationException("Choose a standard band first.");
            args.Add("--start"); args.Add(band.StartHz.ToString(CultureInfo.InvariantCulture));
            args.Add("--stop"); args.Add(band.StopHz.ToString(CultureInfo.InvariantCulture));
            args.Add("--step"); args.Add(band.StepHz.ToString(CultureInfo.InvariantCulture));
        }

        if (VerboseCheck.IsChecked == true)
            args.Add("--verbose");

        return string.Join(" ", args);
    }

    private static void AddOptionalNumericArg(List<string> args, string name, string value)
    {
        value = value.Trim();
        if (string.IsNullOrWhiteSpace(value)) return;
        if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)) return;
        args.Add(name);
        args.Add(value);
    }

    private void StartLiveSpectrumButton_Click(object sender, RoutedEventArgs e)
    {
        _ = StartLiveSpectrumAsync();
    }

    private void StopLiveSpectrumButton_Click(object sender, RoutedEventArgs e)
    {
        StopLiveSpectrum("Stop requested by user.");
    }

    private void UseSelectedForLiveButton_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveChannelsGrid.SelectedItem is ActiveChannel selected)
        {
            LiveCenterFreqText.Text = selected.FrequencyHz.ToString(CultureInfo.InvariantCulture);
            StatusText.Text = $"Live center set to {selected.FrequencyMhz} MHz.";
            return;
        }

        try
        {
            long center;
            string mode = ComboText(ScanModeCombo);
            if (mode.Equals("Single Frequency", StringComparison.OrdinalIgnoreCase))
            {
                center = ParseLong(SingleFreqText.Text, "single frequency Hz");
            }
            else if (mode.Equals("Frequency Range", StringComparison.OrdinalIgnoreCase))
            {
                long start = ParseLong(StartFreqText.Text, "start Hz");
                long stop = ParseLong(StopFreqText.Text, "stop Hz");
                center = start + ((stop - start) / 2);
            }
            else if (BandCombo.SelectedItem is BandDefinition band)
            {
                center = band.StartHz + ((band.StopHz - band.StartHz) / 2);
            }
            else
            {
                center = ParseLong(SingleFreqText.Text, "single frequency Hz");
            }

            LiveCenterFreqText.Text = center.ToString(CultureInfo.InvariantCulture);
            StatusText.Text = $"Live center set to {center / 1000000.0:F6} MHz.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Could not set live center", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async Task StartLiveSpectrumAsync()
    {
        if (_liveSpectrumActive)
        {
            Log("Live spectrum is already running.");
            return;
        }

        SaveUiToConfig();
        SaveConfig();

        string spectrumExe = FindTool("pluto_spectrum_stream.exe");
        if (string.IsNullOrWhiteSpace(spectrumExe))
        {
            MessageBox.Show("pluto_spectrum_stream.exe was not found in bin/. Run ./tools/sync_backend_from_v1_repo.sh or reinstall the v2 package.", "Spectrum tool missing", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        string args;
        try
        {
            args = BuildLiveSpectrumArguments();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Invalid live spectrum settings", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _spectrumCts = new CancellationTokenSource();
        _liveSpectrumActive = true;
        _liveSpectrumFrames = 0;
        _liveRenderQueued = 0;
        _lastLiveRenderTick = 0;
        StartLiveSpectrumButton.IsEnabled = false;
        StopLiveSpectrumButton.IsEnabled = true;
        SpectrumCaption.Visibility = Visibility.Collapsed;
        WaterfallCaption.Visibility = Visibility.Collapsed;
        LiveSpectrumStatusText.Text = "Starting live spectrum...";
        StatusText.Text = "Starting live spectrum...";

        var psi = new ProcessStartInfo(spectrumExe, args)
        {
            WorkingDirectory = _repoRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        _spectrumProcess = new Process { StartInfo = psi, EnableRaisingEvents = true };
        _spectrumProcess.OutputDataReceived += (_, ev) =>
        {
            if (string.IsNullOrWhiteSpace(ev.Data)) return;

            string line = ev.Data;
            if (line.StartsWith("SPECTRUM,", StringComparison.OrdinalIgnoreCase))
            {
                // Live spectrum can arrive faster than WPF can render. Keep only one
                // pending render queued so Stop Live and the rest of the UI remain responsive.
                if (Interlocked.CompareExchange(ref _liveRenderQueued, 1, 0) != 0)
                    return;

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try { HandleLiveSpectrumLine(line); }
                    finally { Interlocked.Exchange(ref _liveRenderQueued, 0); }
                }));
                return;
            }

            Dispatcher.BeginInvoke(new Action(() => HandleLiveSpectrumLine(line)));
        };
        _spectrumProcess.ErrorDataReceived += (_, ev) =>
        {
            if (string.IsNullOrWhiteSpace(ev.Data)) return;
            Dispatcher.BeginInvoke(new Action(() => Log("LIVE ERR: " + ev.Data)));
        };

        try
        {
            Log("Starting live spectrum stream:");
            Log($"  {spectrumExe}");
            Log($"  {args}");
            _spectrumProcess.Start();
            _spectrumProcess.BeginOutputReadLine();
            _spectrumProcess.BeginErrorReadLine();
            await _spectrumProcess.WaitForExitAsync(_spectrumCts.Token);
            int exitCode = _spectrumProcess.ExitCode;
            Log($"Live spectrum exited with code {exitCode}.");
            LiveSpectrumStatusText.Text = $"Live spectrum stopped. Frames: {_liveSpectrumFrames}.";
        }
        catch (OperationCanceledException)
        {
            TryKillProcess(_spectrumProcess);
            Log("Live spectrum stopped by user.");
            LiveSpectrumStatusText.Text = $"Live spectrum stopped. Frames: {_liveSpectrumFrames}.";
        }
        catch (Exception ex)
        {
            Log("Live spectrum failed: " + ex.Message);
            LiveSpectrumStatusText.Text = "Live spectrum failed.";
            MessageBox.Show(ex.Message, "Live spectrum failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            StartLiveSpectrumButton.IsEnabled = true;
            StopLiveSpectrumButton.IsEnabled = false;
            _liveSpectrumActive = false;
            _spectrumProcess = null;
            _spectrumCts?.Dispose();
            _spectrumCts = null;
            StatusText.Text = $"Ready. Live frames: {_liveSpectrumFrames}; active channels: {ActiveChannels.Count}.";
        }
    }

    private string BuildLiveSpectrumArguments()
    {
        long centerHz = ParseLong(LiveCenterFreqText.Text, "live center Hz");
        long rateHz = GetSafeRateHzFromUi();
        long bwHz = ParseLongOrDefault(BwText.Text, rateHz);
        int fft = (int)Math.Clamp(ParseLongOrDefault(LiveFftText.Text, 512), 256, 65536);
        int avg = (int)Math.Clamp(ParseLongOrDefault(LiveAvgText.Text, 2), 1, 1000);
        int intervalMs = (int)Math.Clamp(ParseLongOrDefault(LiveIntervalText.Text, 500), 0, 60000);
        string gainMode = ComboText(LiveGainModeCombo);

        var args = new List<string>
        {
            "--uri", Q(UriText.Text.Trim()),
            "--freq", centerHz.ToString(CultureInfo.InvariantCulture),
            "--rate", rateHz.ToString(CultureInfo.InvariantCulture),
            "--bw", bwHz.ToString(CultureInfo.InvariantCulture),
            "--fft", fft.ToString(CultureInfo.InvariantCulture),
            "--avg", avg.ToString(CultureInfo.InvariantCulture),
            "--interval-ms", intervalMs.ToString(CultureInfo.InvariantCulture),
            "--frames", "0",
            "--gain-mode", Q(gainMode)
        };

        string gainDb = LiveGainDbText.Text.Trim();
        if (!string.IsNullOrWhiteSpace(gainDb))
        {
            if (!int.TryParse(gainDb, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                throw new InvalidOperationException("Gain dB must be a whole number, or leave it blank for automatic gain.");
            args.Add("--gain-db");
            args.Add(gainDb);
        }

        return string.Join(" ", args);
    }

    private void StopLiveSpectrum(string reason)
    {
        try
        {
            _spectrumCts?.Cancel();
            TryKillProcess(_spectrumProcess);
            Log("Live spectrum stop requested: " + reason);
            LiveSpectrumStatusText.Text = "Stopping live spectrum...";
        }
        catch (Exception ex)
        {
            Log("Live spectrum stop failed: " + ex.Message);
        }
    }

    private void HandleLiveSpectrumLine(string line)
    {
        if (line.StartsWith("SPECTRUM,", StringComparison.OrdinalIgnoreCase))
        {
            long now = Environment.TickCount64;
            if (_lastLiveRenderTick != 0 && now - _lastLiveRenderTick < LiveRenderIntervalMs)
                return;
            _lastLiveRenderTick = now;

            if (TryParseSpectrumFrame(line, out var points, out long centerHz, out long rateHz, out int frameNumber))
            {
                LastScanPoints.Clear();
                LastScanPoints.AddRange(points);
                WaterfallHistory.Add(points);
                while (WaterfallHistory.Count > MaxWaterfallRows) WaterfallHistory.RemoveAt(0);
                DrawSpectrum();
                DrawWaterfall();
                _liveSpectrumFrames++;
                LiveSpectrumStatusText.Text = $"Live frame {frameNumber}; center {centerHz / 1000000.0:F6} MHz; span {rateHz / 1000000.0:F3} MHz; bins {points.Count}; render {LiveRenderIntervalMs} ms.";
            }
            else
            {
                Log("LIVE: Could not parse spectrum frame header.");
            }
            return;
        }

        if (line.StartsWith("STATUS,", StringComparison.OrdinalIgnoreCase))
        {
            string status = line.Length > 7 ? line[7..] : line;
            LiveSpectrumStatusText.Text = status;
            Log("LIVE: " + line);
            return;
        }

        Log("LIVE: " + line);
    }

    private bool TryParseSpectrumFrame(string line, out List<ScanPoint> points, out long centerHz, out long rateHz, out int frameNumber)
    {
        points = new List<ScanPoint>();
        centerHz = 0;
        rateHz = 0;
        frameNumber = 0;

        string[] values = SplitCsv(line);
        if (values.Length < 10) return false;
        if (!int.TryParse(values[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out frameNumber)) return false;
        if (!long.TryParse(values[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out centerHz)) return false;
        if (!long.TryParse(values[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out rateHz)) return false;
        _ = int.TryParse(values[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out int fftBins);

        int powerStart = FindSpectrumPowerStart(values, fftBins);
        if (powerStart < 0 || powerStart >= values.Length) return false;

        var powers = new List<double>();
        for (int i = powerStart; i < values.Length; i++)
        {
            if (double.TryParse(values[i], NumberStyles.Float, CultureInfo.InvariantCulture, out double db))
                powers.Add(db);
        }
        if (powers.Count == 0) return false;

        double startHz = centerHz - (rateHz / 2.0);
        double binHz = rateHz / Math.Max(1.0, powers.Count);

        for (int i = 0; i < powers.Count; i++)
        {
            long freq = (long)Math.Round(startHz + ((i + 0.5) * binHz));
            double db = powers[i];
            // Live spectrum bins are display data, not discrete active-channel detections.
            // Leaving Active=false prevents thousands of orange marker lines from saturating the spectrum panel.
            points.Add(new ScanPoint(freq, db, false));
        }

        return true;
    }

    private static int FindSpectrumPowerStart(string[] values, int fftBins)
    {
        if (fftBins > 0)
        {
            int[] candidates = { 7, 6, 5 };
            foreach (int candidate in candidates)
            {
                int available = values.Length - candidate;
                if (available >= Math.Max(16, fftBins / 2))
                    return candidate;
            }
        }

        for (int i = 5; i < values.Length; i++)
        {
            int numeric = 0;
            for (int j = i; j < values.Length; j++)
            {
                if (double.TryParse(values[j], NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                    numeric++;
            }
            if (numeric >= 16) return i;
        }
        return -1;
    }

    private void StopScanButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _scanLoopCts?.Cancel();
            TryKillProcess(_scanProcess);
            Log("Stop requested by user.");
            StatusText.Text = "Stopped.";
        }
        catch (Exception ex)
        {
            Log("Stop failed: " + ex.Message);
        }
    }

    private void ClearActiveButton_Click(object sender, RoutedEventArgs e)
    {
        ActiveChannels.Clear();
        LastScanPoints.Clear();
        WaterfallHistory.Clear();
        DrawEmptyCharts();
        StatusText.Text = "Active list cleared.";
        Log("Cleared active channel list and chart history.");
    }

    private void OpenLastCsvButton_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_lastScanCsv) && File.Exists(_lastScanCsv))
        {
            Process.Start(new ProcessStartInfo(_lastScanCsv) { UseShellExecute = true });
            return;
        }
        MessageBox.Show("No scan CSV has been created yet.", "No CSV", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private static void TryKillProcess(Process? process)
    {
        try
        {
            if (process != null && !process.HasExited)
                process.Kill(true);
        }
        catch
        {
            // Best effort only; the process may have exited between the check and kill.
        }
    }

    private void ListenButton_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveChannelsGrid.SelectedItem is not ActiveChannel selected)
        {
            MessageBox.Show("Select an active channel first.", "No channel selected", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        _ = ListenAsync(selected);
    }

    private async Task ListenAsync(ActiveChannel selected)
    {
        SaveUiToConfig();
        EnsureSessionsDir();

        string audioExe = FindTool("pluto_audio_monitor.exe");
        if (string.IsNullOrWhiteSpace(audioExe))
        {
            MessageBox.Show("pluto_audio_monitor.exe was not found in bin/. Run ./tools/sync_backend_from_v1_repo.sh or reinstall the new-folder package.", "Audio tool missing", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        int seconds = 30;
        if (!int.TryParse(ListenSecondsText.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out seconds))
            seconds = 30;
        seconds = Math.Clamp(seconds, 1, 3600);

        string mode = NormalizeAudioMode(selected.Mode);
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        string wav = IoPath.Combine(_config.SessionsDir, $"listen_{selected.FrequencyHz}_{timestamp}.wav");
        string csv = IoPath.Combine(_config.SessionsDir, "audio_log.csv");

        var args = new List<string>
        {
            "--uri", Q(UriText.Text.Trim()),
            "--mode", mode,
            "--freq", selected.FrequencyHz.ToString(CultureInfo.InvariantCulture),
            "--rate", RateText.Text.Trim(),
            "--seconds", seconds.ToString(CultureInfo.InvariantCulture),
            "--squelch-db", SquelchText.Text.Trim(),
            "--wav", Q(wav),
            "--csv", Q(csv)
        };

            // Apply selected listen profile after default args so profile args win.
            ApplyListenProfileArguments(args, ref mode);
            LogListenCommandForTroubleshooting("pluto_audio_monitor.exe", args);

        AddOptionalNumericArg(args, "--bw", BwText.Text);

        Log("Recording selected channel audio:");
        Log($"  {audioExe}");
        Log($"  {string.Join(" ", args)}");
        StatusText.Text = $"Recording {seconds}s audio from {selected.FrequencyMhz} MHz...";

        try
        {
            var psi = new ProcessStartInfo(audioExe, string.Join(" ", args))
            {
                WorkingDirectory = _repoRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using var proc = new Process { StartInfo = psi };
            proc.OutputDataReceived += (_, ev) => { if (!string.IsNullOrWhiteSpace(ev.Data)) Dispatcher.Invoke(() => Log(ev.Data)); };
            proc.ErrorDataReceived += (_, ev) => { if (!string.IsNullOrWhiteSpace(ev.Data)) Dispatcher.Invoke(() => Log("ERR: " + ev.Data)); };
            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            await proc.WaitForExitAsync();
            Log($"Audio recorder exited with code {proc.ExitCode}.");
            StatusText.Text = File.Exists(wav) ? $"Audio saved: {IoPath.GetFileName(wav)}" : "Audio recorder finished.";
            if (File.Exists(wav))
            {
                Process.Start(new ProcessStartInfo(wav) { UseShellExecute = true });
            }
        }
        catch (Exception ex)
        {
            Log("Audio record failed: " + ex.Message);
            MessageBox.Show(ex.Message, "Audio record failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ExportChirpButton_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveChannels.Count == 0)
        {
            MessageBox.Show("No active channels to export yet.", "Nothing to export", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        EnsureSessionsDir();
        var dialog = new SaveFileDialog
        {
            Title = "Export CHIRP CSV",
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            FileName = "chirp.csv",
            InitialDirectory = _config.SessionsDir
        };
        if (dialog.ShowDialog(this) != true) return;

        WriteChirpCsv(dialog.FileName, ActiveChannels);
        Log($"CHIRP CSV exported: {dialog.FileName}");
        StatusText.Text = "CHIRP CSV exported.";
    }

    private void WriteChirpCsv(string path, IEnumerable<ActiveChannel> rows)
    {
        string[] header =
        {
            "Location", "Name", "Frequency", "Duplex", "Offset", "Tone", "rToneFreq", "cToneFreq",
            "DtcsCode", "DtcsPolarity", "Mode", "TStep", "Skip", "Comment", "URCALL", "RPT1CALL", "RPT2CALL", "DVCODE"
        };

        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", header));
        int location = 1;
        foreach (var row in rows)
        {
            string name = MakeChirpName(row);
            string freqMhz = (row.FrequencyHz / 1000000.0).ToString("F6", CultureInfo.InvariantCulture);
            string mode = ToChirpMode(row.Mode);
            string comment = $"Pluto GUI v2 detected {row.CombinedDbfs:F1} dBFS; {row.Label}";
            string[] values =
            {
                location.ToString(CultureInfo.InvariantCulture),
                name,
                freqMhz,
                string.Empty,
                "0.000000",
                string.Empty,
                "88.5",
                "88.5",
                "023",
                "NN",
                mode,
                "5.00",
                string.Empty,
                comment,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty
            };
            sb.AppendLine(string.Join(",", values.Select(CsvEscape)));
            location++;
        }
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }

    private static string MakeChirpName(ActiveChannel row)
    {
        string label = string.IsNullOrWhiteSpace(row.Label) ? $"{row.FrequencyHz / 1000000.0:F4}" : row.Label;
        var cleaned = new string(label.Where(ch => char.IsLetterOrDigit(ch) || ch == ' ' || ch == '-' || ch == '_').ToArray()).Trim();
        if (cleaned.Length == 0) cleaned = $"F{row.FrequencyHz / 1000}";
        return cleaned.Length <= 16 ? cleaned : cleaned[..16];
    }

    private void ParseScanCsv(string csvPath)
    {
        var lines = File.ReadAllLines(csvPath);
        if (lines.Length == 0) return;

        string[] header = SplitCsv(lines[0]);
        var index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < header.Length; i++) index[header[i].Trim()] = i;

        LastScanPoints.Clear();
        string selectedMode = SelectedBandMode();
        DateTime seenAt = DateTime.Now;
        int activeThisPass = 0;

        foreach (string rawLine in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(rawLine) || rawLine.TrimStart().StartsWith("#", StringComparison.Ordinal)) continue;
            string[] values = SplitCsv(rawLine);
            long freq = GetLong(values, index, "frequency_hz");
            if (freq <= 0) continue;
            string label = Get(values, index, "label");
            double combined = GetDouble(values, index, "combined_dbfs");
            double rx1 = GetDouble(values, index, "rx1_dbfs");
            double rx2 = GetDouble(values, index, "rx2_dbfs");
            double threshold = GetDouble(values, index, "threshold_dbfs");
            string active = Get(values, index, "active");
            string effectiveRx = Get(values, index, "effective_rx_mode");
            string rxCombine = Get(values, index, "rx_combine");

            var point = new ScanPoint(freq, combined, active.Equals("yes", StringComparison.OrdinalIgnoreCase));
            LastScanPoints.Add(point);

            if (!point.Active) continue;
            activeThisPass++;
            UpsertActiveChannel(new ActiveChannel
            {
                FrequencyHz = freq,
                Label = label,
                Mode = selectedMode,
                EffectiveRxMode = effectiveRx,
                RxCombine = rxCombine,
                Rx1Dbfs = rx1,
                Rx2Dbfs = rx2,
                CombinedDbfs = combined,
                LastDbfs = combined,
                PeakDbfs = combined,
                ThresholdDbfs = threshold,
                Active = true,
                DetectedCount = 1,
                FirstSeen = seenAt,
                LastSeen = seenAt,
                Comment = IoPath.GetFileName(csvPath)
            });
        }

        if (LastScanPoints.Count > 0)
        {
            WaterfallHistory.Add(LastScanPoints.ToList());
            while (WaterfallHistory.Count > MaxWaterfallRows) WaterfallHistory.RemoveAt(0);
        }

        DrawSpectrum();
        DrawWaterfall();
        ActiveChannelsGrid.Items.Refresh();
        Log($"Parsed {LastScanPoints.Count} scan rows; active this pass: {activeThisPass}; total unique active: {ActiveChannels.Count}.");
    }

    private void UpsertActiveChannel(ActiveChannel incoming)
    {
        var existing = ActiveChannels.FirstOrDefault(c => c.FrequencyHz == incoming.FrequencyHz);
        if (existing == null)
        {
            incoming.PeakDbfs = double.IsNaN(incoming.PeakDbfs) ? incoming.CombinedDbfs : incoming.PeakDbfs;
            incoming.LastDbfs = incoming.CombinedDbfs;
            ActiveChannels.Add(incoming);
            return;
        }

        if (!string.IsNullOrWhiteSpace(incoming.Label)) existing.Label = incoming.Label;
        existing.Mode = incoming.Mode;
        existing.EffectiveRxMode = incoming.EffectiveRxMode;
        existing.RxCombine = incoming.RxCombine;
        existing.Rx1Dbfs = incoming.Rx1Dbfs;
        existing.Rx2Dbfs = incoming.Rx2Dbfs;
        existing.CombinedDbfs = incoming.CombinedDbfs;
        existing.LastDbfs = incoming.CombinedDbfs;
        if (double.IsNaN(existing.PeakDbfs) || incoming.CombinedDbfs > existing.PeakDbfs)
            existing.PeakDbfs = incoming.CombinedDbfs;
        existing.ThresholdDbfs = incoming.ThresholdDbfs;
        existing.Active = true;
        existing.DetectedCount += 1;
        if (existing.FirstSeen == default) existing.FirstSeen = incoming.FirstSeen;
        existing.LastSeen = incoming.LastSeen;
        existing.Comment = incoming.Comment;
    }

    private void LoadBands()
    {
        Bands.Clear();
        BandCombo.ItemsSource = null;

        string path = BandsCsvText.Text.Trim();
        if (string.IsNullOrWhiteSpace(path))
            path = IoPath.Combine(_repoRoot, "configs", "bands.csv");
        if (!File.Exists(path))
            path = IoPath.Combine(_repoRoot, "configs", "bands_v2_default.csv");

        if (!File.Exists(path))
        {
            Log("No bands.csv found. Standard Band mode will be empty until configs\\bands.csv is added.");
            return;
        }

        try
        {
            string[] lines = File.ReadAllLines(path);
            if (lines.Length == 0) return;
            int headerIndex = Array.FindIndex(lines, l => !string.IsNullOrWhiteSpace(l) && !l.TrimStart().StartsWith("#"));
            if (headerIndex < 0) return;
            string[] header = SplitCsv(lines[headerIndex]);
            var index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < header.Length; i++) index[header[i].Trim()] = i;

            foreach (string rawLine in lines.Skip(headerIndex + 1))
            {
                if (string.IsNullOrWhiteSpace(rawLine) || rawLine.TrimStart().StartsWith("#", StringComparison.Ordinal)) continue;
                string[] values = SplitCsv(rawLine);
                var band = new BandDefinition
                {
                    Id = Get(values, index, "id"),
                    Name = Get(values, index, "name"),
                    StartHz = GetLong(values, index, "start_hz"),
                    StopHz = GetLong(values, index, "stop_hz"),
                    StepHz = Math.Max(1, GetLong(values, index, "step_hz")),
                    Mode = Get(values, index, "mode"),
                    RateHz = NormalizeSampleRateHz(GetLong(values, index, "rate_hz")),
                    BandwidthHz = GetLong(values, index, "bw_hz"),
                    SquelchDb = GetDouble(values, index, "squelch_db"),
                    Comment = Get(values, index, "notes")
                };
                if (band.StartHz > 0 && band.StopHz >= band.StartHz && !string.IsNullOrWhiteSpace(band.Name))
                    Bands.Add(band);
            }

            BandCombo.ItemsSource = Bands;
            if (Bands.Count > 0) BandCombo.SelectedIndex = 0;
            Log($"Loaded {Bands.Count} band definitions from {path}");
        }
        catch (Exception ex)
        {
            Log("Failed to load bands: " + ex.Message);
        }
    }

    private void BandCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (BandCombo.SelectedItem is not BandDefinition band) return;
        StartFreqText.Text = band.StartHz.ToString(CultureInfo.InvariantCulture);
        StopFreqText.Text = band.StopHz.ToString(CultureInfo.InvariantCulture);
        StepHzText.Text = band.StepHz.ToString(CultureInfo.InvariantCulture);
        if (band.RateHz > 0) RateText.Text = NormalizeSampleRateHz(band.RateHz).ToString(CultureInfo.InvariantCulture);
        if (band.BandwidthHz > 0) BwText.Text = band.BandwidthHz.ToString(CultureInfo.InvariantCulture);
        if (!double.IsNaN(band.SquelchDb) && band.SquelchDb != 0) SquelchText.Text = band.SquelchDb.ToString(CultureInfo.InvariantCulture);
    }

    private void ScanModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // The fields remain visible intentionally; it makes it easier to copy band values into a range scan.
    }

    private void ChartCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        DrawSpectrum();
        DrawWaterfall();
    }

    private void DrawEmptyCharts()
    {
        SpectrumCanvas.Children.Clear();
        WaterfallCanvas.Children.Clear();
    }

    private void DrawSpectrum()
    {
        SpectrumCanvas.Children.Clear();
        if (LastScanPoints.Count == 0)
        {
            SpectrumCaption.Visibility = Visibility.Visible;
            return;
        }
        SpectrumCaption.Visibility = Visibility.Collapsed;

        double w = Math.Max(1, SpectrumCanvas.ActualWidth);
        double h = Math.Max(1, SpectrumCanvas.ActualHeight);
        DrawGrid(SpectrumCanvas, w, h);

        GetDbScale(LastScanPoints.Select(p => p.Dbfs), out double minDb, out double maxDb);
        double minFreq = LastScanPoints.Min(p => (double)p.FrequencyHz);
        double maxFreq = LastScanPoints.Max(p => (double)p.FrequencyHz);
        if (Math.Abs(maxFreq - minFreq) < 1) maxFreq = minFreq + 1;

        var line = new Polyline
        {
            Stroke = Brushes.DeepSkyBlue,
            StrokeThickness = 2
        };
        foreach (var p in LastScanPoints.OrderBy(p => p.FrequencyHz))
        {
            double x = (p.FrequencyHz - minFreq) / (maxFreq - minFreq) * w;
            double y = h - ((Clamp(p.Dbfs, minDb, maxDb) - minDb) / (maxDb - minDb) * h);
            line.Points.Add(new Point(x, y));
        }
        SpectrumCanvas.Children.Add(line);

        // Show active-channel markers for scanner results, but avoid drawing hundreds/thousands
        // of markers for dense live FFT displays.
        if (LastScanPoints.Count <= 300)
        {
            foreach (var p in LastScanPoints.Where(p => p.Active))
            {
                double x = (p.FrequencyHz - minFreq) / (maxFreq - minFreq) * w;
                var marker = new Line { X1 = x, X2 = x, Y1 = 0, Y2 = h, Stroke = Brushes.Orange, StrokeThickness = 1.5, Opacity = 0.85 };
                SpectrumCanvas.Children.Add(marker);
            }
        }

        AddCanvasText(SpectrumCanvas, $"{minFreq / 1000000.0:F3} MHz", 6, h - 20);
        AddCanvasText(SpectrumCanvas, $"{maxFreq / 1000000.0:F3} MHz", Math.Max(6, w - 90), h - 20);
    }

    private void DrawWaterfall()
    {
        WaterfallCanvas.Children.Clear();
        if (WaterfallHistory.Count == 0)
        {
            WaterfallCaption.Visibility = Visibility.Visible;
            return;
        }
        WaterfallCaption.Visibility = Visibility.Collapsed;

        double canvasW = Math.Max(1, WaterfallCanvas.ActualWidth);
        double canvasH = Math.Max(1, WaterfallCanvas.ActualHeight);
        int pixelW = Math.Max(1, Math.Min(MaxWaterfallBitmapWidth, (int)Math.Ceiling(canvasW)));
        int pixelH = Math.Max(1, Math.Min(MaxWaterfallBitmapHeight, (int)Math.Ceiling(canvasH)));

        GetDbScale(WaterfallHistory.SelectMany(row => row.Select(p => p.Dbfs)), out double minDb, out double maxDb);

        byte[] pixels = new byte[pixelW * pixelH * 4];
        int rows = WaterfallHistory.Count;
        for (int y = 0; y < pixelH; y++)
        {
            int rowIndex = Math.Clamp((int)Math.Floor((double)y / pixelH * rows), 0, rows - 1);
            var sweep = WaterfallHistory[rowIndex].OrderBy(p => p.FrequencyHz).ToList();
            if (sweep.Count == 0) continue;

            for (int x = 0; x < pixelW; x++)
            {
                int binIndex = Math.Clamp((int)Math.Floor((double)x / pixelW * sweep.Count), 0, sweep.Count - 1);
                Color color = DbToWaterfallColor(sweep[binIndex].Dbfs, minDb, maxDb);
                int offset = ((y * pixelW) + x) * 4;
                pixels[offset + 0] = color.B;
                pixels[offset + 1] = color.G;
                pixels[offset + 2] = color.R;
                pixels[offset + 3] = 255;
            }
        }

        var bitmap = new WriteableBitmap(pixelW, pixelH, 96, 96, PixelFormats.Pbgra32, null);
        bitmap.WritePixels(new Int32Rect(0, 0, pixelW, pixelH), pixels, pixelW * 4, 0);

        var image = new Image
        {
            Source = bitmap,
            Width = canvasW,
            Height = canvasH,
            Stretch = System.Windows.Media.Stretch.Fill,
            SnapsToDevicePixels = true
        };
        Canvas.SetLeft(image, 0);
        Canvas.SetTop(image, 0);
        WaterfallCanvas.Children.Add(image);

        AddCanvasText(WaterfallCanvas, $"{minDb:F1} to {maxDb:F1} dBFS", 6, 6);
    }

    private static void DrawGrid(Canvas canvas, double w, double h)
    {
        for (int i = 1; i < 5; i++)
        {
            double y = h * i / 5.0;
            canvas.Children.Add(new Line { X1 = 0, X2 = w, Y1 = y, Y2 = y, Stroke = Brushes.DimGray, StrokeThickness = 0.5, Opacity = 0.6 });
        }
        for (int i = 1; i < 6; i++)
        {
            double x = w * i / 6.0;
            canvas.Children.Add(new Line { X1 = x, X2 = x, Y1 = 0, Y2 = h, Stroke = Brushes.DimGray, StrokeThickness = 0.5, Opacity = 0.45 });
        }
    }

    private static void AddCanvasText(Canvas canvas, string text, double x, double y)
    {
        var tb = new TextBlock { Text = text, Foreground = Brushes.LightGray, FontSize = 11 };
        Canvas.SetLeft(tb, x);
        Canvas.SetTop(tb, y);
        canvas.Children.Add(tb);
    }

    private static Color DbToWaterfallColor(double db, double minDb, double maxDb)
    {
        double span = Math.Max(1.0, maxDb - minDb);
        double t = (Clamp(db, minDb, maxDb) - minDb) / span;

        // Dark blue/green background, yellow/orange for stronger signals. The scaling is
        // dynamic per waterfall history so a normal noise floor does not render as solid red.
        byte r = (byte)(Math.Pow(t, 1.35) * 255);
        byte g = (byte)(Math.Sin(t * Math.PI) * 210);
        byte b = (byte)((1.0 - t) * 160 + 20);
        return Color.FromRgb(r, g, b);
    }

    private static void GetDbScale(IEnumerable<double> samples, out double minDb, out double maxDb)
    {
        var values = samples
            .Where(v => !double.IsNaN(v) && !double.IsInfinity(v))
            .OrderBy(v => v)
            .ToList();

        if (values.Count == 0)
        {
            minDb = -120;
            maxDb = -40;
            return;
        }

        minDb = Percentile(values, 0.05);
        maxDb = Percentile(values, 0.98);

        if (maxDb - minDb < 12)
        {
            double mid = (minDb + maxDb) / 2.0;
            minDb = mid - 8;
            maxDb = mid + 8;
        }

        // Keep extreme one-off values from making the display useless.
        minDb = Math.Max(-160, minDb);
        maxDb = Math.Min(40, maxDb);
    }

    private static double Percentile(IReadOnlyList<double> sortedValues, double percentile)
    {
        if (sortedValues.Count == 0) return 0;
        if (sortedValues.Count == 1) return sortedValues[0];
        double pos = Math.Clamp(percentile, 0.0, 1.0) * (sortedValues.Count - 1);
        int lo = (int)Math.Floor(pos);
        int hi = (int)Math.Ceiling(pos);
        if (lo == hi) return sortedValues[lo];
        double frac = pos - lo;
        return sortedValues[lo] + ((sortedValues[hi] - sortedValues[lo]) * frac);
    }

    private void SaveConfigButton_Click(object sender, RoutedEventArgs e)
    {
        SaveUiToConfig();
        SaveConfig();
        StatusText.Text = "Configuration saved.";
    }

    private void ReloadBandsButton_Click(object sender, RoutedEventArgs e)
    {
        SaveUiToConfig();
        LoadBands();
    }

    private void ResetConfigButton_Click(object sender, RoutedEventArgs e)
    {
        _config = new AppConfig { RepoRoot = _repoRoot, SessionsDir = IoPath.Combine(_repoRoot, "sessions"), BandsCsvPath = IoPath.Combine(_repoRoot, "configs", "bands.csv") };
        ApplyConfigToUi();
        SaveConfig();
        LoadBands();
        StatusText.Text = "Defaults restored.";
    }

    private void OpenSessionsButton_Click(object sender, RoutedEventArgs e)
    {
        EnsureSessionsDir();
        Process.Start(new ProcessStartInfo(_config.SessionsDir) { UseShellExecute = true });
    }

    private void OpenConfigButton_Click(object sender, RoutedEventArgs e)
    {
        string dir = IoPath.Combine(_repoRoot, "configs");
        Directory.CreateDirectory(dir);
        Process.Start(new ProcessStartInfo(dir) { UseShellExecute = true });
    }

    private void LoadConfig()
    {
        string path = ConfigPath();
        _config = new AppConfig
        {
            RepoRoot = _repoRoot,
            SessionsDir = IoPath.Combine(_repoRoot, "sessions"),
            BandsCsvPath = IoPath.Combine(_repoRoot, "configs", "bands.csv")
        };

        try
        {
            if (File.Exists(path))
            {
                var loaded = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(path));
                if (loaded != null) _config = loaded;
            }
        }
        catch (Exception ex)
        {
            Log("Could not load GUI config; using defaults: " + ex.Message);
        }

        if (string.IsNullOrWhiteSpace(_config.RepoRoot)) _config.RepoRoot = _repoRoot;
        if (string.IsNullOrWhiteSpace(_config.SessionsDir)) _config.SessionsDir = IoPath.Combine(_repoRoot, "sessions");
        if (string.IsNullOrWhiteSpace(_config.BandsCsvPath)) _config.BandsCsvPath = IoPath.Combine(_repoRoot, "configs", "bands.csv");
    }

    private void SaveConfig()
    {
        try
        {
            Directory.CreateDirectory(IoPath.GetDirectoryName(ConfigPath())!);
            File.WriteAllText(ConfigPath(), JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true }));
            Log($"Config saved: {ConfigPath()}");
        }
        catch (Exception ex)
        {
            Log("Config save failed: " + ex.Message);
        }
    }

    private void ApplyConfigToUi()
    {
        UriText.Text = _config.Uri;
        RateText.Text = _config.RateHz.ToString(CultureInfo.InvariantCulture);
        BwText.Text = _config.BandwidthHz.ToString(CultureInfo.InvariantCulture);
        SquelchText.Text = _config.SquelchDb.ToString(CultureInfo.InvariantCulture);
        SessionsDirText.Text = _config.SessionsDir;
        BandsCsvText.Text = _config.BandsCsvPath;
        ListenSecondsText.Text = _config.ListenSeconds.ToString(CultureInfo.InvariantCulture);
        DefaultChirpModeText.Text = _config.DefaultChirpMode;
        RepeatScanCheck.IsChecked = _config.RepeatScan;
        RepeatDelayText.Text = _config.RepeatDelaySeconds.ToString(CultureInfo.InvariantCulture);
        LiveCenterFreqText.Text = _config.LiveCenterHz.ToString(CultureInfo.InvariantCulture);
        LiveFftText.Text = _config.LiveFftSize.ToString(CultureInfo.InvariantCulture);
        LiveAvgText.Text = _config.LiveAverages.ToString(CultureInfo.InvariantCulture);
        LiveIntervalText.Text = _config.LiveIntervalMs.ToString(CultureInfo.InvariantCulture);
        LiveGainDbText.Text = _config.LiveGainDb;
        SelectComboValue(RxModeCombo, _config.RxMode);
        SelectComboValue(RxCombineCombo, _config.RxCombine);
        SelectComboValue(LiveGainModeCombo, _config.LiveGainMode);
    }

    private void SaveUiToConfig()
    {
        _config.Uri = UriText.Text.Trim();
        _config.RateHz = GetSafeRateHzFromUi();
        _config.BandwidthHz = ParseLongOrDefault(BwText.Text, 1000000);
        _config.SquelchDb = ParseDoubleOrDefault(SquelchText.Text, -65);
        _config.RxMode = ComboText(RxModeCombo);
        _config.RxCombine = ComboText(RxCombineCombo);
        _config.RepoRoot = _repoRoot;
        _config.SessionsDir = string.IsNullOrWhiteSpace(SessionsDirText.Text) ? IoPath.Combine(_repoRoot, "sessions") : ExpandPath(SessionsDirText.Text.Trim());
        _config.BandsCsvPath = string.IsNullOrWhiteSpace(BandsCsvText.Text) ? IoPath.Combine(_repoRoot, "configs", "bands.csv") : ExpandPath(BandsCsvText.Text.Trim());
        _config.ListenSeconds = (int)Math.Clamp(ParseLongOrDefault(ListenSecondsText.Text, 30), 1, 3600);
        _config.DefaultChirpMode = string.IsNullOrWhiteSpace(DefaultChirpModeText.Text) ? "NFM" : DefaultChirpModeText.Text.Trim();
        _config.RepeatScan = RepeatScanCheck.IsChecked == true;
        _config.RepeatDelaySeconds = (int)Math.Clamp(ParseLongOrDefault(RepeatDelayText.Text, 2), 0, 3600);
        _config.LiveCenterHz = ParseLongOrDefault(LiveCenterFreqText.Text, 162550000);
        _config.LiveFftSize = (int)Math.Clamp(ParseLongOrDefault(LiveFftText.Text, 512), 256, 65536);
        _config.LiveAverages = (int)Math.Clamp(ParseLongOrDefault(LiveAvgText.Text, 2), 1, 1000);
        _config.LiveIntervalMs = (int)Math.Clamp(ParseLongOrDefault(LiveIntervalText.Text, 500), 0, 60000);
        _config.LiveGainMode = ComboText(LiveGainModeCombo);
        _config.LiveGainDb = LiveGainDbText.Text.Trim();
    }

    private long GetSafeRateHzFromUi()
    {
        long requested = ParseLongOrDefault(RateText.Text, DefaultSampleRateHz);
        long safe = NormalizeSampleRateHz(requested);
        if (safe != requested)
        {
            RateText.Text = safe.ToString(CultureInfo.InvariantCulture);
            Log($"Sample rate {requested} Hz is not used because prior Pluto testing showed it can fail with ret=-22. Using {safe} Hz.");
        }
        return safe;
    }

    private static long NormalizeSampleRateHz(long rateHz)
    {
        if (rateHz == KnownInvalidSampleRateHz || rateHz <= 0)
            return DefaultSampleRateHz;
        return rateHz;
    }

    private string ExpandPath(string path)
    {
        return IoPath.IsPathRooted(path) ? path : IoPath.GetFullPath(IoPath.Combine(_repoRoot, path));
    }

    private void EnsureSessionsDir()
    {
        SaveUiToConfig();
        Directory.CreateDirectory(_config.SessionsDir);
    }

    private string ConfigPath() => IoPath.Combine(_repoRoot, "configs", "gui_v2_settings.json");

    private string FindTool(string exeName)
    {
        var candidates = new[]
        {
            IoPath.Combine(_repoRoot, "build", "native", exeName),
            IoPath.Combine(_repoRoot, "bin", exeName),
            IoPath.Combine(_repoRoot, "bin", "native", exeName),
            IoPath.Combine(AppContext.BaseDirectory, exeName),
            IoPath.Combine(AppContext.BaseDirectory, "bin", exeName),
        };
        return candidates.FirstOrDefault(File.Exists) ?? string.Empty;
    }

    private static string FindRepoRoot()
    {
        var starts = new[] { Environment.CurrentDirectory, AppContext.BaseDirectory };
        foreach (string start in starts)
        {
            var dir = new DirectoryInfo(start);
            for (int i = 0; dir != null && i < 10; i++, dir = dir.Parent)
            {
                if (File.Exists(IoPath.Combine(dir.FullName, ".pluto_windows_scanner_root")))
                    return dir.FullName;

                bool looksLikeRepo = Directory.Exists(IoPath.Combine(dir.FullName, "configs")) &&
                                     (Directory.Exists(IoPath.Combine(dir.FullName, "build")) || Directory.Exists(IoPath.Combine(dir.FullName, "bin")) || Directory.Exists(IoPath.Combine(dir.FullName, "launchers")));
                if (looksLikeRepo) return dir.FullName;
            }
        }
        return Environment.CurrentDirectory;
    }

    private static string ComboText(ComboBox combo)
    {
        return combo.SelectedItem is ComboBoxItem item ? item.Content?.ToString() ?? string.Empty : combo.Text;
    }

    private static void SelectComboValue(ComboBox combo, string value)
    {
        foreach (var item in combo.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Content?.ToString(), value, StringComparison.OrdinalIgnoreCase))
            {
                combo.SelectedItem = item;
                return;
            }
        }
    }

    private string SelectedBandMode()
    {
        if (BandCombo.SelectedItem is BandDefinition band && !string.IsNullOrWhiteSpace(band.Mode))
            return band.Mode;
        return DefaultChirpModeText.Text.Trim();
    }

    private static string NormalizeAudioMode(string mode)
    {
        mode = (mode ?? string.Empty).Trim().ToLowerInvariant();
        if (mode is "am" or "wbfm" or "nfm") return mode;
        if (mode.Contains("air")) return "am";
        if (mode.Contains("fm") && !mode.Contains("wbfm")) return "nfm";
        return "nfm";
    }

    private string ToChirpMode(string mode)
    {
        mode = (mode ?? string.Empty).Trim().ToLowerInvariant();
        if (mode == "am") return "AM";
        if (mode == "wbfm") return "FM";
        if (mode == "nfm") return "NFM";
        return string.IsNullOrWhiteSpace(_config.DefaultChirpMode) ? "NFM" : _config.DefaultChirpMode;
    }

    private void Log(string message)
    {
        LogText.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        LogText.ScrollToEnd();
    }

    private static long ParseLong(string value, string name)
    {
        if (!long.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long result))
            throw new InvalidOperationException($"Invalid {name}: {value}");
        return result;
    }

    private static long ParseLongOrDefault(string value, long fallback)
    {
        return long.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long result) ? result : fallback;
    }

    private static double ParseDoubleOrDefault(string value, double fallback)
    {
        return double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double result) ? result : fallback;
    }

    private static string Q(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";

    private static string CsvEscape(string value)
    {
        value ??= string.Empty;
        return value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r')
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
    }

    private static string[] SplitCsv(string line)
    {
        var values = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;
        for (int i = 0; i < line.Length; i++)
        {
            char ch = line[i];
            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    sb.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (ch == ',' && !inQuotes)
            {
                values.Add(sb.ToString());
                sb.Clear();
            }
            else
            {
                sb.Append(ch);
            }
        }
        values.Add(sb.ToString());
        return values.ToArray();
    }

    private static string Get(string[] values, Dictionary<string, int> index, string name)
    {
        return index.TryGetValue(name, out int i) && i >= 0 && i < values.Length ? values[i].Trim() : string.Empty;
    }

    private static long GetLong(string[] values, Dictionary<string, int> index, string name)
    {
        return long.TryParse(Get(values, index, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out long result) ? result : 0;
    }

    private static double GetDouble(string[] values, Dictionary<string, int> index, string name)
    {
        return double.TryParse(Get(values, index, name), NumberStyles.Float, CultureInfo.InvariantCulture, out double result) ? result : double.NaN;
    }

    private static double Clamp(double value, double min, double max) => Math.Max(min, Math.Min(max, value));
}

public sealed class AppConfig
{
    public string RepoRoot { get; set; } = string.Empty;
    public string Uri { get; set; } = "ip:192.168.2.1";
    public long RateHz { get; set; } = 1000000;
    public long BandwidthHz { get; set; } = 1000000;
    public double SquelchDb { get; set; } = -65;
    public string RxMode { get; set; } = "auto";
    public string RxCombine { get; set; } = "max";
    public string SessionsDir { get; set; } = "sessions";
    public string BandsCsvPath { get; set; } = "configs/bands.csv";
    public int ListenSeconds { get; set; } = 30;
    public string DefaultChirpMode { get; set; } = "NFM";
    public bool RepeatScan { get; set; } = false;
    public int RepeatDelaySeconds { get; set; } = 2;
    public long LiveCenterHz { get; set; } = 162550000;
    public int LiveFftSize { get; set; } = 512;
    public int LiveAverages { get; set; } = 2;
    public int LiveIntervalMs { get; set; } = 500;
    public string LiveGainMode { get; set; } = "slow_attack";
    public string LiveGainDb { get; set; } = string.Empty;
}

public sealed class BandDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public long StartHz { get; set; }
    public long StopHz { get; set; }
    public long StepHz { get; set; } = 25000;
    public string Mode { get; set; } = "nfm";
    public long RateHz { get; set; } = 1000000;
    public long BandwidthHz { get; set; } = 1000000;
    public double SquelchDb { get; set; } = -65;
    public string Comment { get; set; } = string.Empty;
    public override string ToString() => Name;
}

public sealed class ActiveChannel : INotifyPropertyChanged
{
    public long FrequencyHz { get; set; }
    public string FrequencyMhz => (FrequencyHz / 1000000.0).ToString("F6", CultureInfo.InvariantCulture);
    public string Label { get; set; } = string.Empty;
    public string Mode { get; set; } = "nfm";
    public string EffectiveRxMode { get; set; } = string.Empty;
    public string RxCombine { get; set; } = string.Empty;
    public double Rx1Dbfs { get; set; }
    public double Rx2Dbfs { get; set; }
    public double CombinedDbfs { get; set; }
    public double LastDbfs { get; set; }
    public double PeakDbfs { get; set; }
    public double ThresholdDbfs { get; set; }
    public int DetectedCount { get; set; }
    public DateTime FirstSeen { get; set; }
    public DateTime LastSeen { get; set; }
    public string FirstSeenDisplay => FirstSeen == default ? string.Empty : FirstSeen.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
    public string LastSeenDisplay => LastSeen == default ? string.Empty : LastSeen.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
    public bool Active { get; set; }
    public string Comment { get; set; } = string.Empty;
    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed record ScanPoint(long FrequencyHz, double Dbfs, bool Active);
