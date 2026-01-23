using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using VentLib.Commands.Attributes;
using VentLib.Logging.Default;
using VentLib.Utilities.Extensions;

namespace VentLib.Commands;

public class CommandRunner
{
    /// <summary>
    /// The prefix text for all command instances for all mods.
    /// </summary>
    public static string Prefix = "/";
    
    public static CommandRunner Instance { get; private set; } = null!;
    internal Dictionary<string, List<Command>> Commands = new();
    internal Dictionary<string, List<Command>> CaseSensitiveCommands = new();

    internal CommandRunner()
    {
        Instance = this;
    }

    public void Register(Assembly assembly)
    {
        assembly.GetTypes().ForEach(t => CommandAttribute.Register(this, t));
    }

    internal void Register(Command command)
    {
        Dictionary<string, List<Command>> commandDictionary = command.Flags.HasFlag(CommandFlag.CaseSensitive) ? CaseSensitiveCommands : Commands;
        command.Aliases.ForEach(alias => commandDictionary.GetOrCompute(alias, () => new List<Command>()).Add(command));
    }

    internal void Execute(CommandContext context)
    {
        List<Command>? matches = Commands!.GetValueOrDefault(context.Alias);
        matches ??= CaseSensitiveCommands!.GetValueOrDefault(context.Alias);
        matches?.ForEach(m => ExecuteLowestCommands(m, context));
    }
    
    private bool ExecuteLowestCommands(Command source, CommandContext context, int subCommandIndex = 1)
    {
        // NoDepLogger.Debug($"{context.Source.name} is executing /{context.OriginalMessage}");        
        if (source.Flags.HasFlag(CommandFlag.HostOnly) && !context.Source.IsHost()) return false;
        if (source.Flags.HasFlag(CommandFlag.ModdedOnly) && !context.Source.IsModded()) return false;
        if (source.Flags.HasFlag(CommandFlag.LobbyOnly) && LobbyBehaviour.Instance == null) return false;
        if (source.Flags.HasFlag(CommandFlag.InGameOnly) && LobbyBehaviour.Instance != null) return false;
        // NoDepLogger.Debug($"c1 - alias null check");

        if (context.Alias == null) return false;
        // NoDepLogger.Debug($"c2 - case senseitive check");
        List<string> aliases = source.Aliases.ToList();
        string alias = context.Alias;
        if (!source.Flags.HasFlag(CommandFlag.CaseSensitive))
        {
            aliases = aliases.Select(s => s.ToLower()).ToList();
            alias = alias.ToLower();
        }

        if (!aliases.Contains(alias)) return false;
        // NoDepLogger.Debug($"c3 - subcommands check");
        
        bool higherExecution = source.SubCommands.Any(sc => ExecuteLowestCommands(sc, context.Subcommand(subCommandIndex), subCommandIndex + 1));
        if (higherExecution) return false;
        // NoDepLogger.Debug($"c4 - executing main command");

        source.Execute(context);
        return true;
    }
}