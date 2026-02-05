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
    // Zadatak 7: Klasa za čuvanje informacija o svakom klijentu
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
        // Zadatak 2: Dve double promenljive za trenutno zauzeće
        private static double currentCpuUsage = 0.0;
        private static double currentMemoryUsage = 0.0;

        private const int UDP_PORT = 51000;
        private const int TCP_PORT_FOR_CLIENTS = 52000;
        private const int BUFFER_SIZE = 8192;
        private const double MAX_CPU = 100.0;
        private const double MAX_MEMORY = 100.0;

        private static string schedulingAlgorithm = "";
        private static int quantum = 0;
        private static List<Proces> procesiList = new List<Proces>();

        // Zadatak 7: Lista svih konektovanih klijenata
        private static List<ClientInfo> clients = new List<ClientInfo>();

        static void Main(string[] args)
        {
            Console.WriteLine("╔════════════════════════════════════════╗");
            Console.WriteLine("║    RASPOREĐIVAČ PROCESA - SERVER      ║");
            Console.WriteLine("╚════════════════════════════════════════╝");
            Console.WriteLine();

            // Zadatak 2: Izbor algoritma raspoređivanja
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

            // Zadatak 2: UDP Socket za prijavu klijenata
            Socket udpSocket = new Socket(AddressFamily.InterNetwork,
                                          SocketType.Dgram,
                                          ProtocolType.Udp);
            IPEndPoint udpEndPoint = new IPEndPoint(IPAddress.Any, UDP_PORT);
            udpSocket.Bind(udpEndPoint);
            udpSocket.Blocking = false; // Zadatak 7: Neblokirajući režim

            // Zadatak 2: TCP Listen Socket
            Socket tcpListenSocket = new Socket(AddressFamily.InterNetwork,
                                               SocketType.Stream,
                                               ProtocolType.Tcp);
            IPEndPoint tcpEndPoint = new IPEndPoint(IPAddress.Any, TCP_PORT_FOR_CLIENTS);
            tcpListenSocket.Bind(tcpEndPoint);
            tcpListenSocket.Listen(10);
            tcpListenSocket.Blocking = false; // Zadatak 7: Neblokirajući režim

            Console.WriteLine();
            Console.WriteLine("════════════════════════════════════════");
            Console.WriteLine("✅ Server uspješno pokrenut!");
            Console.WriteLine($"   UDP port:  {UDP_PORT}");
            Console.WriteLine($"   TCP port:  {TCP_PORT_FOR_CLIENTS}");
            Console.WriteLine($"   Algoritam: {schedulingAlgorithm}" +
                            (schedulingAlgorithm == "RoundRobin" ? $" (quantum={quantum}s)" : ""));
            Console.WriteLine("════════════════════════════════════════");
            Console.WriteLine("\nČekam klijente...\n");

            // === ZADATAK 7: Glavna petlja sa Select multipleksiranjem ===
            while (true)
            {
                // Zadatak 7: Lista socket-a za praćenje
                List<Socket> readSockets = new List<Socket>();

                readSockets.Add(udpSocket);
                readSockets.Add(tcpListenSocket);

                // Zadatak 7: Dodaj sve klijentske TCP socket-e
                foreach (var client in clients)
                {
                    readSockets.Add(client.TcpSocket);
                }

                try
                {
                    // Zadatak 7: Select - čeka događaje na socket-ima
                    // Timeout: 1000000 microsekundi = 1 sekunda
                    Socket.Select(readSockets, null, null, 1000000);

                    // Obradi sve socket-e koji imaju podatke
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

                    // Obrada procesa iz liste
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

        // Zadatak 7: Obrada UDP registracije
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
                    Console.WriteLine($"[UDP] 📝 Primljena prijava od: {clientEndPoint}");

                    // Zadatak 2: Pošalji TCP port klijentu
                    string response = TCP_PORT_FOR_CLIENTS.ToString();
                    byte[] responseBytes = Encoding.UTF8.GetBytes(response);
                    udpSocket.SendTo(responseBytes, clientEndPoint);

                    Console.WriteLine($"[UDP] ✉️  Poslat TCP port {response} klijentu.\n");
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

        // Zadatak 7: Prihvatanje nove TCP konekcije
        static void HandleNewTcpConnection(Socket tcpListenSocket)
        {
            try
            {
                Socket acceptedSocket = tcpListenSocket.Accept();

                // Zadatak 7: Postavi u neblokirajući režim
                acceptedSocket.Blocking = false;

                // Zadatak 7: Kreiraj ClientInfo i dodaj u listu
                ClientInfo newClient = new ClientInfo(acceptedSocket);
                clients.Add(newClient);

                Console.WriteLine($"[TCP] 🔗 Novi klijent povezan: {newClient.EndPoint}");
                Console.WriteLine($"      👥 Ukupno aktivnih klijenata: {clients.Count}\n");
            }
            catch (SocketException ex)
            {
                if (ex.SocketErrorCode != SocketError.WouldBlock)
                {
                    Console.WriteLine($"[TCP Accept Greška] {ex.Message}");
                }
            }
        }

        // Zadatak 7: Obrada podataka od klijenta
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
                    // Klijent se diskonektovao
                    Console.WriteLine($"[TCP] ❌ Klijent {client.EndPoint} se diskonektovao.");

                    // Zadatak 6: Prosleđivanje preostalih procesa
                    if (client.PendingProcesi.Count > 0)
                    {
                        Console.WriteLine($"      📦 Dodajem {client.PendingProcesi.Count} pending procesa...");

                        foreach (var proces in client.PendingProcesi)
                        {
                            if ((currentCpuUsage + proces.ZauzeceProcessora <= MAX_CPU) &&
                                (currentMemoryUsage + proces.ZauzeceMemorije <= MAX_MEMORY))
                            {
                                procesiList.Add(proces);
                                currentCpuUsage += proces.ZauzeceProcessora;
                                currentMemoryUsage += proces.ZauzeceMemorije;

                                Console.WriteLine($"         ✅ Dodat: {proces.Naziv}");
                            }
                        }
                    }

                    socket.Close();
                    clients.Remove(client);
                    Console.WriteLine($"      👥 Preostalo aktivnih klijenata: {clients.Count}\n");
                    return;
                }

                // Zadatak 3: Deserijalizacija objekta Proces
                string json = Encoding.UTF8.GetString(buffer, 0, bytesReceived);
                Proces proces = JsonSerializer.Deserialize<Proces>(json);

                Console.WriteLine($"[PRIMLJEN od {client.EndPoint}]");
                Console.WriteLine($"  {proces}");

                // Zadatak 3: Provera resursa
                if ((currentCpuUsage + proces.ZauzeceProcessora <= MAX_CPU) &&
                    (currentMemoryUsage + proces.ZauzeceMemorije <= MAX_MEMORY))
                {
                    // Zadatak 3: Dodaj u listu
                    procesiList.Add(proces);
                    currentCpuUsage += proces.ZauzeceProcessora;
                    currentMemoryUsage += proces.ZauzeceMemorije;

                    Console.WriteLine($"  ✅ Prihvaćen! (CPU: {currentCpuUsage:F1}%, RAM: {currentMemoryUsage:F1}%)\n");
                }
                else
                {
                    // Zadatak 3: Proces se čuva na klijentu (pending)
                    client.PendingProcesi.Add(proces);
                    Console.WriteLine($"  ❌ Nedovoljno resursa! Čuva se kao pending.\n");
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

        // Obrada sledećeg procesa iz liste
        static void ProcessNext()
        {
            if (procesiList.Count == 0) return;

            Proces proces = null;

            if (schedulingAlgorithm == "SJF")
            {
                // Zadatak 4: SJF - najkraći prvi
                proces = procesiList.OrderBy(p => p.VremeIzvrsavanja).First();
                procesiList.Remove(proces);

                Console.WriteLine($"[SJF] ⚙️  Izvršavam: {proces.Naziv} ({proces.VremeIzvrsavanja}s)");
                Thread.Sleep(proces.VremeIzvrsavanja * 1000);

                // Zadatak 6: Oslobodi resurse
                currentCpuUsage -= proces.ZauzeceProcessora;
                currentMemoryUsage -= proces.ZauzeceMemorije;

                Console.WriteLine($"      ✅ Završeno! (CPU: {currentCpuUsage:F1}%, RAM: {currentMemoryUsage:F1}%)\n");
            }
            else // Round Robin
            {
                // Zadatak 5: Round Robin
                proces = procesiList[0];
                procesiList.RemoveAt(0);

                Console.WriteLine($"[RR] ⚙️  Izvršavam: {proces.Naziv} ({proces.VremeIzvrsavanja}s, quantum={quantum}s)");

                if (proces.VremeIzvrsavanja > quantum)
                {
                    // Zadatak 5: Nije završen - izvršava samo quantum
                    Thread.Sleep(quantum * 1000);
                    proces.VremeIzvrsavanja -= quantum;
                    procesiList.Add(proces); // Zadatak 5: Vrati nazad u listu

                    Console.WriteLine("$Preostalo: {proces.VremeIzvrsavanja}s (vraćen u red)\n");
                }
                else
                {
                    Thread.Sleep(proces.VrijemeIzvrsavanja * 1000);

                    currentCpuUsage -= proces.ZauzeceProcessora;
                    currentMemoryUsage -= proces.ZauzeceMemorije;

                    Console.WriteLine($"Završeno! (CPU: {currentCpuUsage:F1}%, RAM: {currentMemoryUsage:F1}%)\n");
                }
            }

            if (procesiList.Count == 0 && clients.Count == 0)
            {
                Console.WriteLine("\n════════════════════════════════════════");
                Console.WriteLine("Svi procesi izvršeni!");
                Console.WriteLine("Svi klijenti diskonektovani!");
                Console.WriteLine("════════════════════════════════════════\n");
            }
        }
    }
}