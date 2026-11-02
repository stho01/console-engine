using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ConsoleEngine.Infrastructure.Logging;

public static class Log
{
    private static readonly Socket Socket;
    private static readonly IPEndPoint EndPoint;
    private static readonly ConcurrentQueue<string> Messages = new();
    private static readonly Thread LoggerThread = new(DispatchMessages) { Name = "Logger", IsBackground = true };
    private static Socket _loggerConnection;
    private static Process _loggerProcess;
    private static bool _running;
    private const char EndOfMessage = '\0';

    static Log()
    {
        var host = Dns.GetHostEntry("localhost");  
        var ipAddress = host.AddressList[0];  
        EndPoint = new IPEndPoint(ipAddress, 11000);
        Socket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
    }
        
    public static void WriteLine(object message)
    {
        var sb = new StringBuilder();
        sb.Append(message);
        EnqueueMessage(sb);
    }

    public static void ReportFps(int fps)
    {
        var sb = new StringBuilder("#<FPS>:");
        sb.Append(fps);
        EnqueueMessage(sb);
    }

    private static void EnqueueMessage(StringBuilder messageBuilder)
    {
        // Nothing reads the queue unless the logger is running.
        if (!_running)
            return;
            
        messageBuilder.Append(EndOfMessage);
        Messages.Enqueue(messageBuilder.ToString());
    }

    private static void DispatchMessages()
    {
        while (_running)
        {
            var loggerConnected = _loggerConnection?.Connected ?? false;
                
            if (!loggerConnected)
                _loggerConnection = Socket.Accept(); // wait for new connection
                
            while (loggerConnected && Messages.TryDequeue(out var msg))
            {
                var bytes = Encoding.UTF8.GetBytes(msg);
                _loggerConnection.Send(bytes);
            }

            Thread.Sleep(100); 
        }
    }
        
    internal static void Start()
    {
        Socket.Bind(EndPoint);
        Socket.Listen(10);

        if (!StartLoggerProcess())
        {
            // Without a logger nobody would ever connect or read the messages.
            Socket.Dispose();
            return;
        }

        _running = true;
        LoggerThread.Start();
    }

    private static bool StartLoggerProcess()
    {
        var logger = Path.Combine(AppContext.BaseDirectory, OperatingSystem.IsWindows() ? "ConsoleEngine.Logger.exe" : "ConsoleEngine.Logger");
        
        _loggerProcess = new Process();
        if (OperatingSystem.IsWindows())
        {
            // Opens the logger in its own console window.
            _loggerProcess.StartInfo.FileName = logger;
            _loggerProcess.StartInfo.UseShellExecute = true;
        }
        else if (OperatingSystem.IsMacOS())
        {
            // The game owns the current terminal, open the logger in a new Terminal window.
            _loggerProcess.StartInfo.FileName = "open";
            _loggerProcess.StartInfo.ArgumentList.Add("-a");
            _loggerProcess.StartInfo.ArgumentList.Add("Terminal");
            _loggerProcess.StartInfo.ArgumentList.Add(logger);
        }
        else
        {
            // On Linux a shell executed binary runs in the game's own terminal, so it needs a terminal emulator.
            var terminal = Environment.GetEnvironmentVariable("TERMINAL");
            if (string.IsNullOrWhiteSpace(terminal))
                terminal = "x-terminal-emulator";

            _loggerProcess.StartInfo.FileName = terminal;
            _loggerProcess.StartInfo.ArgumentList.Add("-e");
            _loggerProcess.StartInfo.ArgumentList.Add(logger);
            _loggerProcess.StartInfo.UseShellExecute = false;
        }

        try
        {
            return _loggerProcess.Start();
        }
        catch (Exception)
        {
            // Printed before the game takes over the terminal, so it's visible once the game exits.
            Console.Error.WriteLine("Could not open the logger in a new window, logging is disabled. Set $TERMINAL to a terminal emulator to enable it.");
            return false;
        }
    }

    internal static void Stop()
    {
        _running = false;
        _loggerConnection?.Dispose();
        Socket.Dispose();
        Messages.Clear();
    }
}