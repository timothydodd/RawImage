using RawImage.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using System.Threading;

namespace RawImage.Services;

/// <summary>
/// Ensures only one instance of the application is running.
/// Uses named mutex and named pipes for IPC.
/// </summary>
public class SingleInstanceService : IDisposable
{
    private readonly string _appName;
    private Mutex? _mutex;
    private Thread? _pipeThread;
    private CancellationTokenSource? _cts;
    private bool _disposed;

    public static SingleInstanceService? Instance { get; private set; }

    public event Action<IList<string>>? ArgsReceived;

    public SingleInstanceService(string appName)
    {
        _appName = appName;
        Instance = this;
    }

    public bool TryAcquire(string[] args)
    {
        var mutexName = $"Global\\{_appName}";
        var pipeName = $"{_appName}_Pipe";

        _mutex = new Mutex(true, mutexName, out bool isFirstInstance);

        if (isFirstInstance)
        {
            StartListening(pipeName);
            return true;
        }
        else
        {
            SendArgsToFirstInstance(pipeName, args);
            return false;
        }
    }

    private void StartListening(string pipeName)
    {
        _cts = new CancellationTokenSource();
        _pipeThread = new Thread(() =>
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    using var pipeServer = new NamedPipeServerStream(pipeName, PipeDirection.In);
                    pipeServer.WaitForConnection();

                    using var reader = new StreamReader(pipeServer);
                    var json = reader.ReadToEnd();
                    var args = JsonSerializer.Deserialize(json, AppJsonContext.Default.ListString);

                    if (args != null)
                    {
                        ArgsReceived?.Invoke(args);
                    }
                }
                catch (Exception)
                {
                    if (_cts.Token.IsCancellationRequested)
                        break;
                }
            }
        })
        {
            IsBackground = true
        };
        _pipeThread.Start();
    }

    private static void SendArgsToFirstInstance(string pipeName, string[] args)
    {
        try
        {
            using var pipeClient = new NamedPipeClientStream(".", pipeName, PipeDirection.Out);
            pipeClient.Connect(1000);

            using var writer = new StreamWriter(pipeClient);
            var json = JsonSerializer.Serialize(args, AppJsonContext.Default.StringArray);
            writer.Write(json);
            writer.Flush();
        }
        catch
        {
            // Could not connect to first instance
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _cts?.Cancel();
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        _cts?.Dispose();

        Instance = null;
    }
}
