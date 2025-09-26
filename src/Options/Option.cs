using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using Hazel;
using VentLib.Logging;
using VentLib.Networking.Interfaces;
using VentLib.Networking.RPC;
using VentLib.Options.Enum;
using VentLib.Options.Events;
using VentLib.Options.Interfaces;
using VentLib.Options.IO;
using VentLib.Utilities.Optionals;
using VentLib.Version;

namespace VentLib.Options;

public class Option: IRpcSendable<Option>
{
    #if ANDROID
    private static StandardLogger? _log;
    private static StandardLogger Log => _log ??= LoggerFactory.GetLogger<StandardLogger>(typeof(Option));
    #else
    private static StandardLogger log = LoggerFactory.GetLogger<StandardLogger>(typeof(Option));
    #endif
    
    private static ModRPC _modRPC = Vents.FindRPC((uint)VentCall.SyncSingleOption)!;
    // ReSharper disable once InconsistentNaming
    internal string name = null!;
    internal string? key;
    public Optional<string> Description = Optional<string>.Null();
    public OptionType OptionType = OptionType.Undefined;
    public IOSettings IOSettings { get; set; } = new();
    public Dictionary<string, object> Attributes = new();

    internal Optional<int> Index = Optional<int>.Null();
    public int DefaultIndex { get; internal set; }

    internal Type ValueType
    {
        get => valueType ??= Values.Count > 0 ? Values[0].Value.GetType() : typeof(string);
        set => valueType = value;
    }
    internal Type? valueType;
    
    internal List<OptionValue> Values = new();

    protected Optional<OptionValue> Value = Optional<OptionValue>.Null();

    internal Optional<Option> Parent = Optional<Option>.Null();
    public readonly SubOptions Children = new();
    internal OptionManager? Manager;

    internal readonly List<Action<IOptionEvent>> EventHandlers = new();

    public string Name() => name;
    public string Key() => key ?? Name();

    public string Qualifier() => Parent.Map(p => p.Qualifier() + ".").OrElse("") + Key();
    internal int InternalLevel() => Parent.Exists() ? Parent.Get().InternalLevel() + 1 : 0;

    public Option()
    {
        IOSettings = new(this);
    }

    public void AddChild(Option child)
    {
        Children.Add(child);
        child.Parent = Optional<Option>.Of(this);
    }

    public OptionValue GetDefault()
    {
        return Values[EnforceIndexConstraint(DefaultIndex, true)];
    }
    
    public OptionValue GetRawValue()
    {
        return Value.Transform(v => v, () =>
        {
            if (!Index.Exists()) return GetDefault();
            return Value.OrElseSet(() => Values[EnforceIndexConstraint(Index.Get(), true)]);
        });
    }

    public object GetValue() => GetRawValue().Value;

    public T GetValue<T>() => (T)GetRawValue().Value;

    public string GetValueText() => GetRawValue().GetText();

    internal object SetValue(OptionValue value)
    {
        Index = Optional<int>.NonNull(Values.IndexOf(value));
        if (Index.Get() == -1) Index = Optional<int>.NonNull(DefaultIndex);
        Optional<object> oldValue = Value.Map(v => v.Value);
        
        Value = Optional<OptionValue>.Of(value);
        
        OptionValueEvent optionValueEvent = new(this, oldValue, value.Value);
        EventHandlers.ForEach(eh => eh(optionValueEvent));
        SyncOption();
        return value.Value;
    }

    public int SetValue(int index, bool triggerEvent = true)
    {
        Index = Optional<int>.NonNull(EnforceIndexConstraint(index));
        Optional<object> oldValue = Value.Map(v => v.Value);
        
        Value = Optional<OptionValue>.NonNull(Values[index]);

        OptionValueEvent optionValueEvent = new(this, oldValue, Value.Get().Value);
        if (triggerEvent) EventHandlers.ForEach(eh => eh(optionValueEvent));
        SyncOption();
        return index;
    }

    public void SetHardValue(object value) => SetValue(IOSettings.OptionValueLoader.LoadValue(Values, value, IOSettings));

    public void SetDefaultIndex(int index)
    {
        DefaultIndex = EnforceIndexConstraint(index);
    }
    
    public void RegisterEventHandler(Action<IOptionEvent> eventHandler)
    {
        EventHandlers.Add(eventHandler);
    }
    
    public void Register(OptionManager manager, OptionLoadMode loadMode = OptionLoadMode.None)
    {
        manager.Register(this, loadMode);
        OptionRegisterEvent registerEvent = new(this, manager);
        EventHandlers.ForEach(eh => eh(registerEvent));
    }

    public void Register(Assembly? assembly = null, OptionLoadMode loadMode = OptionLoadMode.None)
    {
        assembly ??= Assembly.GetCallingAssembly();
        OptionManager manager = OptionManager.GetManager(assembly);
        Register(manager, loadMode);
    }
    
    public void Load(bool saveOnCreation = true)
    {
        if (Manager == null)
            throw new FileLoadException($"Cannot load Option \"{Qualifier()}\". Option not registered.");
        Manager.Load(this, saveOnCreation);
        if (!Value.Exists()) return;
        OptionLoadEvent loadEvent = new(this, Value.Get().Value);
        EventHandlers.ForEach(eh => eh(loadEvent));
    }

    protected int EnforceIndexConstraint(int index, bool allowOuFlow = false)
    {
        if (Values.Count == 0)
            throw new ConstraintException($"Index fails constraint because no values exist! (Option={Qualifier()})");
        if (index >= Values.Count)
        {
            if (!allowOuFlow)
                throw new ConstraintException($"Index is greater than value count! ({index} >= {Values.Count})");
            index = 0;
        } 
        else if (index < 0)
        {
            if (!allowOuFlow)
                throw new ConstraintException($"Index is less than zero! ({index} < 0)");
            index = Values.Count - 1;
        }

        return index;
    }

    public void NotifySubscribers(IOptionEvent @event)
    {
        EventHandlers.ForEach(eh => eh(@event));
    }

    public Option Read(MessageReader reader)
    {
        string qualifier = reader.ReadString();
        
        if (!OptionManager.AllOptions.TryGetValue(qualifier, out Option? targetOption))
            return new NullOption();

        return targetOption;
    }

    public void Write(MessageWriter writer)
    {
        writer.Write(Qualifier());
    }

    private void SyncOption()
    {
        if (PresetManager.IsSwitchingPresets) return; // Fix sending an RPC for every option (it would cause kicks if you had a lot of options!)
        if (!VersionControl.Instance.PassedClients.Any()) return;
        if (Manager == null) return;
        if (!Manager.Flags().HasFlag(OptionManagerFlags.SyncOverRpc)) return;
        _modRPC.Send(null, new VentRPC.NetworkedOption(this, Index.OrElse(0)));
    }

    internal bool HasParent() => Parent.Exists();
    internal Option GetAncestor() => HasParent() ? Parent.Get().GetAncestor() : this;
}

/// <summary>
/// A class representing an option that doesn't exist on this client.
/// </summary>
public class NullOption : Option
{
    
}