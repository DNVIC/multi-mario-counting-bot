using System;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.IO;


namespace _602countingbot
{
    
    class Program
    {
        private static string _user;
        private static string _oauth;
        private static string _channel;
        private static string _username;
        private static string _path;
        private static string _index;
        private static IPAddress _ip;

        
        private static void assignStrings()
        {
            LoginCredentials loginCredentials = new LoginCredentials();
            string CredentialsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"credentials.json");
            try
            {
                loginCredentials = LoginCredentials.GetCredentials(CredentialsPath);
            } 
            catch
            {
                var dlg = new FolderPicker();
                dlg.Title = "Select your split index folder";
                dlg.InputPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                if (dlg.ShowDialog(IntPtr.Zero) == true)
                {
                    loginCredentials.path = dlg.ResultPath;
                }
                
                    

                Console.Write("Insert bot username: ");
                loginCredentials.user = Console.ReadLine();

                Console.Write("Insert bot oauth ID: ");
                loginCredentials.oauth = Console.ReadLine();

                Console.Write("Insert twitch channel for the bot to autocount in: ");
                loginCredentials.channel = Console.ReadLine();

                Console.Write("Insert the twitch username for the account you're counting for: ");
                loginCredentials.username = Console.ReadLine();

                Console.Write("Insert IP from LiveSplit Server: ");
                loginCredentials.ip = Console.ReadLine();

                LoginCredentials.SaveCredentials(loginCredentials, CredentialsPath);
            }

            


            _ip = IPAddress.Parse(loginCredentials.ip);
            _user = loginCredentials.user;
            _oauth = loginCredentials.oauth;
            _channel = loginCredentials.channel;
            _username = loginCredentials.username;
            _path = loginCredentials.path;
        }

        static async Task Main(string[] args)
        {
            assignStrings();
            string[] files = Directory.GetFiles(_path);
            string[] textFiles = Array.FindAll(files, c => c.EndsWith(".txt"));
            while(true)
            {
                if(textFiles.Length == 1)
                {
                    Console.WriteLine(textFiles[0] + " was selected");
                    _index = textFiles[0];
                    break;
                }
                for(var i = 0; i < textFiles.Length; i++)
                {
                    Console.WriteLine(i + 1 + ": " + textFiles[i]);
                }
                Console.Write("Select an index file to be used: ");
                try
                {
                    var input = Int32.Parse(Console.ReadLine()) - 1;
                    Console.WriteLine(textFiles[input] + " was selected");
                    _index = textFiles[input];
                    break;
                } 
                catch
                {
                    Console.WriteLine("Invalid input. Please try again.");
                }
            }
            Console.WriteLine("Make sure to start your livesplit server before hitting enter here.");
            Console.ReadLine();
            await ExecuteClient();
        }
        static async Task ExecuteClient()
        {
            while(true)
            {
                // Base socket code taken from https://geeksforgeeks.org/socket-programming-in-c-sharp/
                try
                {
                    IPHostEntry ipHost = Dns.GetHostEntry(Dns.GetHostName());
                    IPAddress ipaddr = _ip;
                    IPEndPoint localEndPoint = new IPEndPoint(ipaddr, 16834);
                    Console.WriteLine(ipaddr.ToString());
                    Socket sender = new Socket(ipaddr.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

                    try
                    {
                        //Connect socket to endpoint
                        sender.Connect(localEndPoint);
                        byte[] ByteBuffer = new byte[1024];



                        //Print information that means we are good
                        Console.WriteLine("Socket connected to -> {0} ", sender.RemoteEndPoint.ToString());



                        await SplitLevelChecker(sender, ByteBuffer);
                    }
                    catch (ArgumentNullException ane)
                    {
                        Console.WriteLine("ArgumentNullException : {0}", ane.ToString());
                        Console.WriteLine("Press enter to exit the program: ");
                        Console.ReadLine();
                        break;
                    }
                    catch (SocketException se)
                    {
                        if(se.ErrorCode == 10053)
                        {
                            Console.WriteLine("It appears the livesplit server has stopped; please start it again.");
                            Console.WriteLine("Press enter once you have started it: ");
                            Console.ReadLine();
                            continue;
                        }
                        Console.WriteLine("SocketException : {0}", se.ToString());
                        Console.WriteLine("Press enter to exit the program:");
                        Console.ReadLine();
                        break;
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Unexpected Exception : {0}", e.ToString());
                        Console.WriteLine("Press enter to exit the program: ");
                        Console.ReadLine();
                        break;
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                    Console.WriteLine("Press Enter to exit the program: ");
                    Console.ReadLine();
                    break;
                }
            }
        }
        static string SendAndReceiveCommand(Socket s, string Command, byte[] Buffer)
        {
            byte[] spinx = Encoding.ASCII.GetBytes(Command + "\r\n");
            s.Send(spinx);
            int recv = s.Receive(Buffer);
            return Encoding.ASCII.GetString(Buffer, 0, recv);
        }


        private static async Task SplitLevelChecker(Socket sender, byte[] ByteBuffer )
        {
            Console.WriteLine("The bot will automatically start when you start your splits.");
            while (true)
            {
                string ReceivedCommand = SendAndReceiveCommand(sender, "getcurrenttimerphase", ByteBuffer);
                if (ReceivedCommand.StartsWith("Running")) {
                    Console.WriteLine("Bot is starting now!");
                    break;
                }
                await Task.Delay(1500);
            }
            IrcClient ircClient = new IrcClient("irc.chat.twitch.tv", 6667, _user, _oauth, _channel); // Sets up the connection with the twitch chat

            PingSender ping = new PingSender(ircClient); // Sends a ping every 5 minutes; otherwise twitch will kick the bot
            ping.Start();
            int StarCount = 0; //star count is created outside of the while loops so that it is an external variable

            int PreviousStarCount = 0; //used to calculate when the star count changes
            string[] index = File.ReadAllLines(_index);
            while (true)
            {


                string ReceivedCommand = SendAndReceiveCommand(sender, "getsplitindex", ByteBuffer); // Gets the current split from livesplit
                string SplitID = index[int.Parse(ReceivedCommand)];


                StarCount = int.Parse(SplitID);
                if (StarCount != PreviousStarCount)
                {
                    Console.WriteLine("Current Star Count " + StarCount + " ");
                    Console.WriteLine("Timestamp: "+ DateTime.Now.ToString("HH:mm:ss"));
                    ircClient.SendPublicChatMessage("!set " + _username + " " + (StarCount).ToString());
                    await Task.Delay(10000); //10 second buffer between chat messages
                }
                PreviousStarCount = StarCount;                

                await Task.Delay(1500); //1.5 second delay between update checks
            }
        }
        
    }
}
