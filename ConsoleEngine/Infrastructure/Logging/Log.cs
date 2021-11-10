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
        private static readonly Thread LoggerThread = new(DispatchMessages) { Name = "Logger", IsBackground = true };
        private static Socket _loggerConnection;
        private static Process _loggerProcess;
        private static bool _running;
        private static GameBase _game;
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
            if (!(_game?.EnableLogger ?? false))
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
        
        internal static void Start(GameBase game)
        {
            _game = game;
            _running = true;
            Socket.Bind(EndPoint);
            Socket.Listen(10);
            LoggerThread.Start();
            
            _loggerProcess = new Process();
            _loggerProcess.StartInfo.FileName = "ConsoleEngine.Logger.exe";
            _loggerProcess.StartInfo.UseShellExecute = true;
            //_loggerProcess.Start();
        }

        internal static void Stop()
        {
            _running = false;
            _loggerConnection.Dispose();
            Socket.Dispose();
            Messages.Clear();
        }
    }
}