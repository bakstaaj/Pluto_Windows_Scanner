using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;

namespace PlutoWindowsScannerGui;

public partial class MainWindow
{
    private sealed class ListenProfilesFile
    {
        [JsonPropertyName("profiles")]
        public List<ListenProfileConfig> Profiles { get; set; } = new();
    }

    private sealed class ListenProfileConfig
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("mode")]
        public string? Mode { get; set; }

        [JsonPropertyName("rx_channel")]
        public int? RxChannel { get; set; }

        [JsonPropertyName("iq_mode")]
        public string? IqMode { get; set; }

        [JsonPropertyName("fm_channel_lowpass_hz")]
        public double? FmChannelLowpassHz { get; set; }

        [JsonPropertyName("fm_demod_rate_hz")]
        public double? FmDemodRateHz { get; set; }

        [JsonPropertyName("audio_lowpass_hz")]
        public double? AudioLowpassHz { get; set; }

        [JsonPropertyName("audio_highpass_hz")]
        public double? AudioHighpassHz { get; set; }

        [JsonPropertyName("fm_deviation_hz")]
        public double? FmDeviationHz { get; set; }

        [JsonPropertyName("volume")]
        public double? Volume { get; set; }

        [JsonPropertyName("squelch")]
        public string? Squelch { get; set; }

        [JsonPropertyName("squelch_db")]
        public double? SquelchDb { get; set; }

        [JsonPropertyName("detect_only")]
        public bool DetectOnly { get; set; }

        [JsonPropertyName("args")]
        public List<string>? ExtraArgs { get; set; }
    }

    private List<ListenProfileConfig> _listenProfiles = new();

    private string GetListenProfileName()
    {
        if (ListenProfileComboBox?.SelectedItem is ComboBoxItem item)
        {
            return item.Content?.ToString() ?? "Auto by Mode";
        }

        return "Auto by Mode";
    }

    private void AddListenArg(List<string> args, string name, string value)
    {
        args.Add(name);
        args.Add(value);
    }

    private static string FormatArg(double value)
    {
        return value.ToString("0.########", CultureInfo.InvariantCulture);
    }

    private void RemoveListenArgs(List<string> args, params string[] names)
    {
        var remove = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < args.Count; i++)
        {
            if (!remove.Contains(args[i]))
            {
                continue;
            }

            bool hasValue =
                i + 1 < args.Count &&
                !args[i + 1].StartsWith("--", StringComparison.Ordinal);

            args.RemoveAt(i);

            if (hasValue)
            {
                args.RemoveAt(i);
            }

            i--;
        }
    }

    private string FindListenProfilesPath()
    {
        string cwdCandidate = Path.Combine(Environment.CurrentDirectory, "configs", "listen_profiles.json");
        if (File.Exists(cwdCandidate))
        {
            return cwdCandidate;
        }

        string baseCandidate = Path.Combine(AppContext.BaseDirectory, "configs", "listen_profiles.json");
        if (File.Exists(baseCandidate))
        {
            return baseCandidate;
        }

        DirectoryInfo? dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            string candidate = Path.Combine(dir.FullName, "configs", "listen_profiles.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        return cwdCandidate;
    }

    private void InitializeListenProfilesFromConfig()
    {
        try
        {
            string path = FindListenProfilesPath();

            if (File.Exists(path))
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                };

                var file = JsonSerializer.Deserialize<ListenProfilesFile>(File.ReadAllText(path), options);
                _listenProfiles = file?.Profiles?
                    .Where(p => !string.IsNullOrWhiteSpace(p.Name))
                    .ToList() ?? new List<ListenProfileConfig>();
            }
            else
            {
                _listenProfiles = new List<ListenProfileConfig>();
            }

            if (ListenProfileComboBox == null)
            {
                return;
            }

            string selected = GetListenProfileName();

            ListenProfileComboBox.Items.Clear();
            ListenProfileComboBox.Items.Add(new ComboBoxItem { Content = "Auto by Mode" });

            foreach (var profile in _listenProfiles)
            {
                ListenProfileComboBox.Items.Add(new ComboBoxItem { Content = profile.Name });
            }

            bool restored = false;
            foreach (var item in ListenProfileComboBox.Items.OfType<ComboBoxItem>())
            {
                if (string.Equals(item.Content?.ToString(), selected, StringComparison.OrdinalIgnoreCase))
                {
                    ListenProfileComboBox.SelectedItem = item;
                    restored = true;
                    break;
                }
            }

            if (!restored)
            {
                ListenProfileComboBox.SelectedIndex = 0;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Listen profile load failed: {ex.Message}");
        }
    }

    private void ApplyListenProfileArguments(List<string> args, ref string mode)
    {
        string profileName = GetListenProfileName();

        if (profileName == "Auto by Mode")
        {
            return;
        }

        var profile = _listenProfiles.FirstOrDefault(
            p => string.Equals(p.Name, profileName, StringComparison.OrdinalIgnoreCase));

        if (profile == null)
        {
            Debug.WriteLine($"Listen profile not found in configs/listen_profiles.json: {profileName}");
            return;
        }

        if (profile.DetectOnly)
        {
            return;
        }

        // Profiles should replace default GUI listen settings, not append conflicting args.
        RemoveListenArgs(
            args,
            "--mode",
            "--squelch-db",
            "--squelch-off",
            "--volume",
            "--rx-channel",
            "--iq-mode",
            "--fm-channel-lowpass-hz",
            "--fm-demod-rate-hz",
            "--audio-lowpass-hz",
            "--audio-highpass-hz",
            "--fm-deviation-hz");

        if (!string.IsNullOrWhiteSpace(profile.Mode))
        {
            mode = profile.Mode;
            AddListenArg(args, "--mode", profile.Mode);
        }

        if (profile.RxChannel.HasValue)
        {
            AddListenArg(args, "--rx-channel", profile.RxChannel.Value.ToString(CultureInfo.InvariantCulture));
        }

        if (!string.IsNullOrWhiteSpace(profile.IqMode))
        {
            AddListenArg(args, "--iq-mode", profile.IqMode);
        }

        if (profile.FmChannelLowpassHz.HasValue)
        {
            AddListenArg(args, "--fm-channel-lowpass-hz", FormatArg(profile.FmChannelLowpassHz.Value));
        }

        if (profile.FmDemodRateHz.HasValue)
        {
            AddListenArg(args, "--fm-demod-rate-hz", FormatArg(profile.FmDemodRateHz.Value));
        }

        if (profile.AudioLowpassHz.HasValue)
        {
            AddListenArg(args, "--audio-lowpass-hz", FormatArg(profile.AudioLowpassHz.Value));
        }

        if (profile.AudioHighpassHz.HasValue)
        {
            AddListenArg(args, "--audio-highpass-hz", FormatArg(profile.AudioHighpassHz.Value));
        }

        if (profile.FmDeviationHz.HasValue)
        {
            AddListenArg(args, "--fm-deviation-hz", FormatArg(profile.FmDeviationHz.Value));
        }

        if (profile.Volume.HasValue)
        {
            AddListenArg(args, "--volume", FormatArg(profile.Volume.Value));
        }

        if (string.Equals(profile.Squelch, "off", StringComparison.OrdinalIgnoreCase))
        {
            args.Add("--squelch-off");
        }
        else if (profile.SquelchDb.HasValue)
        {
            AddListenArg(args, "--squelch-db", FormatArg(profile.SquelchDb.Value));
        }

        if (profile.ExtraArgs != null)
        {
            args.AddRange(profile.ExtraArgs.Where(a => !string.IsNullOrWhiteSpace(a)));
        }
    }
    private void ReloadListenProfilesButton_Click(object sender, RoutedEventArgs e)
    {
        InitializeListenProfilesFromConfig();
        Debug.WriteLine($"Listen profiles reloaded: {FindListenProfilesPath()}");
    }

    private void OpenListenProfilesJsonButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string path = FindListenProfilesPath();
            string? dir = Path.GetDirectoryName(path);

            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }

            if (!File.Exists(path))
            {
                File.WriteAllText(path, "{\n  \"profiles\": []\n}\n");
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Could not open listen profiles JSON:\n{ex.Message}",
                "Listen Profiles",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

}
