using System;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Text.RegularExpressions;

namespace Klient_Projekt_Zad8
{

    class Program
    {
        // Odbiera wiadomość i dzieli ją na części (Operacja, Status, Identyfikator, Dane, Czas)
        static string[] Odbierz(NetworkStream ns)
        {

            byte[] buf = new byte[1024];
            byte[] myReadBuffer = new byte[1024];
            string msg = "";
            int numberOfBytesRead = 0;
            do // Odbiera dane dopóki są jakieś do odebrania
            {
                numberOfBytesRead = ns.Read(myReadBuffer, 0, myReadBuffer.Length);

                msg = msg + Encoding.ASCII.GetString(myReadBuffer, 0, numberOfBytesRead);

            }
            while (ns.DataAvailable);

            // Poniższe funkcje dzielą wiadomość za pomocą wyrażeń regularnych
            string[] msgs = new string[5];
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
        // Funkcja odpowiedzialna za czat między dwoma klientami za pośrednictwem serwera
       static bool Polaczenie(NetworkStream ns, int sesja,int sesja2, ref bool conn)
        {
            TimeSpan godzina;
            string godz;
            string message;
            string[] msgs = new string[5];
            Pisanie pisanie = new Pisanie(ns, sesja,sesja2); // Klasa odpowiedzialna za wysyłanie wiadomości wpisanej przez użytkownika
            Thread oThread = new Thread(new ThreadStart(pisanie.Run)); // Powyższa klasa wykonywana jest w osobnym wątku
            oThread.Start(); // Rozpoczęcie wątku

            while (pisanie.connect)
            {
                msgs = Odbierz(ns); // Odebranie danych

                // Drugi klient przesyła wiadomość
                if (msgs[0] == "Chat" && msgs[1] == "Send")
                {
                    godzina = DateTime.UtcNow.ToLocalTime().TimeOfDay;
                    godz = Convert.ToString($"{godzina.Hours}:{godzina.Minutes}:{godzina.Seconds}");
                    message = "Operacja-)Chat(|Status-)Gotit(|Identyfikator-)" + sesja + "(|Data-)(|Time-)"+godz+"(|";
                    byte[] bytes = Encoding.ASCII.GetBytes(message);
                    ns.Write(bytes, 0, message.Length); // Wysłanie powiadomienia o otrzymaniu wiadomości
                    ns.Flush();
                    msgs[3] = Regex.Replace(msgs[3], "~", " ");
                    godzina = DateTime.UtcNow.ToLocalTime().TimeOfDay;
                    // Wyświetlenie wiadomości
                    Console.WriteLine($"{godzina.Hours}:{godzina.Minutes}:{godzina.Seconds} " + "Klient " + Convert.ToString(sesja2) + ": " + msgs[3]);
                }
                // Potwierdzenie otrzymania wiadomości
                else if (msgs[0] == "Chat" && msgs[1] == "Gotit")
                {
                    godzina = DateTime.UtcNow.ToLocalTime().TimeOfDay;
                    Console.WriteLine($"{godzina.Hours}:{godzina.Minutes}:{godzina.Seconds} " + "Second client got message");
                }
                // Drugi klient rozłączył się z serwerem
                else if (msgs[0] == "Server" && msgs[1] == "Noclient")
                {
                    godzina = DateTime.UtcNow.ToLocalTime().TimeOfDay;
                    Console.WriteLine($"{godzina.Hours}:{godzina.Minutes}:{godzina.Seconds} " + "Client " + sesja2 + " disconnected...");
                    conn = false;
                    oThread.Abort();
                    return false;
                }
                // Drugi klient zakończył czat
                else if (msgs[0] == "Chat" && msgs[1] == "Bye")
                {
                    if (pisanie.connect != false)
                    {
                        godzina = DateTime.UtcNow.ToLocalTime().TimeOfDay;
                        godz = Convert.ToString($"{godzina.Hours}:{godzina.Minutes}:{godzina.Seconds}");
                        message = "Operacja-)Chat(|Status-)Bye(|Identyfikator-)" + sesja + "(|Data-)" + sesja2 + "(|Time-)" + godz + "(|";
                        ns.Write(Encoding.ASCII.GetBytes(message), 0, message.Length); // Rozłączenie się z pokojem czatu
                        ns.Flush();
                    }
                    godzina = DateTime.UtcNow.ToLocalTime().TimeOfDay;
                    Console.WriteLine($"{godzina.Hours}:{godzina.Minutes}:{godzina.Seconds} " + "Second client disconnected");
                    conn = false;
                    oThread.Abort(); // Zakończenie wątku wczytującego z bufora
                    return false;
                }
            }
            oThread.Abort();
            return false;
        }

        static void Main(string[] args)
        {
            string godz;
            TimeSpan godzina;
            godzina = DateTime.UtcNow.ToLocalTime().TimeOfDay;
            Console.Write($"{godzina.Hours}:{godzina.Minutes}:{godzina.Seconds} " + "***ClientApp v.1.0***\n");
            start:
            Console.Write($"{godzina.Hours}:{godzina.Minutes}:{godzina.Seconds} " + "***Enter Server IP: ");
            string ip = Console.ReadLine(); // Pobiera IP serwera od klienta

            TcpClient client = new TcpClient(); 
            try
            {
                client.Connect(IPAddress.Parse(ip), 8888); // Próbuje połączyć się z serwerem o podanym IP na porcie 8888
            }
            catch (SocketException se)
            {
                godzina = DateTime.UtcNow.ToLocalTime().TimeOfDay;
                Console.WriteLine($"{godzina.Hours}:{godzina.Minutes}:{godzina.Seconds} " + "I can't connect with server... Closing app...");
                Console.ReadKey();
                goto start;
            }
            NetworkStream ns = client.GetStream(); // Tworzy strumień połączenia z serwerem
            godzina = DateTime.UtcNow.ToLocalTime().TimeOfDay;
            godz = Convert.ToString($"{godzina.Hours}:{godzina.Minutes}:{godzina.Seconds}");
            string message = "Operacja-)Hi(|Status-)Ask(|Identyfikator-)(|Data-)(|Time-)"+godz+"(|";
            ns.Write(Encoding.ASCII.GetBytes(message), 0, message.Length); // Prosi serwer o ID sesji
            ns.Flush();
            string[] msgs = new string[4];
            msgs = Odbierz(ns); // Odbiera odpowiedź od serwera

            // Polaczenie zaakceptowane przez serwer
            if (msgs[0] == "Hi" && msgs[1] == "Ok")
            {

                godzina = DateTime.UtcNow.ToLocalTime().TimeOfDay;
                Console.WriteLine($"{godzina.Hours}:{godzina.Minutes}:{godzina.Seconds} " + "***Host connected to server!***\n");
                int sesja = Convert.ToInt32(msgs[2]);
                godzina = DateTime.UtcNow.ToLocalTime().TimeOfDay;
                Console.WriteLine($"{godzina.Hours}:{godzina.Minutes}:{godzina.Seconds} " + "Your sesion id: " + msgs[2]);
                while(true)
                {
                    here2:
                    Laczenie pytaj = new Laczenie(ns, sesja); // Klasa odpowiedzialna pobieranie id sesji do połączenia od użytkownika
                    Thread oThread = new Thread(new ThreadStart(pytaj.Run));
                    oThread.Start(); // Start wątku powyższej klasy
                    msgs = Odbierz(ns); // Pobieranie odpowiedzi od drugiego klienta lub pytania o połączenie
                    oThread.Abort(); // Koniec wątku klasy Laczenie

                    // Pytanie o połączenie od drugiego klienta
                    if (msgs[0]=="Chat" && msgs[1] == "Ask")
                    {
                        while (true)
                        {
                            Console.WriteLine("Client " + msgs[2] + " want to chat with you. Enter [1] to connect or [2] to reject: ");
                            string input = Console.ReadLine();
                            if (input == "1") // Jeżeli użytkownik się zgodzi
                            {
                                Console.WriteLine("Connected with Client. Say something: ");
                                godzina = DateTime.UtcNow.ToLocalTime().TimeOfDay;
                                godz = Convert.ToString($"{godzina.Hours}:{godzina.Minutes}:{godzina.Seconds}");
                                message = "Operacja-)Chat(|Status-)Ok(|Identyfikator-)" + sesja + "(|Data-)"+msgs[2]+"(|Time-)" + godz + "(|";
                                ns.Write(Encoding.ASCII.GetBytes(message), 0, message.Length); // Wysłanie odpowiedzi drugiemu klientowi
                                ns.Flush();
                                bool conn=true;
                                
                                while(Polaczenie(ns, sesja, Convert.ToInt32(msgs[2]), ref conn)) // Pętla czatu
                                {

                                }
                                break;
                            }
                            else if (input == "2") // Jeżeli użytkownik odmówi
                            {
                                godzina = DateTime.UtcNow.ToLocalTime().TimeOfDay;
                                godz = Convert.ToString($"{godzina.Hours}:{godzina.Minutes}:{godzina.Seconds}");
                                message = "Operacja-)Chat(|Status-)No(|Identyfikator-)" + sesja + "(|Data-)"+msgs[2]+"(|Time-)" + godz + "(|";
                                ns.Write(Encoding.ASCII.GetBytes(message), 0, message.Length); // Wysyłamy odpowiedź do drugiego użytkownika
                                ns.Flush();
                                break;
                            }
                            else if(input == "") { } 
                            else
                            {
                                Console.WriteLine("Wrong input!");
                            }
                        }
                    }
                    // Drugi klient zaakceptował zaproszenie
                    else if (msgs[0]=="Chat" && msgs[1] == "Ok")
                    {
                        Console.WriteLine("Client connected! Say something: ");
                        int sesja2 = pytaj.sesja2;
                        bool connect = true;
                        while (Polaczenie(ns, sesja, sesja2, ref connect)) { } // Pętla czatu
                        goto here2;
                    }
                    // Drugi klient odrzucił zaproszenie
                    else if (msgs[1] == "No")
                    {
                        godzina = DateTime.UtcNow.ToLocalTime().TimeOfDay;
                        Console.WriteLine($"{godzina.Hours}:{godzina.Minutes}:{godzina.Seconds} " + "Client don't want to chat with you...");
                    }
                    // Brak klienta z takim ID
                    else if (msgs[1] == "Noclient")
                    {
                        godzina = DateTime.UtcNow.ToLocalTime().TimeOfDay;
                        Console.WriteLine($"{godzina.Hours}:{godzina.Minutes}:{godzina.Seconds} " + "No client with this ID...");
                    }
                    else
                    {
                        godzina = DateTime.UtcNow.ToLocalTime().TimeOfDay;
                        Console.WriteLine($"{godzina.Hours}:{godzina.Minutes}:{godzina.Seconds} " + "Ups something wrong, try again...");
                    }

                }
                
            }
            // Serwer odmówił połączenia
            else if (msgs[0] == "Hi" && msgs[1] == "No")
            {
                godzina = DateTime.UtcNow.ToLocalTime().TimeOfDay;
                Console.WriteLine($"{godzina.Hours}:{godzina.Minutes}:{godzina.Seconds} " + "***Host can't connect with server!***");
                client.Close();
                Console.ReadKey();
                return;
            }
            else
            {
                godzina = DateTime.UtcNow.ToLocalTime().TimeOfDay;
                Console.WriteLine($"{godzina.Hours}:{godzina.Minutes}:{godzina.Seconds} " + "Problems with connection, you will be disconnected...");
                client.Close();
                Console.ReadKey();
                return;

            }
            

        }

        // Obiekt odpowiedzialny za wysyłanie zaproszenia do czatu
        class Laczenie
        {
            public NetworkStream ns; 
            public int sesja;
            public int sesja2; // sesja drugiego klienta
            public Laczenie(NetworkStream ns, int sesja)
            {
                this.ns = ns;
                this.sesja = sesja;
            }
            public void Run()
            {
                here:
                TimeSpan godzina;
                string godz;
                string message;
                godzina = DateTime.UtcNow.ToLocalTime().TimeOfDay;
                Console.WriteLine($"{godzina.Hours}:{godzina.Minutes}:{godzina.Seconds} "+ "Enter ID to connect with client: ");
                string txt = Console.ReadLine(); // Wczytanie od użytkownika danych

                // Rozłączenie z serwerem
                if(txt=="Exit") 
                {
                    godzina = DateTime.UtcNow.ToLocalTime().TimeOfDay;
                    godz = Convert.ToString($"{godzina.Hours}:{godzina.Minutes}:{godzina.Seconds}");
                    message = "Operacja-)Server(|Status-)Bye(|Identyfikator-)" + sesja + "(|Data-)(|Time-)"+godz+"(|";
                    ns.Write(Encoding.ASCII.GetBytes(message), 0, message.Length);
                    ns.Flush();
                    godzina = DateTime.UtcNow.ToLocalTime().TimeOfDay;
                    Console.WriteLine($"{godzina.Hours}:{godzina.Minutes}:{godzina.Seconds} " + "You are disconnected");

                    System.Diagnostics.Process.GetCurrentProcess().Kill();
                }
                else
                {
                    try
                    {
                        sesja2 = Convert.ToInt32(txt); // Konwersja na wartość int i przypisanie do sesja2
                    }
                    catch(Exception e)
                    {
                        Console.WriteLine("Wrong ID, try again...");
                        txt = "";
                        goto here;
                    }
                    // sesja2 mniejsza od 10 (max liczba użytkowników) i nie równa sesji użytkownika
                    if(sesja2<10 && sesja2>=0 && sesja2!=sesja)
                    {
                        godzina = DateTime.UtcNow.ToLocalTime().TimeOfDay;
                        godz = Convert.ToString($"{godzina.Hours}:{godzina.Minutes}:{godzina.Seconds}");
                        message = "Operacja-)Chat(|Status-)Ask(|Identyfikator-)" + sesja + "(|Data-)"+sesja2+"(|Time-)"+godz+"(|";
                        ns.Write(Encoding.ASCII.GetBytes(message), 0, message.Length); // Wysłanie zaproszenia
                        ns.Flush();
                        Console.WriteLine("Waiting for answer...");
                        return;
                    }
                    else
                    {
                        Console.WriteLine("Wrong ID, try again...");
                        txt = "";
                        goto here;
                    }
                }
            }
           
        }

        // Obiekt odpowiedzialny za wczytanie i wysłanie wiadomości wprowadzonej przez użytkownika
        class Pisanie
        {
            public NetworkStream ns;
            public int sesja;
            public bool connect;
            public int sesja2;
            public Pisanie(NetworkStream ns, int sesja,int sesja2)
            {
                this.ns = ns;
                this.sesja = sesja;
                this.sesja2 = sesja2;
                this.connect = true;
            }
            public void Run()
            {
                string godz;
                TimeSpan godzina;
                string txt;
                string message;
                while (true)
                {
                    txt = Console.ReadLine(); // Wczytuje wiadomość od użytkownika
                    txt = Regex.Replace(txt, " ", "~"); // Zamiana spacji na "~" (spacje niestety usuwa po zamianie na bajty)

                    // Rozłączenie z serwerem
                    if (txt == "Exit")
                    {
                        godzina = DateTime.UtcNow.ToLocalTime().TimeOfDay;
                        godz = Convert.ToString($"{godzina.Hours}:{godzina.Minutes}:{godzina.Seconds}");
                        message = "Operacja-)Server(|Status-)Bye(|Identyfikator-)" + sesja + "(|Data-)(|Time-)"+godz+"(|";
                        ns.Write(Encoding.ASCII.GetBytes(message), 0, message.Length); // Wyslanie wiadomosci do serwera o rozłączeniu
                        ns.Flush();
                        godzina = DateTime.UtcNow.ToLocalTime().TimeOfDay;
                        Console.WriteLine($"{godzina.Hours}:{godzina.Minutes}:{godzina.Seconds} " + "You are disconnected");
                        Console.ReadKey();
                        System.Diagnostics.Process.GetCurrentProcess().Kill(); // Zakończenie pracy wszystkich wątków (również głównego)
                    }
                    // Rozłączenie czatu
                    if(txt == "Bye")
                    {
                        godzina = DateTime.UtcNow.ToLocalTime().TimeOfDay;
                        godz = Convert.ToString($"{godzina.Hours}:{godzina.Minutes}:{godzina.Seconds}");
                        message = "Operacja-)Chat(|Status-)Bye(|Identyfikator-)" + sesja + "(|Data-)"+sesja2+"(|Time-)" + godz + "(|";
                        ns.Write(Encoding.ASCII.GetBytes(message), 0, message.Length); // Wysłanie wiadomości o rozłączeniu czatu
                        ns.Flush();
                        sesja = -1;
                        connect = false;
                        return;
                    }

                    if (txt == "") { }
                    // Przesłanie wiadomości do połączonego klienta
                    else
                    {
                        godzina = DateTime.UtcNow.ToLocalTime().TimeOfDay;
                        godz = Convert.ToString($"{godzina.Hours}:{godzina.Minutes}:{godzina.Seconds}");
                        message = "Operacja-)Chat(|Status-)Send(|Identyfikator-)" + sesja + "(|Data-)" + txt + "(|Time-)"+godz+"(|";
                        ns.Write(Encoding.ASCII.GetBytes(message), 0, message.Length); // Wysłanie wiadomości
                        ns.Flush();
                    }
                }
            }
        }

    }
}

