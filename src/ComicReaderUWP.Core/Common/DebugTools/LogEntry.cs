// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReaderUWP.Core.Common.DebugTools;

public class LogEntry
{
    public required int Id { init; get; }
    public required DateTimeOffset Time { init; get; }
    public required LogLevel Level { init; get; }
    public required string Tag { init; get; }
    public required string? Message { init; get; }
    public required Exception? Exception { init; get; }

    private string? _displayMessage = null;
    public string DisplayMessage
    {
        get
        {
            string? msg = _displayMessage;
            if (msg == null)
            {
                msg = GenerateDisplayMessage();
                _displayMessage = msg;
            }

            return msg;
        }
    }

    private string GenerateDisplayMessage()
    {
        string levelTag = Level switch
        {
            LogLevel.Console => "C",
            LogLevel.Debug => "D",
            LogLevel.Info => "I",
            LogLevel.Warning => "W",
            LogLevel.Error => "E",
            LogLevel.Fatal => "F",
            _ => "U",
        };
        string realMessage = $"{Time:yyyy/M/d HH:mm:ss.fff} [{levelTag},{Tag}] {Message}";
        if (Exception != null)
        {
            realMessage += "\n" + Exception.ToString();
        }

        return realMessage;
    }
}
