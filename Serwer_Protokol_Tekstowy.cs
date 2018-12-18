using System;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace L8_Z8_Server
{
    class Program
    {


        static void Main(string[] args)
        {
            Console.Write("****ServerApp v.1.0****\n\n");
            // Poniżej tworzymy potrzebne obiekty do przyjęcia klientów na serwer.
            // Klientów na serwerze może być maksymalnie 10 w jednym momencie, 
            // ale w kodzie można ustalić maksymalną liczbę, lub ustawić na nieskończoność.
            // W tym projekcie zdecydowałem się na ustawienie max 10, by łatwo sprawdzić "przeciążenie serwera"

            TcpListener[] server_port = new TcpListener[10]; // Obiekt czekający na połączenie ze strony klienta (tablica)
            TcpClient[] client_port = new TcpClient[10];     // Obiekt przechowujący dane na temat połączonego klienta (tablica)
            Thread[] oThread = new Thread[10];               // Wątki klientów, umożliwiają aktywność wielu klientów w tym samym czasie
            List<Klient> clients = new List<Klient>();       // Lista klasy Klient, opisanej niżej
            int i;

            // Ustawiamy wszystkich klientów na wartość "null"
            for (i = 0; i < 10; i++)
            {
                clients.Add(new Klient());
            }

            while (true)
            {
                for (i = 0; i < 10; i++)
                {
                    if (clients[i].sesja == -1)                                 // Nasłuchuje nowego klienta jeżeli jest na niego miejsce na serwerze
                    {
                        server_port[i] = new TcpListener(IPAddress.Any, 8888);  // Ustawiamy nasłuchiwany adres IP na dowolny i port na 8888
                        server_port[i].Start();                                 // Rozpoczynamy nasłuchiwanie
                        client_port[i] = default(TcpClient);
                        client_port[i] = server_port[i].AcceptTcpClient();      // Akceptujemy klienta próbującego się połączyć
                        server_port[i].Stop();                                  // Przestajemy nasłuchiwać
                        clients[i] = new Klient(i, ref client_port[i]);         // Dodajemy do listy nowego klienta, nadajemy ID sesji
                        for(int j=0;j<10;j++)                                   // W pętli aktualizujemy listę klientów dla każdego klienta
                        {
                            clients[j].clients = clients;
                        }
                        oThread[i] = new Thread(new ThreadStart(clients[i].Run));
                        oThread[i].Start();                                     // Startujemy wątek klienta
                        i = 10;                                                 
                    }

                }
            }




        }



    }

    // Klasa klient odpowiedzialna za całe połączenie klienta z serwerem i innymi użytkownikami
    class Klient                          
    {

        public TcpClient client;             // Obiekt przechowujący dane na temat klienta
        public int sesja;                    // ID Sesji klienta
        public TcpClient tosend;             /* Obiekt przechowujący dane na temat klienta z którym trwa konwersacja
                                                Ustawiony na null jeżeli aktualnie nie przebiega konwersacja */
        public List<Klient> clients;         // Lista wszystkich aktualnie połączonych klientów
        public Klient()                      // Konstruktor, sesja = -1 znacza, że nie ma połączonego klienta
        {
            sesja = -1;
        }

        public Klient(int sesja, ref TcpClient client)
        {
            this.sesja = sesja;
            this.client = client;
        }

        // Przyjmuje lub odrzuca klienta
        bool StartConnection(NetworkStream ns)
        {
            string godz;
            TimeSpan godzina;
            Console.WriteLine("***Client trying to connect***");
            string message = "";
            string[] msg = new string[5];
            msg = Czytaj(ns); // Odbiera wiadomość od klienta i ją dzieli na części
            if (msg[0] == "Hi" && msg[1] == "Ask") // Klient pyta o ID sesji
            {
                if (sesja < 10) // Jest miejsce na serwerze, przydziela id sesji
                {
                    godzina = DateTime.UtcNow.ToLocalTime().TimeOfDay;
                    godz = Convert.ToString($"{godzina.Hours}:{godzina.Minutes}:{godzina.Seconds}");
                    message = "Operacja-)Hi(|Status-)Ok(|Identyfikator-)" + Convert.ToString(sesja) + "(|Data-)(|Time-)"+godz+"(|";
                    ns.Write(Encoding.ASCII.GetBytes(message), 0, message.Length);                      // Wysyła do klienta wiadomość
                    ns.Flush();
                    Console.Write("***Accepted client " + (sesja) + "!***\n");
                    return true;
                }
                else // Odmawia przydzielenia sesji i połączenia z serwerem
                {
                    godzina = DateTime.UtcNow.ToLocalTime().TimeOfDay;
                    godz = Convert.ToString($"{godzina.Hours}:{godzina.Minutes}:{godzina.Seconds}");
                    message = "Operacja-)Hi(|Status-)No(|Identyfikator-)(|Data-)(|Time-)"+godz+"(|";
                    ns.Write(Encoding.ASCII.GetBytes(message), 0, message.Length);                      // Wysyła do klienta wiadomość
                    ns.Flush();
                    Console.Write("***Unaccepted client " + (sesja) + "!***\n");
                    return false;
                }
            }
            else // Zwrócenie erroru, błąd w protokole
            {
                godzina = DateTime.UtcNow.ToLocalTime().TimeOfDay;
                godz = Convert.ToString($"{godzina.Hours}:{godzina.Minutes}:{godzina.Seconds}");
                message = "Operacja-)Server(|Status-)Error(|Identyfikator-)(|Data-)(|Time-)"+godz+"(|";
                ns.Write(Encoding.ASCII.GetBytes(message), 0, message.Length);                          // Wysyła wiadomość do klienta
                ns.Flush();
                return false;
            }
        }


        // Odbiera wiadomość i dzieli ją na części (Operacja, Status, Identyfikator, Dane, Czas)
        public string[] Czytaj(NetworkStream ns)
        {
            string[] msgs = new string[5];
            byte[] buf = new byte[1024];
            byte[] myReadBuffer = new byte[1024];
            string msg = "";
            int numberOfBytesRead = 0;
            do
            {
                try
                {
                    numberOfBytesRead = ns.Read(myReadBuffer, 0, myReadBuffer.Length); // Odbiera dane od klienta
                }
                catch (Exception e) // Klient zerwał połączenie
                {
                    msgs[0] = "Server";
                    msgs[1] = "Bye";
                    return msgs;
                }

                msg = msg + Encoding.ASCII.GetString(myReadBuffer, 0, numberOfBytesRead);

            }
            while (ns.DataAvailable); // Dopóki są dane do odebrania

            // Niżej dzielimy otrzymaną wiadomość na części za pomocą wyrażeń regularnych

            string pattern = "(Operacja\\-\\)?|\\(\\|Status\\-\\).+|\\(\\|Identyfikator\\-\\).+|\\(\\|Data\\-\\).+|\\(\\|Time\\-\\).+|\\(\\|)";
            msgs[0] = Regex.Replace(msg, pattern, string.Empty);
            pattern = "(Operacja\\-\\)" + msgs[0] + "\\(\\|Status\\-\\)?|\\(\\|Identyfikator\\-\\).+|\\(\\|Data\\-\\).+|\\(\\|Time\\-\\).+|\\(\\|)";
            msgs[1] = Regex.Replace(msg, pattern, string.Empty);
            pattern = "(Operacja\\-\\)" + msgs[0] + "\\(\\|Status\\-\\)" + msgs[1] + "\\(\\|Identyfikator\\-\\)?|\\(\\|Data\\-\\).+|\\(\\|Time\\-\\).+|\\(\\|)";
            msgs[2] = Regex.Replace(msg, pattern, string.Empty);
            pattern = "(Operacja\\-\\)" + msgs[0] + "\\(\\|Status\\-\\)" + msgs[1] + "\\(\\|Identyfikator\\-\\)" + msgs[2] + "\\(\\|Data\\-\\)?|\\(\\|Time\\-\\).+|\\(\\|)";
            msgs[3] = Regex.Replace(msg, pattern, string.Empty);
            pattern = "(Operacja\\-\\)" + msgs[0] + "\\(\\|Status\\-\\)" + msgs[1] + "\\(\\|Identyfikator\\-\\)" + msgs[2] + "\\(\\|Data\\-\\)" + msgs[3] + "|\\(\\|Time\\-\\)?|\\(\\|)";
            msgs[4] = Regex.Replace(msg, pattern, string.Empty);
            return msgs;
        }
        public void Run()                                // Funkcja aktywności klienta na serwerze
        {

            NetworkStream ns = client.GetStream();       // Tworzymy strumień komunikacji z klientem
            bool connect = StartConnection(ns);          // Jeżeli jest miejsce na serwerze to startujemy klienta
            while (connect)
            {
                string godz;
                TimeSpan godzina;
                string[] msgs = new string[4];
                string message = "";
                msgs = Czytaj(ns);                       // Odbieramy wiadomość od klienta


                // zaproszenie do sesji
                if (msgs[0] == "Chat" && msgs[1] == "Ask")
                {
                    tosend = clients[Convert.ToInt32(msgs[3])].client;
                    here:
                    if (tosend == null) // Jeżeli klient z którym chce się połączyć nie istnieje
                    {

                        godzina = DateTime.UtcNow.ToLocalTime().TimeOfDay;
                        godz = Convert.ToString($"{godzina.Hours}:{godzina.Minutes}:{godzina.Seconds}");
                        message = "Operacja-)Server(|Status-)Noclient(|Identyfikator-)" + sesja + "(|Data-)(|Time-)" + godz + "(|";
                        ns.Write(Encoding.ASCII.GetBytes(message), 0, message.Length); // Wysyłamy wiadomość o braku klienta z tym ID
                        ns.Flush();
                    }
                    else // Jeżeli klient istnieje
                    {
                        message = "Operacja-)" + msgs[0] + "(|Status-)" + msgs[1] + "(|Identyfikator-)" + msgs[2] + "(|Data-)" + msgs[3] + "(|Time-)" + msgs[4] + "(|";
                        byte[] bytes = Encoding.ASCII.GetBytes(message);
                        try
                        {
                            NetworkStream nss = tosend.GetStream();
                            nss.Write(bytes, 0, message.Length); // Wysyłamy zaproszenie do drugiego klienta
                            nss.Flush();
                        }
                        catch (Exception e)
                        {
                            tosend = null;
                            goto here;
                        }
                    }
                }
                // Odrzucenie zaproszenia
                else if (msgs[0] == "Chat" && msgs[1] == "No")
                {
                    tosend = clients[Convert.ToInt32(msgs[3])].client;
                    message = "Operacja-)Chat(|Status-)No(|Identyfikator-)" + msgs[2] + "(|Data-)" + msgs[3] + "(|Time-)" + msgs[4] + "(|";
                    byte[] bytes = Encoding.ASCII.GetBytes(message);
                    NetworkStream nss = tosend.GetStream();
                    nss.Write(bytes, 0, message.Length); // Wysłanie odpowiedzi
                    nss.Flush();
                    tosend = null;
                }
                // Zaakceptowanie zaproszenia
                else if (msgs[0] == "Chat" && msgs[1] == "Ok")
                {
                    tosend = clients[Convert.ToInt32(msgs[3])].client; // Ustawienie powiązanego klienta
                    message = "Operacja-)Chat(|Status-)Ok(|Identyfikator-)" + msgs[2] + "(|Data-)" + msgs[3] + "(|Time-)" + msgs[4] + "(|";
                    byte[] bytes = Encoding.ASCII.GetBytes(message);
                    NetworkStream nss = tosend.GetStream();
                    nss.Write(bytes, 0, message.Length); // Wysłanie odpowiedzi
                    nss.Flush();
                }
                // Odebralem dane
                else if (msgs[0] == "Chat" && msgs[1] == "Gotit")
                {
                    message = "Operacja-)Chat(|Status-)Gotit(|Identyfikator-)" + sesja + "(|Data-)(|Time-)" + msgs[4] + "(|";
                    byte[] bytes = Encoding.ASCII.GetBytes(message);
                    NetworkStream nss = tosend.GetStream();
                    nss.Write(bytes, 0, message.Length); // Przeslanie odpowiedzi
                    nss.Flush();
                }
                // Koncze polaczenie z serwerem
                else if (msgs[0] == "Server" && msgs[1] == "Bye")
                {
                    message = "Operacja-)Server(|Status-)Noclient(|Identyfikator-)" + sesja + "(|Data-)(|";
                    byte[] mess = Encoding.ASCII.GetBytes(message);
                    try
                    {
                        NetworkStream nss = tosend.GetStream();
                        nss.Write(mess, 0, message.Length); // Przesłanie info o rozłączeniu klienta, jeżeli jest w trakcie rozmowy
                        nss.Flush();
                    }
                    catch (Exception e)
                    {

                    }
                    Console.WriteLine("Client " + Convert.ToString(sesja) + "disconnected!");
                    client.Close(); // Rozłączenie klienta
                    sesja = -1;
                    tosend = null;
                    return;
                }
                // Przesłanie wiadomości
                else if (msgs[0] == "Chat" && msgs[1] == "Send")
                {
                    message = "Operacja-)" + msgs[0] + "(|Status-)" + msgs[1] + "(|Identyfikator-)" + msgs[2] + "(|Data-)" + msgs[3] + "(|Time-)" + msgs[4] + "(|";
                    byte[] bytes = Encoding.ASCII.GetBytes(message);
                    if (tosend != null)
                    {
                        try
                        {
                            NetworkStream nss = tosend.GetStream();
                            nss.Write(bytes, 0, message.Length); // Przesłanie wiadomości
                            nss.Flush();
                        }
                        catch (Exception e)
                        {

                        }

                    }
                }
                // Rozłączenie czatu
                else if (msgs[0] == "Chat" && msgs[1] == "Bye")
                {
                    tosend = clients[Convert.ToInt32(msgs[3])].client;
                    message = "Operacja-)Chat(|Status-)Bye(|Identyfikator-)" + sesja + "(|Data-)"+msgs[3]+"(|Time-)"+msgs[4]+"(|";
                    byte[] mess = Encoding.ASCII.GetBytes(message);
                    NetworkStream nss = tosend.GetStream(); // Przesłanie info o rozłączeniu do drugiego klienta
                    nss.Write(mess, 0, message.Length);
                    nss.Flush();
                    tosend = null; // Rozłączenie czatu
                }
            }
            sesja = -1;
            tosend = null;
            client.Close();
        }
    }

}


