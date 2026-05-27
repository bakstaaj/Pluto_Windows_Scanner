using System.Collections.Generic;
using System.Windows.Controls;

namespace PlutoWindowsScannerGui;

public partial class MainWindow
{
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


    private void ApplyListenProfileArguments(List<string> args, ref string mode)
    {
        string profile = GetListenProfileName();

        if (profile != "Auto by Mode" && profile != "Digital Detect Only")
        {
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
        }

        switch (profile)
        {
            case "NOAA/NFM Clean Normal":
                mode = "nfm";
                AddListenArg(args, "--mode", "nfm");
                AddListenArg(args, "--rx-channel", "1");
                AddListenArg(args, "--iq-mode", "normal");
                AddListenArg(args, "--fm-channel-lowpass-hz", "8000");
                AddListenArg(args, "--fm-demod-rate-hz", "100000");
                AddListenArg(args, "--audio-lowpass-hz", "3000");
                AddListenArg(args, "--audio-highpass-hz", "150");
                AddListenArg(args, "--volume", "0.45");
                args.Add("--squelch-off");
                break;

            case "NOAA/NFM Clean Invert-Q":
                mode = "nfm";
                AddListenArg(args, "--mode", "nfm");
                AddListenArg(args, "--rx-channel", "1");
                AddListenArg(args, "--iq-mode", "invert-q");
                AddListenArg(args, "--fm-channel-lowpass-hz", "8000");
                AddListenArg(args, "--fm-demod-rate-hz", "100000");
                AddListenArg(args, "--audio-lowpass-hz", "3000");
                AddListenArg(args, "--audio-highpass-hz", "150");
                AddListenArg(args, "--volume", "0.45");
                args.Add("--squelch-off");
                break;

            case "Ham FM NFM":
                mode = "nfm";
                AddListenArg(args, "--mode", "nfm");
                AddListenArg(args, "--rx-channel", "1");
                AddListenArg(args, "--iq-mode", "normal");
                AddListenArg(args, "--fm-channel-lowpass-hz", "10000");
                AddListenArg(args, "--fm-demod-rate-hz", "100000");
                AddListenArg(args, "--audio-lowpass-hz", "3500");
                AddListenArg(args, "--audio-highpass-hz", "150");
                AddListenArg(args, "--volume", "0.35");
                args.Add("--squelch-off");
                break;

            case "Airband AM":
                mode = "am";
                AddListenArg(args, "--mode", "am");
                AddListenArg(args, "--audio-lowpass-hz", "5000");
                AddListenArg(args, "--audio-highpass-hz", "100");
                AddListenArg(args, "--volume", "0.40");
                args.Add("--squelch-off");
                break;

            case "Broadcast FM WBFM":
                mode = "wbfm";
                AddListenArg(args, "--mode", "wbfm");
                AddListenArg(args, "--audio-lowpass-hz", "15000");
                AddListenArg(args, "--audio-highpass-hz", "30");
                AddListenArg(args, "--volume", "0.30");
                args.Add("--squelch-off");
                break;

            case "Digital Detect Only":
                // Leave demod settings unchanged for now. We will later disable Listen
                // or route this profile to decoder/detect-only behavior.
                break;

            case "Auto by Mode":
            default:
                // Keep existing GUI behavior.
                break;
        }
    }
}
