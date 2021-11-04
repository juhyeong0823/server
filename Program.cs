using System;
using System.Threading;
using System.Net.Sockets;
using System.Text;
using System.Collections;
using System.Net;
using System.Timers;
using System.Numerics;
using System.Collections.Generic;

namespace ConsoleApplication1
{
    class Program
    {
        const int TIMER_INTERVAL = 1000;
        public const float SPEED_UNIT = 3.0f;
        const float ATTACK_DISTANCE = 3.5f;
        const string COMMAND_DAMAGE = "#Damage#";
        const string STRING_TERMINATOR = ";";


        public static Hashtable clientsList = new Hashtable();
        public static Dictionary<string, handleClient> movingUnits = new Dictionary<string, handleClient>();

        private static int userCount = 0;
        private static System.Timers.Timer aTimer;

        private static object lockSocket = new object();
        private static object lockMove = new object();

        static Random rand = new Random();

        private static System.Timers.Timer bTimer;
        static List<string> agarDatas = new List<string>();
        static int agarId = 0;
        static bool isSpawnerStarted = false;

        private static void SetTimer_b()
        {
            if (!isSpawnerStarted)
            {
                bTimer = new System.Timers.Timer(TIMER_INTERVAL);
                bTimer.Elapsed += OnTimedEvent_b;
                bTimer.AutoReset = true;
                bTimer.Enabled = true;
                isSpawnerStarted = true;
            }
            else
            {
                Console.WriteLine("이 타이머는 하나만 실행할거");
            }
        }

        private static object lockObj = new object();

        private static void OnTimedEvent_b(object source, ElapsedEventArgs e)
        {
            lock (lockObj)
            {
                for (int i = 0; i < 2; i++)
                {
                    MakeAgar_b();
                }
            }
        }

        private static void MakeAgar_b()
        {
            string x = (rand.Next(-7, 7) + rand.NextDouble()).ToString("0.00"); // 8,8안에 만들어져야함.
            string y = (rand.Next(-3, 3) + rand.NextDouble()).ToString("0.00"); // 4,4안에 만들어져야함.
            string dataFromClient = $"#MakeAgar#{agarId},{x},{y},0";
            agarId++;
            agarDatas.Add(dataFromClient);
            Console.WriteLine(dataFromClient);
            broadcast(dataFromClient, "", false);
        }

        public static void SetTimer()
        {
            aTimer = new System.Timers.Timer(TIMER_INTERVAL);
            aTimer.Elapsed += OnTimedEvent;
            aTimer.AutoReset = true;
            aTimer.Enabled = true;
        }

        private static void OnTimedEvent(object source, ElapsedEventArgs e)
        {
            lock (lockMove)
            {
                foreach (var client in movingUnits)
                {
                    handleClient hc = client.Value;
                    if (hc.bMoving)
                    {
                        TimeSpan elapsed = DateTime.Now - hc.startTime;
                        if (elapsed.TotalMilliseconds >= hc.timeArrive)
                        {
                            //도착완료
                            hc.bMoving = false;
                            Console.WriteLine("unit " + hc.clientID + " arrived");
                        }
                        else
                        {
                            float ratio = (float)elapsed.TotalMilliseconds / (float)hc.timeArrive;
                            hc.currentPos = Vector2.Lerp(hc.orgPos, hc.targetPos, ratio);
                        }
                    }
                }
            }
        }

        static void Main(string[] args)
        {
            try
            {
                //IPAddress ipAddress = Dns.GetHostEntry("localhost").AddressList[0];
                TcpListener serverSocket = new TcpListener(IPAddress.Any, 8888);
                //TcpListener serverSocket = new TcpListener(System.Net.IPAddress.Loopback, 8888);
                TcpClient clientSocket = default(TcpClient);
                int counter = 0;
                byte[] bytesFrom = new byte[1024];
                string dataFromClient = "";

                SetTimer();
                SetTimer_b();

                serverSocket.Start();
                Console.WriteLine("Game Server Started ....");
                counter = 0;
                while ((true))
                {
                    counter += 1;
                    clientSocket = serverSocket.AcceptTcpClient();

                    dataFromClient = "";
                    /*
                    NetworkStream networkStream = clientSocket.GetStream();
                    int numBytesRead;

                    // 접속된 클라이언트의 닉네임 가져오기
                    while (networkStream.DataAvailable)
                    {
                        numBytesRead = networkStream.Read(bytesFrom, 0, bytesFrom.Length);
                        dataFromClient += Encoding.ASCII.GetString(bytesFrom, 0, numBytesRead);
                    }
                    int idx = dataFromClient.IndexOf("$");
                    if (idx >= 0)
                    {
                        dataFromClient = dataFromClient.Substring(0, idx);
                    }
                    */

                    // dataFromClient is nick name of the user.
                    //clientsList.Add(dataFromClient, clientSocket);
                    //broadcast(dataFromClient + " Joined ", dataFromClient, false);
                    //Console.WriteLine(dataFromClient + " Joined chat room ");
                    //clientsList.Add(userCount, clientSocket);

                    counter = userCount;
                    userCount++;

                    handleClient client = new handleClient();
                    clientsList.Add(counter, client);

                    client.startClient(clientSocket, clientsList, counter);
                }

                clientSocket.Close();
                serverSocket.Stop();
                Console.WriteLine("exit");
                Console.ReadLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        public static TcpClient GetSocket(int id)
        {
            TcpClient socket = null;

            if (clientsList.ContainsKey(id))
            {
                handleClient hc = (handleClient)clientsList[id];
                socket = hc.clientSocket;
            }

            return socket;
        }

        public static void broadcast(string msg, string uName, bool flag)
        {

            Byte[] broadcastBytes = null;
            List<object> deletedClients = new List<object>();
            if (flag == true)
            {
                broadcastBytes = Encoding.ASCII.GetBytes(uName + "$" + msg);
            }
            else
            {
                broadcastBytes = Encoding.ASCII.GetBytes(msg);
            }
            lock (lockSocket)
            {
                foreach (DictionaryEntry Item in clientsList)
                {

                    TcpClient broadcastSocket;
                    handleClient hc = (handleClient)Item.Value;
                    broadcastSocket = hc.clientSocket;
                    NetworkStream broadcastStream = broadcastSocket.GetStream();
                    try
                    {
                        broadcastStream.Write(broadcastBytes, 0, broadcastBytes.Length);
                        broadcastStream.Flush();
                    }
                    catch (Exception ex)
                    {
                        deletedClients.Add(Item.Key);
                    }

                }
            }
            foreach(var item in deletedClients)
            {
                TcpClient broadcastSocket;
                handleClient hc = (handleClient)clientsList[item];
                broadcastSocket = hc.clientSocket;
                broadcastSocket.Close();

                clientsList.Remove(item);
            }
        }  //end broadcast function

        public static void CheckAndBroadcastDamage(string attackerID)
        {
            lock (lockMove)
            {
                handleClient hc = movingUnits[attackerID];
                string msg = ""; //걸린 애들 key값 받아오기
                foreach(var unit in movingUnits)
                {
                    handleClient client = unit.Value;
                    if(hc.clientID != client.clientID)
                    {
                        float distance = Vector2.Distance(hc.currentPos, client.currentPos);
                        if(distance < ATTACK_DISTANCE)
                        {
                            if(msg.Length > 0)
                            {
                                msg += ",";
                            }
                            msg += client.clientID;
                        }
                    }
                }
                if(msg.Length > 0)
                {
                    msg = "$" + COMMAND_DAMAGE + msg;
                    broadcast(msg, hc.clientID, false);
                }
            }
        }



        public static void UserAdd(string clientNo, TcpClient clientSocket)
        {
            broadcast(clientNo + " Joined ", "", false);
            Console.WriteLine(clientNo + " Joined chat room ");
        }

        public static void UserLeft(int userID, string clientID)
        {
            if (clientsList.ContainsKey(userID))
            {
                broadcast(clientID + "$#Left#", clientID, false);
                Console.WriteLine("client Left:" + clientID);

                TcpClient clientSocket = GetSocket(userID);

                clientsList.Remove(userID);
                clientSocket.Close();
            }
        }

        public static void SetUnitMove(handleClient client)
        {
            lock (lockMove)
            {
                if (!movingUnits.ContainsKey(client.clientID))
                {
                    movingUnits.Add(client.clientID, client);
                }
            }
        }

        ~Program()
        {
           // mut.Dispose();
        }
    }//end Main class


    public class handleClient
    {
        const string COMMAND_MOVE = "#Move#";
        const string COMMAND_ENTER = "#Enter#";
        const string COMMAND_HISTORY = "#History#";
        const string COMMAND_ATTACK = "#Attack#";
        const string COMMAND_MAKE_AGAR = "#MakeAgar#";

        const char CHAR_TERMINATOR = ';';

        public TcpClient clientSocket;
        public int userID;
        public string clientID;

        public float hp = 100f;

        public float posX;
        public float posY;
        public Vector2 currentPos;
        public Vector2 orgPos;
        public Vector2 targetPos;
        public float targetPosX;
        public float targetPosY;
        public DateTime startTime;
        public int timeArrive;
        public bool bMoving;

        private Hashtable clientsList;
        private bool noConnection = false;

        

        public void startClient(TcpClient inClientSocket, Hashtable cList, int userSerial)
        {
            userID = userSerial;
            this.clientSocket = inClientSocket;
            // this.clNo = clineNo;
            this.clientsList = cList;

            currentPos.X = 0;
            currentPos.Y = 0;
            bMoving = false;

            Thread ctThread = new Thread(doChat);
            ctThread.Start();
        }

        bool SocketConnected(Socket s)
        {
            bool part1 = s.Poll(1000, SelectMode.SelectRead);
            bool part2 = (s.Available == 0);
            if (part1 && part2)
                return false;
            else
                return true;
        }

        private void ProcessMove(string clientID, string remain)
        {
            var strs = remain.Split();
            try
            {
                orgPos = currentPos;
                targetPosX = float.Parse(strs[0]);
                targetPosY = float.Parse(strs[1]);

                targetPos.X = targetPosX;
                targetPos.Y = targetPosY;
                startTime = DateTime.Now;
                timeArrive = (int)(Vector2.Distance(orgPos, targetPos) * 1000.0f / Program.SPEED_UNIT);
                bMoving = true;
            }
            catch(Exception e)
            {

            }
        }

        private void ProcessAttack(string clientID)
        {
            Console.WriteLine("Attack - " + clientID);
            Program.CheckAndBroadcastDamage(clientID);
        }

        private void ProcessCommand(string clientID, string dataFromClient)
        {
            if(dataFromClient[0] == '#') // 이제 커맨드면 #으로 시작할거임
            {
                string command;
                string remain;
                int idx = dataFromClient.IndexOf('#', 1); // #을 건너뛴, 1번부터 #을 찾으면 다음 #의 위치가 나오고?
                if(idx > 1) // #이 없으면 -1임, 즉 #이 있으면
                {
                    command = dataFromClient.Substring(0, idx + 1); // #부터 다음 #까지 가져오는거
                    switch (command)
                    {
                        case COMMAND_MOVE:
                            remain = DeleteTerminator(dataFromClient.Substring(idx + 1));
                            ProcessMove(clientID, remain);
                            Program.broadcast(dataFromClient, clientID, true);
                            break;
                        case COMMAND_ATTACK:
                            ProcessAttack(clientID);
                            Program.broadcast(dataFromClient, clientID, true);
                            break;
                    }
                }
            }
        }

        private string DeleteTerminator(string remain)
        {
            int idx = remain.IndexOf(CHAR_TERMINATOR);
            if (idx >= 0)
            {
                remain = remain.Substring(0, idx);
            }
            return remain;
        }

        // 기존 사용자 정보의 전달
        private void SendHistory(NetworkStream dataStream)
        {
            string history = "$" + COMMAND_HISTORY;
            bool first = true;
            foreach (DictionaryEntry Item in clientsList)
            {
                handleClient hc = (handleClient)Item.Value;
                if (!first)
                {
                    history += ",";
                }
                else
                {
                    first = false;
                }

                history += hc.clientID + "," + hc.targetPosX.ToString() + "," + hc.targetPosY.ToString();
            }



            history += CHAR_TERMINATOR.ToString();

            Console.WriteLine("final history = " + history);

            Byte[] dataBytes = null;
            dataBytes = Encoding.ASCII.GetBytes(history);

            dataStream.Write(dataBytes, 0, dataBytes.Length);
            dataStream.Flush();
        }
       
        private void doChat()
        {
            int requestCount = 0;
            byte[] bytesFrom = new byte[1024];
            string dataFromClient = "";
            string rCount = null;
            requestCount = 0;
            NetworkStream networkStream = clientSocket.GetStream();

            while (!noConnection)
            {
                try
                {
                    requestCount = requestCount + 1;

                    int numBytesRead;
                    if (!SocketConnected(clientSocket.Client))
                    {
                        // socket closed
                        noConnection = true;
                    }
                    else
                    {
                        if (networkStream.DataAvailable)
                        {
                            dataFromClient = "";
                            while (networkStream.DataAvailable)
                            {
                                numBytesRead = networkStream.Read(bytesFrom, 0, bytesFrom.Length);
                                dataFromClient += Encoding.ASCII.GetString(bytesFrom, 0, numBytesRead);
                            }

                            int idx = dataFromClient.IndexOf("$");

                            // check if ID has been sent
                            if (clientID == null && idx > 0) // 아이디를 포함해서 보내지 않아도 된다는거죠 ㅇㅇ
                            {
                                // get the ID part only
                                clientID = dataFromClient.Substring(0, idx); // 이름 가져오고
                                Program.SetUnitMove(this); // 뭔지 몰라도 될듯?
                                Console.WriteLine(clientID + " enters chat room.");
                                Program.broadcast(clientID + "$" + COMMAND_ENTER + CHAR_TERMINATOR.ToString(), clientID, false); // 애들한테 애가 들어온걸 알려주고
                                SendHistory(networkStream); // 애들 데이터를 새로 들어온 애한테 보내서 초기화
                            }

                            // get the message part only
                            int pos = idx + 1; //아이디 없이 보내면 0일듯?
                            if (pos < dataFromClient.Length)
                            {
                                dataFromClient = dataFromClient.Substring(pos, dataFromClient.Length - pos); // datafromclient가 커맨드 종류나 그런걸로만 남은거
                                Console.WriteLine("From client - " + clientID + " : " + dataFromClient);

                                // 비지니스 로직의 처리
                                ProcessCommand(clientID,dataFromClient);
                                Console.WriteLine("dataFromClient : " + dataFromClient);
                                
                            }
                            else
                            {
                                dataFromClient = "";
                            }
                            rCount = Convert.ToString(requestCount);
                        }
                    }
                }
                catch (Exception ex)
                {
                    // connection broken - delete current user.
                    noConnection = true;
                    Console.WriteLine("Error:" + ex.ToString());
                }
            }//end while

            Program.UserLeft(userID, clientID);
            Program.broadcast("User left:" + clientID, "", false);

        }//end doChat

    } //end class handleClinet
}//end namespace