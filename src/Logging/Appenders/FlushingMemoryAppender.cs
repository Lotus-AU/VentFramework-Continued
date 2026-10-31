extern alias JetbrainsAnnotations;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using BepInEx;
using JetbrainsAnnotations::JetBrains.Annotations;
using UnityEngine;
using VentLib.Utilities;

namespace VentLib.Logging.Appenders;

public class FlushingMemoryAppender: InMemoryAppender
{
    private static StandardLogger? _log;
    private static StandardLogger LOG => _log ??= LoggerFactory.GetLogger<StandardLogger>(typeof(FlushingMemoryAppender));
    
    private DirectoryInfo TargetDirectory { get; }
    public string FileNamePattern;
    public LogLevel MinLevel { get; set; }
    public FileInfo? LogFile { get; set; }
    public Encoding Encoding { get; set; } = Encoding.UTF8;

    private const int FlushDelayMilliseconds = 10_000; // 10 Seconds
    
    public bool AutoFlush
    {
        get => autoFlush;
        set
        {
            if (!autoFlush && value) flushingThread.Start();
            autoFlush = value;
        }
    }
    private bool autoFlush = true;

    private Thread flushingThread;
    
    public FlushingMemoryAppender(string directoryPath, string filenamePattern, LogLevel? minLevel = null)
    {
        Assembly executingAssembly = Assembly.GetExecutingAssembly();
        string assemblyName = OperatingSystem.IsWindows()
            ? string.Empty
            : AssemblyUtils.GetAssemblyRefName(executingAssembly);
        
        FileNamePattern = filenamePattern;
        TargetDirectory = OperatingSystem.IsAndroid()
            ? new DirectoryInfo(Path.Combine(Vents.BasePath, assemblyName, directoryPath))
            : new DirectoryInfo(Path.Combine(Vents.BasePath, directoryPath));

        if (!TargetDirectory.Exists) TargetDirectory.Create();
        MinLevel = minLevel ?? LogLevel.Info;
        flushingThread = new Thread(FlushMemory) { IsBackground = true };
        flushingThread.Start();
    }
    
    /// <summary>
    /// Creates the file on disc for this object.
    /// </summary>
    /// <returns>The created logfile as a <see cref="FileInfo"/>.</returns>
    public FileInfo CreateNewFile()
    {
        return LogFile = LogDirectory.CreateLog(FileNamePattern, dir: TargetDirectory);
    }

    private void FlushMemory()
    {
        while (autoFlush)
        {
            Thread.Sleep(FlushDelayMilliseconds);
            ForceFlush();
        }
    }

    /// <summary>
    /// Forces this log entry to write to the disc and clear all entries from memory.
    /// </summary>
    [UsedImplicitly]
    public void ForceFlush()
    {
        if (LogFile == null) return;
        FileStream? stream = null;

        List<LogComposite> oldComposites = Composites;
        Composites = new List<LogComposite>();
            
        try
        {
            stream = LogFile.Open(FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            foreach (LogComposite composite in oldComposites)
            {
                stream.Write(Encoding.GetBytes(composite.ToString(false) + '\n'));
            }
            Clear();
        }
        catch (Exception exception)
        {
            LOG.Exception(exception);
        }
        finally
        {
            stream?.Flush();
            stream?.Close();
            oldComposites.Clear();
        }
    }
}