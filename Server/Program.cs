using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Text.Json;
using System.Text;

namespace Server
{
    class Server
    {
        private static double currentCpuUsage = 0.0;
        private static double currentMemoryUsage = 0.0;

        private const int UDP_PORT = 51000;
        private const int TCP_PORT_FOR_CLIENTS = 52000;
        private const int BUFFER_SIZE = 8192;
        private const double MAX_CPU = 100.0;
        private const double MAX_MEMORY = 100.0;

        private static List<Proces> procesiList = new List<Proces>();

        static void Main(string[] args)
        {
            Console.WriteLine("=== RASPOREĐIVAČ PROCESA ===\n");
            Console.WriteLine("Algoritam raspoređivanja:");
            Console.WriteLine("1. Round Robin");
            Console.WriteLine("2. SJF");
            Console.Write("Izbor: ");
            string choice = Console.ReadLine();

            
            Socket udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            IPEndPoint udpEndPoint = new IPEndPoint(IPAddress.Any, UDP_PORT);
            udpSocket.Bind(udpEndPoint);

            
            Socket tcpListenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint tcpEndPoint = new IPEndPoint(IPAddress.Any, TCP_PORT_FOR_CLIENTS);
            tcpListenSocket.Bind(tcpEndPoint);
            tcpListenSocket.Listen(10);

            Console.WriteLine($"\n Server pokrenut (UDP: {UDP_PORT}, TCP: {TCP_PORT_FOR_CLIENTS})\n");

            
            byte[] buffer = new byte[BUFFER_SIZE];
            EndPoint clientEndPoint = new IPEndPoint(IPAddress.Any, 0);

            int bytesReceived = udpSocket.ReceiveFrom(buffer, ref clientEndPoint);
            string message = System.Text.Encoding.UTF8.GetString(buffer, 0, bytesReceived);

            if (message == "REGISTER")
            {
                Console.WriteLine($"[UDP] Prijava od: {clientEndPoint}");
                string response = TCP_PORT_FOR_CLIENTS.ToString();
                byte[] responseBytes = System.Text.Encoding.UTF8.GetBytes(response);
                udpSocket.SendTo(responseBytes, clientEndPoint);
                Console.WriteLine($"[UDP] Poslat TCP port.\n");
            }

            
            Socket acceptedSocket = tcpListenSocket.Accept();
            Console.WriteLine($"[TCP] Klijent povezan: {acceptedSocket.RemoteEndPoint}\n");

            List<Proces> procesiList = new List<Proces>();
            const double MAX_CPU = 100.0;
            const double MAX_MEMORY = 100.0;

            while (true)
            {
                try
                {
                    byte[] data = new byte[BUFFER_SIZE];
                    int bytes = acceptedSocket.Receive(data);

                    if (bytes == 0)
                    {
                        
                        Console.WriteLine("[TCP] Klijent se diskonektovao.\n");
                        break;
                    }

                    
                    string json = Encoding.UTF8.GetString(data, 0, bytes);
                    Proces proces = System.Text.Json.JsonSerializer.Deserialize<Proces>(json);

                    
                    Console.WriteLine($"[PRIMLJEN PROCES]");
                    Console.WriteLine($"  {proces}");

                    
                    if ((currentCpuUsage + proces.ZauzeceProcessora <= MAX_CPU) &&
                        (currentMemoryUsage + proces.ZauzeceMemorije <= MAX_MEMORY))
                    {
                        
                        procesiList.Add(proces);
                        currentCpuUsage += proces.ZauzeceProcessora;
                        currentMemoryUsage += proces.ZauzeceMemorije;

                        Console.WriteLine($"   Proces prihvaćen!");
                        Console.WriteLine($"     CPU: {currentCpuUsage}%, Memorija: {currentMemoryUsage}%\n");
                    }
                    else
                    {
                        
                        Console.WriteLine($"   Nedovoljno resursa - proces odbačen!\n");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[GREŠKA] {ex.Message}");
                    break;
                }
            }

            Console.WriteLine($"\nUkupno primljeno procesa: {procesiList.Count}");

            acceptedSocket.Close();
            tcpListenSocket.Close();
            udpSocket.Close();

            Console.WriteLine("\nPritisnite bilo koji taster...");

            Console.WriteLine($"\nPrimljeno procesa: {procesiList.Count}");

            if (choice == "2" && procesiList.Count > 0)
            {
                Console.WriteLine("\n=== SJF RASPOREĐIVANJE ===\n");

                while (procesiList.Count > 0)
                {
                    Proces najkraci = procesiList[0];
                    foreach (var p in procesiList)
                    {
                        if (p.VrijemeIzvrsavanja < najkraci.VrijemeIzvrsavanja)
                            najkraci = p;
                    }

                    Console.WriteLine($"[SJF] Izvršavam: {najkraci.Naziv} ({najkraci.VrijemeIzvrsavanja}s)");
                    System.Threading.Thread.Sleep(najkraci.VrijemeIzvrsavanja * 1000);

                    currentCpuUsage -= najkraci.ZauzeceProcessora;
                    currentMemoryUsage -= najkraci.ZauzeceMemorije;

                    Console.WriteLine($"Završeno! CPU: {currentCpuUsage}%, RAM: {currentMemoryUsage}%\n");

                    procesiList.Remove(najkraci);
                }
            }

            acceptedSocket.Close();
            Console.ReadKey();
        }
    }
}