using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

class Client
{
    private const string SERVER_IP = "127.0.0.1";
    private const int SERVER_UDP_PORT = 51000;

    static void Main(string[] args)
    {
        Console.WriteLine("--- Klijentska aplikacija pokrenuta ---");

        Socket udpSocket = new Socket(AddressFamily.InterNetwork,
                                      SocketType.Dgram,
                                      ProtocolType.Udp);

        IPEndPoint serverEndPoint = new IPEndPoint(IPAddress.Parse(SERVER_IP),
                                                   SERVER_UDP_PORT);

        string registrationMessage = "REGISTER";
        byte[] messageBytes = Encoding.UTF8.GetBytes(registrationMessage);

        try
        {
            udpSocket.SendTo(messageBytes, serverEndPoint);
            Console.WriteLine("[UDP] Poslata poruka 'REGISTER' serveru...");

            byte[] buffer = new byte[1024];
            EndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

            int bytesReceived = udpSocket.ReceiveFrom(buffer, ref remoteEndPoint);
            string response = Encoding.UTF8.GetString(buffer, 0, bytesReceived);

            if (int.TryParse(response, out int tcpPort))
            {
                Console.WriteLine($"[UDP] Uspješna prijava! Primljen TCP port: {tcpPort}");

                Socket tcpSocket = new Socket(AddressFamily.InterNetwork,
                                              SocketType.Stream,
                                              ProtocolType.Tcp);

                IPEndPoint tcpEndPoint = new IPEndPoint(IPAddress.Parse(SERVER_IP), tcpPort);

                tcpSocket.Connect(tcpEndPoint);
                Console.WriteLine($"[TCP] Uspostavljena konekcija sa serverom na portu {tcpPort}!");

                tcpSocket.Close();
            }
            else
            {
                Console.WriteLine("[Greska] Server nije poslao validan port.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Greška pri komunikaciji: {ex.Message}");
        }
        finally
        {
            udpSocket.Close();
            Console.WriteLine("\nKlijent završio. Pritisnite bilo koji taster za kraj.");
            Console.ReadKey();
        }
    }
}