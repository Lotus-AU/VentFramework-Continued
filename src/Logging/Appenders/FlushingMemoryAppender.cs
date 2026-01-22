using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text;
using System.Threading;
using BepInEx;
using UnityEngine;

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
        FileNamePattern = filenamePattern;
        TargetDirectory = new DirectoryInfo(Path.Combine(Vents.BasePath, directoryPath));

        if (!TargetDirectory.Exists) TargetDirectory.Create();
        MinLevel = minLevel ?? LogLevel.Info;
        flushingThread = new Thread(FlushMemory) { IsBackground = true };
        flushingThread.Start();
    }
    
    public FileInfo CreateNewFile()
    {
        return LogFile = LogDirectory.CreateLog(FileNamePattern, TargetDirectory);
    }

    private void FlushMemory()
    {
        while (autoFlush)
        {
            Thread.Sleep(FlushDelayMilliseconds);
            if (LogFile == null) continue;
            
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
    
}