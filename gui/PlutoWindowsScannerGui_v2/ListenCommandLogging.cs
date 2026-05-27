using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;

namespace PlutoWindowsScannerGui;

public partial class MainWindow
{
    private string FormatListenCommandForLog(string executable, IEnumerable<string> args)
    {
        static string Quote(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "\"\"";
            }

            bool needsQuote = value.Any(char.IsWhiteSpace) || value.Contains('"');
            if (!needsQuote)
            {
                return value;
            }

            return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        return Quote(executable) + " " + string.Join(" ", args.Select(Quote));
    }

    private void LogListenCommandForTroubleshooting(string executable, IEnumerable<string> args)
    {
        string line = "LISTEN CMD: " + FormatListenCommandForLog(executable, args);

        try
        {
            // Try common run-log TextBox names without depending on one exact XAML field.
            foreach (string name in new[]
            {
                "RunLogTextBox",
                "RunLogBox",
                "LogTextBox",
                "LogOutputTextBox",
                "OutputTextBox",
                "RunLog"
            })
            {
                if (FindName(name) is TextBox tb)
                {
                    tb.AppendText(line + Environment.NewLine);
                    tb.ScrollToEnd();
                    return;
                }
            }
        }
        catch
        {
            // Logging must never break listening.
        }

        try
        {
            System.IO.Directory.CreateDirectory("sessions");
            System.IO.File.WriteAllText(
                System.IO.Path.Combine("sessions", "last_listen_command.txt"),
                line + Environment.NewLine);
        }
        catch
        {
            // Ignore fallback logging errors too.
        }
    }
}
