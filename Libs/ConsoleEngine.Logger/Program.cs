using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ConsoleEngine.Logger
{
    class Program
    {
        private const string FpsPrefix = "#<FPS>:";
        
        static void Main(string[] args)
        {
            Console.WriteLine("Logger up and running...");

            try
            {
                var bytes = new byte[1024];

                var host = Dns.GetHostEntry("localhost");
                var ipAddress = host.AddressList[0];
                var endpoint = new IPEndPoint(ipAddress, 11000);

                var sender = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

                sender.Connect(endpoint);

                while (true)
                {
                    var count = sender.Receive(bytes);
                    var msg = Encoding.UTF8.GetString(bytes, 0, count);

                    if (msg.StartsWith(FpsPrefix))
                    {
                        ReadOnlySpan<char> fps = msg;
                        Console.Title = $"Game FPS: {fps.Slice(FpsPrefix.Length).ToString()}";
                    }
                    else
                    {
                        Console.WriteLine(msg);
                    }
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