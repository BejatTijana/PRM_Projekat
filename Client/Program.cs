using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text;
namespace Client
{
    class Client
    {
        private const string SERVER_IP = "127.0.0.1";
        private const int SERVER_UDP_PORT = 51000;

        static void Main(string[] args)
        {
            Console.WriteLine("=== KLIJENTSKA APLIKACIJA ===\n");

            Socket udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            IPEndPoint serverEndPoint = new IPEndPoint(IPAddress.Parse(SERVER_IP), SERVER_UDP_PORT);

            try
            {
                string registrationMessage = "REGISTER";
                byte[] messageBytes = System.Text.Encoding.UTF8.GetBytes(registrationMessage);
                udpSocket.SendTo(messageBytes, serverEndPoint);
                Console.WriteLine("[UDP] Poslata poruka 'REGISTER' serveru...");

                byte[] buffer = new byte[1024];
                EndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
                int bytesReceived = udpSocket.ReceiveFrom(buffer, ref remoteEndPoint);
                string response = System.Text.Encoding.UTF8.GetString(buffer, 0, bytesReceived);

                if (!int.TryParse(response, out int tcpPort))
                {
                    Console.WriteLine("[GREŠKA] Server nije poslao validan port.");
                    return;
                }

                Console.WriteLine($"[UDP] Primljen TCP port: {tcpPort}\n");
                udpSocket.Close();

                Socket tcpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                IPEndPoint tcpEndPoint = new IPEndPoint(IPAddress.Parse(SERVER_IP), tcpPort);
                tcpSocket.Connect(tcpEndPoint);
                Console.WriteLine($"[TCP] Uspostavljena konekcija!\n");
                List<Proces> pendingProcesi = new List<Proces>();
                while (true)
                {
                    Console.WriteLine("--- UNOS PROCESA ---");
                    Console.Write("Naziv procesa (ili 'kraj' za izlaz): ");
                    string naziv = Console.ReadLine();

                    if (naziv.ToLower() == "kraj")
                        break;

                    Console.Write("Vrijeme izvršavanja (sekunde): ");
                    int vrijeme = int.Parse(Console.ReadLine());

                    Console.Write("Prioritet: ");
                    int prioritet = int.Parse(Console.ReadLine());

                    Console.Write("Zauzeće procesora (%): ");
                    double cpu = double.Parse(Console.ReadLine());

                    Console.Write("Zauzeće memorije (%): ");
                    double memorija = double.Parse(Console.ReadLine());

                    Proces proces = new Proces(naziv, vrijeme, prioritet, cpu, memorija);

                    string json = System.Text.Json.JsonSerializer.Serialize(proces);
                    byte[] data = Encoding.UTF8.GetBytes(json);

                    tcpSocket.Send(data);
                    Console.WriteLine($"\n Proces '{naziv}' poslat serveru!\n");
                }

                tcpSocket.Shutdown(SocketShutdown.Both);
                tcpSocket.Close();
            }

                
            catch (Exception ex)
            {
                Console.WriteLine($"[GREŠKA] {ex.Message}");
            }

            Console.ReadKey();
        }
    }
}