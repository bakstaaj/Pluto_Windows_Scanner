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

    private void ApplyListenProfileArguments(List<string> args, ref string mode)
    {
        string profile = GetListenProfileName();

        switch (profile)
        {
            case "NOAA/NFM Clean Normal":
                mode = "nfm";
                AddListenArg(args, "--mode", "nfm");
                AddListenArg(args, "--rx-channel", "1");
                AddListenArg(args, "--iq-mode", "normal");
                AddListenArg(args, "--fm-channel-lowpass-hz", "8000");
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
