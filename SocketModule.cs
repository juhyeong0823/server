using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class SocketModule : MonoBehaviour
{
    static SocketModule instance = null;

    private TcpClient clientSocket;
    private GameManager gm;

    private NetworkStream serverStream = default(NetworkStream);

    private Queue<string> msgQueue;
    private string nickname;

    bool bRunning = false;

    public static SocketModule GetInstance()
    {
        return instance;
    }

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(this);
        }
    }

    private void Start()
    {
        msgQueue = new Queue<string>();
        gm = GetComponent<GameManager>();
    }

    public void Login(string id)
    {
        if (!bRunning)
        {
            clientSocket = new TcpClient();
            clientSocket.Connect("localhost", 8888);
            serverStream = clientSocket.GetStream();

            byte[] outStream = Encoding.ASCII.GetBytes(id + "$");
            serverStream.Write(outStream, 0, outStream.Length);
            serverStream.Flush();

            Thread ctThread = new Thread(getMessage);
            ctThread.Start();

            bRunning = true;
            nickname = id;
        }
    }

    public void SendData(string str)
    {
        if (bRunning && serverStream != null)
        {
            byte[] outSteam = Encoding.ASCII.GetBytes("$" + str);
            serverStream.Write(outSteam, 0, outSteam.Length);
            serverStream.Flush();
        }
    }

    private void StopThread()
    {
        bRunning = false;
    }

    public void Logout()
    {
        if (bRunning)
        {
            StopThread();
            msgQueue.Clear();
            nickname = "";
        }

        if (serverStream != null)
        {
            serverStream.Close();
            serverStream = null;
        }

        clientSocket.Close();
    }

    public bool IsOnline()
    {
        return bRunning;
    }

    private void getMessage()
    {
        byte[] inStream = new byte[1024];
        string returndata = "";

        try
        {
            while (bRunning)
            {
                serverStream = clientSocket.GetStream();
                int buffSize = 0;
                buffSize = clientSocket.ReceiveBufferSize;
                int numBytesRead;

                if (serverStream.DataAvailable)
                {
                    returndata = "";
                    while (serverStream.DataAvailable)
                    {
                        numBytesRead = serverStream.Read(inStream, 0, inStream.Length);
                        returndata += Encoding.ASCII.GetString(inStream, 0, numBytesRead);
                    }

                    gm.QueueCommand(returndata);
                    Debug.Log(returndata);
                }
            }
        }
        catch (Exception ex)
        {
            StopThread();
        }
    }
}