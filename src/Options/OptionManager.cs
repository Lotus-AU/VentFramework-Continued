using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using VentLib.Logging.Default;
using VentLib.Options.Events;
using VentLib.Options.Interfaces;
using VentLib.Options.IO;
using VentLib.Options.UI.Options;
using VentLib.Utilities;
using VentLib.Utilities.Collections;
using VentLib.Utilities.Extensions;

namespace VentLib.Options;

public class OptionManager
{
    public static string OptionPath => OperatingSystem.IsAndroid() ? Vents.BasePath : Paths.ConfigPath;
    public const string AndroidOptionFolder = "vf_config";
    public const string DefaultFile = "options.txt";
    
    internal static Dictionary<Assembly, List<OptionManager>> Managers = new();
    internal static Dictionary<String, Option> AllOptions = new();
    
    public int SaveIndex;

    private OrderedSet<Action<IOptionEvent>> optionEventHandlers = new();
    private readonly OrderedDictionary<string, Option> options = new();
    private readonly OptionManagerFlags flags;
    private readonly EssFile essFile = new();
    private string filePath;
    private FileInfo file;
    private bool saving;

    internal OptionManager(Assembly assembly, string path, OptionManagerFlags managerFlags)
    {
        string name = AssemblyUtils.GetAssemblyRefName(assembly);
        string optionPath;
        if (OperatingSystem.IsAndroid())
        {
            if (managerFlags.HasFlag(OptionManagerFlags.IgnorePreset)) 
                optionPath = Path.Combine(OptionPath, AndroidOptionFolder, name == "root" ? string.Empty : name);
            else 
                optionPath = Path.Combine(OptionPath, AndroidOptionFolder, PresetManager.CurrentPreset.FolderName(), name == "root" ? string.Empty : name);
        }
        else
        {
            if (managerFlags.HasFlag(OptionManagerFlags.IgnorePreset)) 
                optionPath = name == "root" ? OptionPath : Path.Combine(OptionPath, name);
            else 
                optionPath = Path.Combine(OptionPath, PresetManager.CurrentPreset.FolderName(), name == "root" ? string.Empty : name);
        }
        DirectoryInfo optionDirectory = new(optionPath);
        if (!optionDirectory.Exists) optionDirectory.Create();
        flags = managerFlags;
        filePath = path;
        file = optionDirectory.GetFile(path);
        essFile.ParseFile(file.FullName);
    }

    public static OptionManager GetManager(Assembly? assembly = null, string? file = null, OptionManagerFlags managerFlags = OptionManagerFlags.None)
    {
        file ??= DefaultFile;
        assembly ??= Assembly.GetCallingAssembly();
        List<OptionManager> managers = Managers.GetOrCompute(assembly, () => new List<OptionManager>());
        OptionManager? manager = managers.FirstOrDefault(m => m.filePath == file);
        if (manager != null) return manager;
        manager = new OptionManager(assembly, file, managerFlags);
        managers.Add(manager);
        return manager;
    }

    public static List<OptionManager> GetAllManagers(Assembly? assembly = null)
    {
        assembly ??= Assembly.GetCallingAssembly();
        return Managers.GetOrCompute(assembly, () => new List<OptionManager>());
    }

    public static List<OptionManager> GetAllManagersForAllAssemblys() => Managers.Values.SelectMany(m => m).ToList();

    internal static void OnChangePreset(bool wasDeleted = false)
    {
        Managers.ForEach(kvp => kvp.Value.ForEach(m =>
        {
            if (m.flags.HasFlag(OptionManagerFlags.IgnorePreset)) return;
            if (!wasDeleted) 
                m.SaveAll(); // Save current settings.
            string name = AssemblyUtils.GetAssemblyRefName(kvp.Key);
            
            // Reset File
            string optionPath = Path.Combine(OptionPath, PresetManager.CurrentPreset.FolderName(), name == "root" ? "" : name);
            DirectoryInfo optionDirectory = new(optionPath);
            if (!optionDirectory.Exists) optionDirectory.Create();
            m.file = optionDirectory.GetFile(m.filePath);
            m.essFile.ParseFile(m.file.FullName);
            // NoDepLogger.Info($"Reloaded {m.file.FullName}.");
            m.saving = false;
        }));
        AllOptions.Values.Where(o => !o.Manager?.flags.HasFlag(OptionManagerFlags.IgnorePreset) ?? false).ForEach(o =>
        {
            o.Manager!.Load(o, true);
            if (o is IGameOptionInstance gameOptionInstance) gameOptionInstance.UpdateOption();
        });
    }

    // ReSharper disable once ReturnTypeCanBeNotNullable
    public Option? GetOption(string qualifier)
    {
        return GetOptions().FirstOrOptional(opt => opt.Qualifier() == qualifier)
            .CoalesceEmpty(() => AllOptions.GetOptional(qualifier))
            .OrElse(null!);
    }
    
    public OptionManagerFlags Flags() => flags;

    public List<Option> GetOptions() => options.GetValues().ToList();

    public void Register(Option option, OptionLoadMode loadMode = OptionLoadMode.Load)
    {
        AllOptions[option.Qualifier()] = option;
        
        option.RegisterEventHandler(ev =>
        {
            if (ev is not (OptionValueIncrementEvent or OptionValueDecrementEvent)) return;
            essFile.WriteToCache(ev.Source());
            DelaySave(updateAll: false);
        });

        if (!option.HasParent())
            options[option.Qualifier()] = option;
        option.Manager = this;
        option.Load(loadMode is OptionLoadMode.LoadOrCreate);
        option.RegisterEventHandler(ChangeCallback);
        OptionHelpers.GetChildren(option).ForEach(o => Register(o, loadMode));
    }

    public void ReloadFromFile()
    {
        
    }
    
    public void RegisterEventHandler(Action<IOptionEvent> eventHandler) => optionEventHandlers.Add(eventHandler);

    internal void Load(Option option, bool create = false)
    {
        try
        {
            essFile.ApplyToOption(option);
        }
        catch (DataException)
        {
            string createString = create ? ". Attempting to recreate in file." : ".";
            NoDepLogger.Warn($"Failed to load option ({option.Qualifier()})" + createString);
            if (!create) return;
            essFile.WriteToCache(option);
            DelaySave(fullName:file.FullName);
        }
        catch (Exception exception)
        {
            NoDepLogger.Exception($"Error loading option ({option.Qualifier()}).", exception);
        }
    }

    internal void SaveAll(bool updateAll = true, string? fullName = null, int? saveIndex = null)
    {
        if (!PresetManager.AllowSaving) return;
        if (fullName != null && fullName != file.FullName) return; // Stop saving for old files.
        if (saveIndex != null && saveIndex != SaveIndex) return; // Stop saving for old files.
        NoDepLogger.Trace($"Saving Options to \"{filePath}\"", "OptionSave");
        if (updateAll) GetOptions().ForEach(o => essFile.WriteToCache(o));
        essFile.Dump(fullName ?? file.FullName);
        NoDepLogger.Trace("Saved Options", "OptionSave");
    }

    public void DelaySave(float delay = 10f, bool updateAll = true, string? fullName = null)
    {
        if (!PresetManager.AllowSaving) return;
        if (saving) return;
        saving = true;
        SaveIndex++;
        int specificSaveIndex = SaveIndex;
        fullName ??= file.FullName;
        Async.ScheduleThreaded(() =>
        {
            SaveAll(updateAll, fullName, specificSaveIndex);
            saving = false;
        }, delay);
    }
    
    private void ChangeCallback(IOptionEvent optionEvent)
    {
        optionEventHandlers.ForEach(eh => eh(optionEvent));
    }
}

/// <summary>
/// An enum about the ways an Option can be loaded.
/// </summary>
public enum OptionLoadMode
{
    /// <summary>
    /// Nothing will happen.
    /// </summary>
    None,
    
    /// <summary>
    /// The option will be loaded if found in the file.
    /// </summary>
    Load,
    
    /// <summary>
    /// The option will be loaded if found in the file and created if not.
    /// </summary>
    LoadOrCreate
}

/// <summary>
/// An enum flag for specifying the properties of an Option Manager.
/// </summary>
[Flags]
public enum OptionManagerFlags
{
    /// <summary>
    /// No flags.
    /// </summary>
    None = 0,
    
    /// <summary>
    /// A manager bearing this flag will retain the same settings if the user changes presets.
    /// </summary>
    IgnorePreset = 1,
    
    /// <summary>
    /// A manager bearing this flag will have its settings automatically synced over RPC. <br/>
    /// The players must be on the same version for this to happen.
    /// </summary>
    SyncOverRpc = 2
}