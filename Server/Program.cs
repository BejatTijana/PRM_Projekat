using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace Server
{
    
    class ClientInfo
    {
        public Socket TcpSocket { get; set; }
        public List<Proces> PendingProcesi { get; set; }
        public IPEndPoint EndPoint { get; set; }

        public ClientInfo(Socket socket)
        {
            TcpSocket = socket;
            PendingProcesi = new List<Proces>();
            EndPoint = (IPEndPoint)socket.RemoteEndPoint;
        }
    }

    class Server
    {
        
        private static double currentCpuUsage = 0.0;
        private static double currentMemoryUsage = 0.0;

       
        private static double maxCpuUsage = 0.0;
        private static double maxMemoryUsage = 0.0;
        private static Proces shortestProces = null;

        private const int UDP_PORT = 51000;
        private const int TCP_PORT_FOR_CLIENTS = 52000;
        private const int BUFFER_SIZE = 8192;
        private const double MAX_CPU = 100.0;
        private const double MAX_MEMORY = 100.0;

        private static string schedulingAlgorithm = "";
        private static int quantum = 0;
        private static List<Proces> procesiList = new List<Proces>();

       
        private static List<ClientInfo> clients = new List<ClientInfo>();

        static void Main(string[] args)
        {
           
            Console.WriteLine("   RASPOREĐIVAČ PROCESA - SERVER     ");
            
            Console.WriteLine();

            
            Console.WriteLine("Izaberite algoritam raspoređivanja:");
            Console.WriteLine("  1. Round Robin");
            Console.WriteLine("  2. Najkraće vrijeme izvršavanja (SJF)");
            Console.Write("\nVaš izbor: ");
            string choice = Console.ReadLine();

            if (choice == "1")
            {
                schedulingAlgorithm = "RoundRobin";
                Console.Write("Unesite quantum (sekunde): ");
                quantum = int.Parse(Console.ReadLine());
            }
            else
            {
                schedulingAlgorithm = "SJF";
            }

           
            Socket udpSocket = new Socket(AddressFamily.InterNetwork,
                                          SocketType.Dgram,
                                          ProtocolType.Udp);
            IPEndPoint udpEndPoint = new IPEndPoint(IPAddress.Any, UDP_PORT);
            udpSocket.Bind(udpEndPoint);
            udpSocket.Blocking = false; 

            
            Socket tcpListenSocket = new Socket(AddressFamily.InterNetwork,
                                               SocketType.Stream,
                                               ProtocolType.Tcp);
            IPEndPoint tcpEndPoint = new IPEndPoint(IPAddress.Any, TCP_PORT_FOR_CLIENTS);
            tcpListenSocket.Bind(tcpEndPoint);
            tcpListenSocket.Listen(10);
            tcpListenSocket.Blocking = false; 

            Console.WriteLine();
            Console.WriteLine("════════════════════════════════════════");
            Console.WriteLine("    Server uspješno pokrenut!");
            Console.WriteLine($"   UDP port:  {UDP_PORT}");
            Console.WriteLine($"   TCP port:  {TCP_PORT_FOR_CLIENTS}");
            Console.WriteLine($"   Algoritam: {schedulingAlgorithm}" +
                            (schedulingAlgorithm == "RoundRobin" ? $" (quantum={quantum}s)" : ""));
            Console.WriteLine("════════════════════════════════════════");
            Console.WriteLine("\nČekam klijente...\n");

           
            while (true)
            {
                
                List<Socket> readSockets = new List<Socket>();

                readSockets.Add(udpSocket);
                readSockets.Add(tcpListenSocket);

                
                foreach (var client in clients)
                {
                    readSockets.Add(client.TcpSocket);
                }

                try
                {
                    
                    Socket.Select(readSockets, null, null, 1000000);

                    
                    foreach (Socket socket in readSockets)
                    {
                        if (socket == udpSocket)
                        {
                            HandleUdpRegistration(udpSocket);
                        }
                        else if (socket == tcpListenSocket)
                        {
                            HandleNewTcpConnection(tcpListenSocket);
                        }
                        else
                        {
                            HandleClientData(socket);
                        }
                    }

                    
                    if (procesiList.Count > 0)
                    {
                        ProcessNext();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[GREŠKA] {ex.Message}");
                }
            }
        }

        
        static void HandleUdpRegistration(Socket udpSocket)
        {
            try
            {
                byte[] buffer = new byte[BUFFER_SIZE];
                EndPoint clientEndPoint = new IPEndPoint(IPAddress.Any, 0);

                int bytesReceived = udpSocket.ReceiveFrom(buffer, ref clientEndPoint);
                string message = Encoding.UTF8.GetString(buffer, 0, bytesReceived);

                if (message == "REGISTER")
                {
                    Console.WriteLine($"[UDP]   Primljena prijava od: {clientEndPoint}");

                    
                    string response = TCP_PORT_FOR_CLIENTS.ToString();
                    byte[] responseBytes = Encoding.UTF8.GetBytes(response);
                    udpSocket.SendTo(responseBytes, clientEndPoint);

                    Console.WriteLine($"[UDP]   Poslat TCP port {response} klijentu.\n");
                }
            }
            catch (SocketException ex)
            {
                if (ex.SocketErrorCode != SocketError.WouldBlock)
                {
                    Console.WriteLine($"[UDP Greška] {ex.Message}");
                }
            }
        }

        
        static void HandleNewTcpConnection(Socket tcpListenSocket)
        {
            try
            {
                Socket acceptedSocket = tcpListenSocket.Accept();

                
                acceptedSocket.Blocking = false;

                
                ClientInfo newClient = new ClientInfo(acceptedSocket);
                clients.Add(newClient);

                Console.WriteLine($"[TCP]   Novi klijent povezan: {newClient.EndPoint}");
                Console.WriteLine($"        Ukupno aktivnih klijenata: {clients.Count}\n");
            }
            catch (SocketException ex)
            {
                if (ex.SocketErrorCode != SocketError.WouldBlock)
                {
                    Console.WriteLine($"[TCP Accept Greška] {ex.Message}");
                }
            }
        }

        
        static void HandleClientData(Socket socket)
        {
            ClientInfo client = clients.FirstOrDefault(c => c.TcpSocket == socket);
            if (client == null) return;

            try
            {
                byte[] buffer = new byte[BUFFER_SIZE];
                int bytesReceived = socket.Receive(buffer);

                if (bytesReceived == 0)
                {
                    
                    Console.WriteLine($"[TCP]  Klijent {client.EndPoint} se diskonektovao.");

                    
                    if (client.PendingProcesi.Count > 0)
                    {
                        Console.WriteLine($"       Dodajem {client.PendingProcesi.Count} pending procesa...");

                        foreach (var trenutniProces in client.PendingProcesi)
                        {
                            if ((currentCpuUsage + trenutniProces.ZauzeceProcessora <= MAX_CPU) &&
                                (currentMemoryUsage + trenutniProces.ZauzeceMemorije <= MAX_MEMORY))
                            {
                                procesiList.Add(trenutniProces);
                                currentCpuUsage += trenutniProces.ZauzeceProcessora;
                                currentMemoryUsage += trenutniProces.ZauzeceMemorije;

                                if (currentCpuUsage > maxCpuUsage) maxCpuUsage = currentCpuUsage;
                                if (currentMemoryUsage > maxMemoryUsage) maxMemoryUsage = currentMemoryUsage;

                                Console.WriteLine($"          Dodat: {trenutniProces.Naziv}");
                            }
                        }
                    }

                    socket.Close();
                    clients.Remove(client);
                    Console.WriteLine($"       Preostalo aktivnih klijenata: {clients.Count}\n");
                    return;
                }

                
                string json = Encoding.UTF8.GetString(buffer, 0, bytesReceived);
                Proces proces = JsonSerializer.Deserialize<Proces>(json);

                Console.WriteLine($"[PRIMLJEN od {client.EndPoint}]");
                Console.WriteLine($"  {proces}");

                
                if ((currentCpuUsage + proces.ZauzeceProcessora <= MAX_CPU) &&
                    (currentMemoryUsage + proces.ZauzeceMemorije <= MAX_MEMORY))
                {
                    
                    procesiList.Add(proces);
                    currentCpuUsage += proces.ZauzeceProcessora;
                    currentMemoryUsage += proces.ZauzeceMemorije;

                    
                    if (currentCpuUsage > maxCpuUsage)
                        maxCpuUsage = currentCpuUsage;

                    if (currentMemoryUsage > maxMemoryUsage)
                        maxMemoryUsage = currentMemoryUsage;

                    Console.WriteLine($"   Prihvaćen! (CPU: {currentCpuUsage:F1}%, RAM: {currentMemoryUsage:F1}%)");
                    Console.WriteLine($"     [Max do sada: CPU={maxCpuUsage:F1}%, RAM={maxMemoryUsage:F1}%]\n");
                }
                else
                {
                    
                    client.PendingProcesi.Add(proces);
                    Console.WriteLine($"  Nedovoljno resursa! Čuva se kao pending.\n");
                }
            }
            catch (SocketException ex)
            {
                if (ex.SocketErrorCode != SocketError.WouldBlock)
                {
                    Console.WriteLine($"[Greška od {client.EndPoint}] {ex.Message}");
                }
            }
        }

        
        static void ProcessNext()
        {
            if (procesiList.Count == 0) return;

            Proces trenutniProces = null;

            if (schedulingAlgorithm == "SJF")
            {

                trenutniProces = procesiList.OrderBy(p => p.VrijemeIzvrsavanja).First();
                procesiList.Remove(trenutniProces);

                Console.WriteLine($"[SJF]   Izvršavam: {trenutniProces.Naziv} ({trenutniProces.VrijemeIzvrsavanja}s)");
                Thread.Sleep(trenutniProces.VrijemeIzvrsavanja * 1000);

                
                if (shortestProces == null ||
                    trenutniProces.VrijemeIzvrsavanja < shortestProces.VrijemeIzvrsavanja)
                {
                    shortestProces = new Proces(
                        trenutniProces.Naziv,
                        trenutniProces.VrijemeIzvrsavanja,
                        trenutniProces.Prioritet,
                        trenutniProces.ZauzeceProcessora,
                        trenutniProces.ZauzeceMemorije
                    );
                }

                
                currentCpuUsage -= trenutniProces.ZauzeceProcessora;
                currentMemoryUsage -= trenutniProces.ZauzeceMemorije;

                Console.WriteLine($"       Završeno! (CPU: {currentCpuUsage:F1}%, RAM: {currentMemoryUsage:F1}%)\n");
            }
            else 
            {

                trenutniProces = procesiList[0];
                procesiList.RemoveAt(0);

                Console.WriteLine($"[RR]   Izvršavam: {trenutniProces.Naziv} ({trenutniProces.VrijemeIzvrsavanja}s, quantum={quantum}s)");

                if (trenutniProces.VrijemeIzvrsavanja > quantum)
                {
                    
                    Thread.Sleep(quantum * 1000);
                    trenutniProces.VrijemeIzvrsavanja -= quantum;
                    procesiList.Add(trenutniProces);

                    Console.WriteLine($"       Preostalo: {trenutniProces.VrijemeIzvrsavanja}s (vraćen u red)\n");
                }
                else
                {
            
                    Thread.Sleep(trenutniProces.VrijemeIzvrsavanja * 1000);

                    
                    if (shortestProces == null ||
                        trenutniProces.OriginalnoVrijemeIzvrsavanja < shortestProces.OriginalnoVrijemeIzvrsavanja)
                    {
                        shortestProces = new Proces(
                            trenutniProces.Naziv,
                            trenutniProces.OriginalnoVrijemeIzvrsavanja,
                            trenutniProces.Prioritet,
                            trenutniProces.ZauzeceProcessora,
                            trenutniProces.ZauzeceMemorije
                        );
                    }

                    
                    currentCpuUsage -= trenutniProces.ZauzeceProcessora;
                    currentMemoryUsage -= trenutniProces.ZauzeceMemorije;

                    Console.WriteLine($"      Završeno! (CPU: {currentCpuUsage:F1}%, RAM: {currentMemoryUsage:F1}%)\n");
                }
            }

            
            if (procesiList.Count == 0 && clients.Count == 0)
            {
                Console.WriteLine("\n");

                Console.WriteLine("          STATISTIKA - SVE UTIČNICE ZATVORENE           ");
                Console.WriteLine();

                Console.WriteLine($"   NAJVEĆE ZAUZEĆE TOKOM AKTIVNOSTI SERVERA:");
                Console.WriteLine($"    Maksimalno zauzeće procesora: {maxCpuUsage:F2}%");
                Console.WriteLine($"     Maksimalno zauzeće memorije:  {maxMemoryUsage:F2}%");
                Console.WriteLine();

                if (shortestProces != null)
                {
                    Console.WriteLine($"  PROCES SA NAJKRAĆIM VREMENOM IZVRŠAVANJA:");
                    Console.WriteLine($"   ├─ Naziv:              {shortestProces.Naziv}");
                    Console.WriteLine($"   ├─ Vrijeme izvršavanja:  {shortestProces.VrijemeIzvrsavanja}s");
                    Console.WriteLine($"   ├─ Prioritet:          {shortestProces.Prioritet}");
                    Console.WriteLine($"   ├─ Zauzeće procesora:  {shortestProces.ZauzeceProcessora}%");
                    Console.WriteLine($"   └─ Zauzeće memorije:   {shortestProces.ZauzeceMemorije}%");
                }
                else
                {
                    Console.WriteLine($"  Nijedan proces nije izvršen.");
                }

                Console.WriteLine();
                Console.WriteLine("════════════════════════════════════════════════════════");
                Console.WriteLine();

                Console.WriteLine("Pritisnite bilo koji taster za zatvaranje servera...");
                Console.ReadKey();
                Environment.Exit(0);
            }
        }
    }
}