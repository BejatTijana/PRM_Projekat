using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

class Server
{
    private static double currentCpuUsage = 0.0;
    private static double currentMemoryUsage = 0.0;

   
    private const int UDP_PORT = 51000;
    private const int TCP_PORT_FOR_CLIENTS = 52000; 
    private const int BUFFER_SIZE = 1024;

    static void Main(string[] args)
    {
        
        Console.WriteLine("Izaberite algoritam raspoređivanja:");
        Console.WriteLine("1. Round Robin");
        Console.WriteLine("2. Najkraće vrijeme izvršavanja (SJF)");
        string choice = Console.ReadLine();

        Socket udpSocket = new Socket(AddressFamily.InterNetwork,
                                      SocketType.Dgram,
                                      ProtocolType.Udp);

        IPEndPoint udpEndPoint = new IPEndPoint(IPAddress.Any, UDP_PORT);
        udpSocket.Bind(udpEndPoint);

        Console.WriteLine($"\nServer pokrenut. UDP port: {UDP_PORT}");
        Console.WriteLine($"Algoritam: {(choice == "1" ? "Round Robin" : "SJF")}");
        Console.WriteLine("Čekam prijave klijenata...\n");

        byte[] buffer = new byte[BUFFER_SIZE];
        EndPoint clientEndPoint = new IPEndPoint(IPAddress.Any, 0);

        while (true)
        {
            try
            { 
                int bytesReceived = udpSocket.ReceiveFrom(buffer, ref clientEndPoint);
                string message = Encoding.UTF8.GetString(buffer, 0, bytesReceived);

                if (message == "REGISTER")
                {
                    Console.WriteLine($"[UDP] Stigla prijava od: {clientEndPoint}");

                    string response = TCP_PORT_FOR_CLIENTS.ToString();
                    byte[] responseBytes = Encoding.UTF8.GetBytes(response);

                    udpSocket.SendTo(responseBytes, clientEndPoint);
                    Console.WriteLine($"[UDP] Poslat TCP port {response} klijentu.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Greška: {ex.Message}");
            }
        }
    }
}
