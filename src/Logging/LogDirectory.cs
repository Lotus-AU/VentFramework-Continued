using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using BepInEx;
using UnityEngine;
using VentLib.Utilities.Attributes;
using VentLib.Utilities.Extensions;

namespace VentLib.Logging;

[LoadStatic]
public class LogDirectory
{
    public static string Directory
    {
        get => _path;
        #if ANDROID
        set => _directory = ValidateDirectory(new DirectoryInfo(Path.Combine(Application.persistentDataPath, _path = value)), true);
        #else 
        set => _directory = ValidateDirectory(new DirectoryInfo(Path.Combine(Paths.GameRootPath, _path = value)), true);
        #endif
    }
    private static string _path = "";
    private static DirectoryInfo _directory;

    static LogDirectory()
    {
        #if ANDROID
        Directory = Path.Combine(Application.persistentDataPath, "vf_logs");
        #else
        Directory = Path.Combine(Application.persistentDataPath, "logs");
        #endif
    }

    public static IEnumerable<FileInfo> GetLogs(string regex, DirectoryInfo? dir = null)
    {
        return (dir ?? _directory).EnumerateFiles().Where(f => Regex.IsMatch(f.Name, regex));
    }

    public static FileInfo CreateLog(string filename, DirectoryInfo? dir = null)
    {
        FileInfo tmpFile = new(filename);
        
        string dtFormatted = DateTime.Now.ToString(tmpFile.Name.Replace(tmpFile.Extension, ""));
        string dtRegex = dtFormatted.Select(c =>
        {
            if (char.IsDigit(c)) return "\\d+";
            if (c is '#') return "\\d*";
            return c.ToString();
        }).Fuse("");
        
        dtFormatted = dtFormatted.Replace("##", GetLogs(dtRegex, dir).Count().ToString());
        FileInfo file = _directory.GetFile(dtFormatted + tmpFile.Extension);
        if (file.Exists) throw new ConstraintException($"Cannot create log file. File \"{dtFormatted}\" already exists.");
        file.Create().Close();
        return file;
    }

    private static DirectoryInfo ValidateDirectory(DirectoryInfo dir, bool createDirectory = false)
    {
        if (dir.Exists) return dir;
        if (!createDirectory) throw new DirectoryNotFoundException($"Directory \"{dir.Name}\" does not exist.");
        dir.Create();
        return dir;
    }
}