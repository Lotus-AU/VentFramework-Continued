using System;
using System.IO;
using System.Reflection;
using BepInEx;
using UnityEngine;
using VentLib.Options;
using VentLib.Options.IO;
using VentLib.Utilities;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace VentLib.Localization;

public class LocalizerSettings
{
    // ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable
    private static readonly string LanguageFolder;
    public static DirectoryInfo LanguageDirectory { get; }
    
    static LocalizerSettings()
    {
        Assembly executingAssembly = Assembly.GetExecutingAssembly();
        OptionManager manager = new(executingAssembly, "locale.config", OptionManagerFlags.IgnorePreset);
        Option languageFolderOption = new OptionBuilder().Name("Language Folder")
            .Description("Folder where translations are stored")
            .Value("Languages")
            .IOSettings(settings => settings.UnknownValueAction = ADEAnswer.Allow)
            .BuildAndRegister(manager);

        string assemblyName = OperatingSystem.IsWindows() ? string.Empty : AssemblyUtils.GetAssemblyRefName(executingAssembly);

        LanguageFolder = languageFolderOption.GetValue<string>();
        LanguageDirectory = OperatingSystem.IsAndroid() 
            ? new DirectoryInfo(Path.Combine(Vents.BasePath, assemblyName, LanguageFolder))
            : new DirectoryInfo(Path.Combine(Vents.BasePath, LanguageFolder));
        if (!LanguageDirectory.Exists) LanguageDirectory.Create();
    }

    public INamingConvention NamingConvention = PascalCaseNamingConvention.Instance;

    public string? ForceLanguage = null;

    public bool CreateTemplateFile = true;
}