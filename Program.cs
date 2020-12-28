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
        private static IPAddress _ip;

        
        private static void assignStrings()
        {
            LoginCredentials loginCredentials = new LoginCredentials();

            try
            {
                loginCredentials = LoginCredentials.GetCredentials(@"C:\602counting\credentials.json");
            } 
            catch
            {
                Console.Write("Insert bot username ");
                loginCredentials.user = Console.ReadLine();

                Console.Write("Insert bot oauth ID ");
                loginCredentials.oauth = Console.ReadLine();

                Console.Write("Insert channel to autocount in ");
                loginCredentials.channel = Console.ReadLine();

                Console.Write("Insert your twitch username ");
                loginCredentials.username = Console.ReadLine();

                Console.Write("Insert IP from LiveSplit Server ");
                loginCredentials.ip = Console.ReadLine();

                LoginCredentials.SaveCredentials(loginCredentials, @"C:\602counting\credentials.json");
            }

            


            _ip = IPAddress.Parse(loginCredentials.ip);
            _user = loginCredentials.user;
            _oauth = loginCredentials.oauth;
            _channel = loginCredentials.channel;
            _username = loginCredentials.username;
        }

        static async Task Main(string[] args)
        {
            assignStrings();
            await ExecuteClient();
        }
        static async Task ExecuteClient()
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
                    //sender.Shutdown(SocketShutdown.Both);
                    //sender.Close();
                }
                catch (ArgumentNullException ane)
                {
                    Console.WriteLine("ArgumentNullException : {0}", ane.ToString());
                }
                catch (SocketException se)
                {
                    Console.WriteLine("SocketException : {0}", se.ToString());
                }
                catch (Exception e)
                {
                    Console.WriteLine("Unexpected Exception : {0}", e.ToString());
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
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
            Console.Write("Press enter when the 602 race starts (press enter in opening of sm64)"); //Livesplit server acts weird when started and livesplit is not already running
            Console.ReadLine();
            //string CurrentProgress = "";
            IrcClient ircClient = new IrcClient("irc.chat.twitch.tv", 6667, _user, _oauth, _channel); // Sets up the connection with the twitch chat

            PingSender ping = new PingSender(ircClient); // Sends a ping every 5 minutes; otherwise twitch will kick the bot
            ping.Start();
            int StarCount = 0; //star count is created outside of the while loops so that it is an external variable

            int PreviousStarCount = 0; //used to calculate when the star count changes
            string[] index = File.ReadAllLines(@"C:\602counting\index.txt");
            while (true)
            {


                string ReceivedCommand = SendAndReceiveCommand(sender, "getsplitindex", ByteBuffer); // Gets the current split from livesplit
                string SplitID = index[int.Parse(ReceivedCommand)]; 
                //Console.WriteLine(int.Parse(SplitID)); debug statements to find out what went wrong
                //Console.WriteLine(int.Parse(ReceivedCommand));
                //Console.WriteLine(SplitID);


                StarCount = int.Parse(SplitID);
                Console.WriteLine("Current Star Count " + StarCount);
                if (StarCount != PreviousStarCount)
                {
                    ircClient.SendPublicChatMessage("!set " + _username + " " + (StarCount).ToString());
                }
                PreviousStarCount = StarCount;

                

                //Console.WriteLine("Current Progress" + CurrentProgress);
                //ircClient.SendPublicChatMessage(CurrentProgress);

                await Task.Delay(10000); //10 second break between messages to not ban the global bot
            }
        }
        
    }
}
