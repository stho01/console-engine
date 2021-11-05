using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ConsoleEngine.Infrastructure.Logging
{
    public static class Log
    {
        private static readonly Socket Socket;
        private static readonly IPEndPoint EndPoint;
        private static readonly ConcurrentQueue<string> Messages = new();
        private static readonly Thread LoggerThread = new(DispatchMessages);
        private static Socket _loggerConnection;
        private static Process _loggerProcess;
        private static bool _running;

        static Log()
        {
            var host = Dns.GetHostEntry("localhost");  
            var ipAddress = host.AddressList[0];  
            EndPoint = new IPEndPoint(ipAddress, 11000);
            Socket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        }

        internal static void Start()
        {
            _running = true;
            Socket.Bind(EndPoint);
            Socket.Listen(10);
            LoggerThread.Start();
            
            _loggerProcess = new Process();
            _loggerProcess.StartInfo.FileName = "ConsoleEngine.Logger.exe";
            _loggerProcess.StartInfo.UseShellExecute = true;
            _loggerProcess.Start();
        }

        internal static void Stop()
        {
            _running = false;
            _loggerConnection.Dispose();
            Socket.Dispose();
            Messages.Clear();
        }

        private static void DispatchMessages()
        {
            while (_running)
            {
                var loggerConnected = _loggerConnection?.Connected ?? false;
                
                if (!loggerConnected)
                    _loggerConnection = Socket.Accept(); // wait for new connection
                
                while (loggerConnected
                       && !Messages.IsEmpty 
                       && Messages.TryDequeue(out var msg))
                {
                    var bytes = Encoding.UTF8.GetBytes(msg);
                    _loggerConnection.Send(bytes);
                }

                Thread.Sleep(32); 
            }
        }

        public static void Debug(string message)
        {
            Messages.Enqueue(message);
        }

        public static void ReportFps(int fps)
        {
            Messages.Enqueue($"#<FPS>:{fps}");
        }
    }
}