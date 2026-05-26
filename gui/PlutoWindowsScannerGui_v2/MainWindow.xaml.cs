using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace PlutoWindowsScannerGui;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<ActiveChannel> ActiveChannels = new();
    private readonly List<BandDefinition> Bands = new();
    private readonly List<List<ScanPoint>> WaterfallHistory = new();
    private readonly List<ScanPoint> LastScanPoints = new();
    private Process? _scanProcess;
    private CancellationTokenSource? _scanLoopCts;
    private bool _scanLoopActive;
    private int _scanNumber;
    private AppConfig _config = new();
    private string _repoRoot = string.Empty;
    private string _lastScanCsv = string.Empty;
    private const int MaxWaterfallRows = 120;

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
        Log("Windows Pluto SDR Scanner GUI v2.0 loaded.");
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
                _lastScanCsv = System.IO.Path.Combine(_config.SessionsDir, $"gui_scan_{timestamp}_{_scanNumber:000}.csv");

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

        AddOptionalNumericArg(args, "--rate", RateText.Text);
        AddOptionalNumericArg(args, "--bw", BwText.Text);

        string mode = ComboText(ScanModeCombo);
        if (mode.Equals("Single Frequency", StringComparison.OrdinalIgnoreCase))
        {
            long freq = ParseLong(SingleFreqText.Text, "single frequency Hz");
            string freqFile = System.IO.Path.Combine(_config.SessionsDir, "gui_single_frequency.csv");
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
        string wav = System.IO.Path.Combine(_config.SessionsDir, $"listen_{selected.FrequencyHz}_{timestamp}.wav");
        string csv = System.IO.Path.Combine(_config.SessionsDir, "audio_log.csv");

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
            StatusText.Text = File.Exists(wav) ? $"Audio saved: {System.IO.Path.GetFileName(wav)}" : "Audio recorder finished.";
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
                Comment = System.IO.Path.GetFileName(csvPath)
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
            path = System.IO.Path.Combine(_repoRoot, "configs", "bands.csv");
        if (!File.Exists(path))
            path = System.IO.Path.Combine(_repoRoot, "configs", "bands_v2_default.csv");

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
                    RateHz = GetLong(values, index, "rate_hz"),
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
        if (band.RateHz > 0) RateText.Text = band.RateHz.ToString(CultureInfo.InvariantCulture);
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

        double minDb = -120;
        double maxDb = -20;
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

        foreach (var p in LastScanPoints.Where(p => p.Active))
        {
            double x = (p.FrequencyHz - minFreq) / (maxFreq - minFreq) * w;
            var marker = new Line { X1 = x, X2 = x, Y1 = 0, Y2 = h, Stroke = Brushes.Orange, StrokeThickness = 1.5, Opacity = 0.85 };
            SpectrumCanvas.Children.Add(marker);
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

        double w = Math.Max(1, WaterfallCanvas.ActualWidth);
        double h = Math.Max(1, WaterfallCanvas.ActualHeight);
        int rows = WaterfallHistory.Count;
        double rowHeight = Math.Max(2, h / Math.Max(1, rows));

        for (int r = 0; r < rows; r++)
        {
            var sweep = WaterfallHistory[r].OrderBy(p => p.FrequencyHz).ToList();
            if (sweep.Count == 0) continue;
            double cellWidth = Math.Max(1, w / sweep.Count);
            double y = h - ((rows - r) * rowHeight);
            for (int c = 0; c < sweep.Count; c++)
            {
                var p = sweep[c];
                var rect = new Rectangle
                {
                    Width = Math.Ceiling(cellWidth) + 1,
                    Height = Math.Ceiling(rowHeight) + 1,
                    Fill = new SolidColorBrush(DbToWaterfallColor(p.Dbfs))
                };
                Canvas.SetLeft(rect, c * cellWidth);
                Canvas.SetTop(rect, y);
                WaterfallCanvas.Children.Add(rect);
            }
        }
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

    private static Color DbToWaterfallColor(double db)
    {
        double t = (Clamp(db, -110, -25) + 110) / 85.0;
        byte r = (byte)(Math.Max(0, t - 0.45) / 0.55 * 255);
        byte g = (byte)(Math.Sin(t * Math.PI) * 210);
        byte b = (byte)((1.0 - t) * 170 + 25);
        return Color.FromRgb(r, g, b);
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
        _config = new AppConfig { RepoRoot = _repoRoot, SessionsDir = System.IO.Path.Combine(_repoRoot, "sessions"), BandsCsvPath = System.IO.Path.Combine(_repoRoot, "configs", "bands.csv") };
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
        string dir = System.IO.Path.Combine(_repoRoot, "configs");
        Directory.CreateDirectory(dir);
        Process.Start(new ProcessStartInfo(dir) { UseShellExecute = true });
    }

    private void LoadConfig()
    {
        string path = ConfigPath();
        _config = new AppConfig
        {
            RepoRoot = _repoRoot,
            SessionsDir = System.IO.Path.Combine(_repoRoot, "sessions"),
            BandsCsvPath = System.IO.Path.Combine(_repoRoot, "configs", "bands.csv")
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
        if (string.IsNullOrWhiteSpace(_config.SessionsDir)) _config.SessionsDir = System.IO.Path.Combine(_repoRoot, "sessions");
        if (string.IsNullOrWhiteSpace(_config.BandsCsvPath)) _config.BandsCsvPath = System.IO.Path.Combine(_repoRoot, "configs", "bands.csv");
    }

    private void SaveConfig()
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(ConfigPath())!);
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
        SelectComboValue(RxModeCombo, _config.RxMode);
        SelectComboValue(RxCombineCombo, _config.RxCombine);
    }

    private void SaveUiToConfig()
    {
        _config.Uri = UriText.Text.Trim();
        _config.RateHz = ParseLongOrDefault(RateText.Text, 960000);
        _config.BandwidthHz = ParseLongOrDefault(BwText.Text, 1000000);
        _config.SquelchDb = ParseDoubleOrDefault(SquelchText.Text, -65);
        _config.RxMode = ComboText(RxModeCombo);
        _config.RxCombine = ComboText(RxCombineCombo);
        _config.RepoRoot = _repoRoot;
        _config.SessionsDir = string.IsNullOrWhiteSpace(SessionsDirText.Text) ? System.IO.Path.Combine(_repoRoot, "sessions") : ExpandPath(SessionsDirText.Text.Trim());
        _config.BandsCsvPath = string.IsNullOrWhiteSpace(BandsCsvText.Text) ? System.IO.Path.Combine(_repoRoot, "configs", "bands.csv") : ExpandPath(BandsCsvText.Text.Trim());
        _config.ListenSeconds = (int)Math.Clamp(ParseLongOrDefault(ListenSecondsText.Text, 30), 1, 3600);
        _config.DefaultChirpMode = string.IsNullOrWhiteSpace(DefaultChirpModeText.Text) ? "NFM" : DefaultChirpModeText.Text.Trim();
        _config.RepeatScan = RepeatScanCheck.IsChecked == true;
        _config.RepeatDelaySeconds = (int)Math.Clamp(ParseLongOrDefault(RepeatDelayText.Text, 2), 0, 3600);
    }

    private string ExpandPath(string path)
    {
        return System.IO.Path.IsPathRooted(path) ? path : System.IO.Path.GetFullPath(System.IO.Path.Combine(_repoRoot, path));
    }

    private void EnsureSessionsDir()
    {
        SaveUiToConfig();
        Directory.CreateDirectory(_config.SessionsDir);
    }

    private string ConfigPath() => System.IO.Path.Combine(_repoRoot, "configs", "gui_v2_settings.json");

    private string FindTool(string exeName)
    {
        var candidates = new[]
        {
            System.IO.Path.Combine(_repoRoot, "build", "native", exeName),
            System.IO.Path.Combine(_repoRoot, "bin", exeName),
            System.IO.Path.Combine(_repoRoot, "bin", "native", exeName),
            System.IO.Path.Combine(AppContext.BaseDirectory, exeName),
            System.IO.Path.Combine(AppContext.BaseDirectory, "bin", exeName),
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
                if (File.Exists(System.IO.Path.Combine(dir.FullName, ".pluto_windows_scanner_root")))
                    return dir.FullName;

                bool looksLikeRepo = Directory.Exists(System.IO.Path.Combine(dir.FullName, "configs")) &&
                                     (Directory.Exists(System.IO.Path.Combine(dir.FullName, "build")) || Directory.Exists(System.IO.Path.Combine(dir.FullName, "bin")) || Directory.Exists(System.IO.Path.Combine(dir.FullName, "launchers")));
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
    public long RateHz { get; set; } = 960000;
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
}

public sealed class BandDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public long StartHz { get; set; }
    public long StopHz { get; set; }
    public long StepHz { get; set; } = 25000;
    public string Mode { get; set; } = "nfm";
    public long RateHz { get; set; } = 960000;
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
