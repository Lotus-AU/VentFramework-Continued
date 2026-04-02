using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace VentLib.Utilities;

public static class AssemblyUtils
{
    public static Assembly? FindAssemblyFromFullName(string? fullName)
    {
        return AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(assembly => assembly.GetName().FullName == fullName);
    }
    
    public static Assembly? FindAssemblyFromSimpleName(string? simpleName)
    {
        return AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(assembly => assembly.GetName().Name == simpleName);
    }

    public static Type[] FlattenAssemblyTypes(Assembly assembly, BindingFlags flags)
    {
        Type[] GetTypes(Type type) => type.GetNestedTypes(flags).SelectMany(GetTypes).AddItem(type).ToArray();

        List<Type> surfaceTypes = assembly.GetTypes().ToList();

        return surfaceTypes.SelectMany(GetTypes).Concat(surfaceTypes).ToArray();
    }

    public static string GetRootNamespace(Assembly assembly)
    {
        var types = assembly.GetTypes();

        var namespaces = types
            .Select(t => t.Namespace ?? "")
            .Distinct()
            .Where(ns => !string.IsNullOrEmpty(ns))
            .ToList();

        if (namespaces.Count == 0)
            return string.Empty;

        var commonPrefix = namespaces
            .Aggregate((current, next) => GetCommonPrefix(current, next));

        return commonPrefix;

        string GetCommonPrefix(string str1, string str2)
        {
            int minLength = Math.Min(str1.Length, str2.Length);
            int lastDot = -1;

            for (int i = 0; i < minLength; i++)
            {
                if (str1[i] != str2[i])
                    break;

                if (str1[i] == '.')
                    lastDot = i;
            }

            return lastDot == -1 ? string.Empty : str1.Substring(0, lastDot);
        }
    }

    /// <summary>
    /// Gets the name of an Assembly.<br/>
    /// Caches the result in case of repeated use.
    /// </summary>
    /// <param name="assembly">The assembly instance to get the name of.</param>
    /// <returns>The string name of the assembly.</returns>
    public static string GetAssemblyRefName(Assembly assembly)
    {
        // return assembly == Vents.RootAssemby ? "root" : Vents.AssemblyNames.GetValueOrDefault(assembly, assembly.GetName().Name!);
        return Vents.AssemblyNames.GetValueOrDefault(assembly, assembly.GetName().Name ?? assembly.GetName().FullName.Split(',')[0]);
    }
    
}