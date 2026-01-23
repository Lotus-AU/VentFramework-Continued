using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using VentLib.Logging;
using VentLib.Logging.Default;
using VentLib.Utilities.Extensions;

namespace VentLib.Commands;

public class CommandContext
{
    public string OriginalMessage = null!;
    public string? Alias;
    public string[] Args = null!;
    // public List<int> ErroredParameters = null!;

    internal PlayerControl Source = null!;
    
    internal CommandContext(PlayerControl source, string message)
    {
        message = message.Trim();
        OriginalMessage = message;
        string[] split = message.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Alias = split.Length > 0 ? split[0] : string.Empty;
        Args = split.Length > 1 ? split[1..] : Array.Empty<string>();
        Source = source;
    }

    private CommandContext()
    {
    }

    internal CommandContext Subcommand(int subCommandIndex)
    {
        return new CommandContext
        {
            OriginalMessage = OriginalMessage,
            Alias = Args.Length > 0 ? Args[0] : string.Empty,
            Args = Args.Length > 1  ? Args[1..] : Array.Empty<string>(),
            // ErroredParameters = new List<int>(),
            Source = Source
        };
    }

    public string Join(string delimiter = " ") => Args.Join(delimiter: delimiter);
}