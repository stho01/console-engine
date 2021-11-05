using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ConsoleEngine.Logger
{
    class Program
    {
        private const string FpsPrefix = "#<FPS>:";
        private const string EndOfMessage = "<EOM>";
        
        static void Main(string[] args)
        {
            Console.WriteLine("Logger up and running...");

            try
            {
                var host = Dns.GetHostEntry("localhost");
                var ipAddress = host.AddressList[0];
                var endpoint = new IPEndPoint(ipAddress, 11000);

                var sender = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

                sender.Connect(endpoint);

                var lines = new List<string>();
                
                while (true)
                {
                    var bytes = new byte[1024];
                    var msg = string.Empty;
                    
                    while (true)
                    {
                        var count = sender.Receive(bytes);
                        msg += Encoding.UTF8.GetString(bytes, 0, count);
                        if (msg.IndexOf(EndOfMessage) > -1) 
                            break;
                    }

                    ReadOnlySpan<char> chars = msg;
                    var lastIndex = chars.LastIndexOf(EndOfMessage);
                    lines.AddRange(chars[..lastIndex].ToString().Split(EndOfMessage));
                    
                    foreach (var line in lines)
                    {
                        if (line.StartsWith(FpsPrefix))
                        {
                            ReadOnlySpan<char> fps = line;
                            Console.Title = $"Game FPS: {fps.Slice(FpsPrefix.Length).ToString()}";
                        }
                        else
                        {
                            Console.WriteLine(line);
                        }
                    }
                    
                    lines.Clear();
                }
            }
            catch (SocketException)
            {
                // yummy!
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                Thread.Sleep(10000);
            }
        }
    }
}