/*
 *      ---------------------------------   
 *                   GoHolo    
 *      ---------------------------------
 *      
 *      Copyright (C)   2021 Jakob Hörbst
 *      author:         Jakob Hörbst
 *      email:          jakob@hoerbst.net
 *      year:           2021
 * 
*/

using System;
using System.Collections.Generic;
using System.Text;

// for socket
using System.Net.Sockets;
using System.Net;
using System.Windows;
// for Threading
using System.Threading;
// for protobuf
using Abb.Egm;

namespace MetaMove
{
    class TCPserver
    {
        readonly HoloLens2 holo = new HoloLens2();
        readonly RobotFeedback VC = new RobotFeedback();
        readonly RobotFeedback RC = new RobotFeedback();
        readonly CommandClass command = new CommandClass();
        public MainWindow Guid { get; set; }
        public TCPserver(ref HoloLens2 hl, RobotFeedback vc, RobotFeedback rc, CommandClass cmd, MainWindow guid)
        {
            holo = hl;
            VC = vc;
            RC = rc;
            command = cmd;
            Guid = guid;
        }

        // TCPIP
        public string IP;
        public int PORT;

        //  public bool connection = false;

        Socket server;

        private Tools tools = new Tools();

        DateTime LastSended;
        readonly int TCPsendInterval = 50; // [ms] //jakobnode was 20

        readonly int connectionTimeout = 1000; // [ms]

        private Thread TCPTHREAD;
        public void startThread()
        {

            TCPTHREAD = new Thread(new ThreadStart(TCPThread));
            TCPTHREAD.Start();

        }

        private void TCPThread()
        {
            int status = 0;
            String receiveCombined = String.Empty;

            bool[] holoPathReceivePose = new bool[10];
            bool holoPathReceiveTimeoutActive = new bool();
            DateTime holoPathReceiveTimeout = DateTime.Now;




            while (true)
            {

                if (holo.TCP.Stop)
                {
                    End();
                    holo.connected = false;
                    status = 0;
                    Console.WriteLine("TCP\tstopping");
                    holo.TCP.Stop = false;
                    holo.TCP.Start = false;
                }

                switch (status)
                {
                    // waiting for start button
                    case 0:
                        if (holo.TCP.Start)
                        {
                            status++;
                            holo.TCP.Start = false;
                        }
                        break;
                    // setting IP-addresse from GUI and starting the TCP server
                    case 1:
                        //Console.WriteLine("TCP\tstarting");
                        IP = holo.TCP.IP;
                        PORT = holo.TCP.PORT;

                        Start();
                        status = 10;
                        break;

                    // "main" (if connection is established)
                    case 10:

                        Send("ping");
                        if (holo.connected)
                        {
                            ////////////////////////    send    //////////////////////// 
                            TimeSpan duration = DateTime.Now - LastSended;
                            if (duration.TotalMilliseconds > TCPsendInterval)
                            {
                                LastSended = DateTime.Now;

                                if (command.HoloDataSourceSimulation)
                                {

                                    if (VC.MsgTorque != String.Empty || VC.MsgJoint != String.Empty)
                                    {
                                        string msg = "{\"VC\":";
                                        msg += VC.MsgJoint;
                                       // if (VC.MsgTorque != String.Empty)
                                       // {
                                            if (VC.MsgPose != String.Empty)
                                                msg += ("," + VC.MsgPose);

                                            if (VC.MsgTorque != String.Empty)
                                                msg += ("," + VC.MsgTorque);

                                            if (VC.MsgValidation != String.Empty)
                                                msg += ("," + VC.MsgValidation);
                                        //}
                                        msg += "}";
                                        Send(msg);
                                        //Console.WriteLine("TCP\tVC sending: " + msg);
                                    }

                                }
                                else
                                {
                                    if (RC.MsgTorque != String.Empty || RC.MsgJoint != String.Empty)
                                    {
                                        string msg = "{\"VC\":";
                                        msg += RC.MsgJoint;
                                       // if (RC.MsgTorque != String.Empty)
                                       // {
                                            if (RC.MsgPose != String.Empty)
                                                msg += ("," + RC.MsgPose);

                                            if (RC.MsgTorque != String.Empty)
                                                msg += ("," + RC.MsgTorque);

                                            if (RC.MsgValidation != String.Empty)
                                                msg += ("," + RC.MsgValidation);
                                       // }
                                        msg += "}";
                                        Send(msg);
                                        //Console.WriteLine("TCP\tRC sending: " + msg);

                                    }
                                }
                            }

                            // MESSAGES (HoloLens2 -> GoHolo)
                            // {"holo":{"EGM":{"pos":[-1.9263,0,0.1899],"orient":[-0.7071,0,-0.7071,0]}"e"}}
                            // {"holo":{"joint":[0,0,0,0,0,0]"j"}}
                            // {"holo":{"mode":[0]"m"}}
                            ////////////////////////  receive   //////////////////////// 
                            string received = Receive();
                            if (received != null)
                            {
                                //jakobnode validate message for all types of data
                                //Console.WriteLine("TCP\tmsg from holo: " + received);
                                // VALIDATE MESSAGE
                                if (tools.ValidateTCPMessage("holo", ref received))
                                {
                                    // GET DATA FOR JOINT CONTROL
                                    float[] jointControl = new float[6];
                                    if (tools.GetData("holo", received, "joint", ref jointControl))
                                    {
                                        VC.CTRL_jointTarget = jointControl;
                                        RC.CTRL_jointTarget = jointControl;

                                        VC.CTRL_jointTargetReceived = true;
                                        RC.CTRL_jointTargetReceived = true;
                                    }

                                    // GET DATA FOR EGM CONTROL
                                    {
                                        float[] pos = new float[3];
                                        float[] ori = new float[4];
                                        if (tools.GetDataEGM(received, ref pos, ref ori))
                                        {
                                            VC.CTRL_EGMTargetOri = ori;
                                            RC.CTRL_EGMTargetOri = ori;

                                            VC.CTRL_EGMTargetPos = pos;
                                            RC.CTRL_EGMTargetPos = pos;
                                            command.MODE = command.CMD_EGMPOSE;
                                        }
                                    }

                                    // GET MODE FROM HOLO
                                    {
                                        byte mod = new byte();
                                        if (tools.GetMode(received, "mode", ref mod))
                                        {
                                            command.MODE = mod;
                                            VC.CTRL_sendMode = true;
                                            RC.CTRL_sendMode = true; 
                                            // jakobnode send handshake
                                        }
                                    }

                                    // GET HOLOPATH
                                    {
                                        float[] pos = new float[3];
                                        float[] ori = new float[4];
                                        int id = new int();
                                        int num = new int();
                                        bool move = new bool();
                                        bool simulate = new bool();
                                        if (tools.GetHoloPath(received, ref pos, ref ori, ref id, ref num, ref simulate, ref move))
                                        {
                                            holoPathReceiveTimeout = DateTime.Now;
                                            holoPathReceivePose[id - 1] = true;
                                            holoPathReceiveTimeoutActive = true;


                                            VC.CTRL_holoPathNumber = num;
                                            RC.CTRL_holoPathNumber = num;

                                            VC.CTRL_holoPathPose[id - 1].ori = ori;
                                            RC.CTRL_holoPathPose[id - 1].ori = ori;

                                            VC.CTRL_holoPathPose[id - 1].pos = pos;
                                            RC.CTRL_holoPathPose[id - 1].pos = pos;

                                            Console.Write("TCP\tholoPath pose" + id + ": [[");
                                            Console.Write(VC.CTRL_holoPathPose[id - 1].pos[0].ToString().Replace(',', '.') + ", ");
                                            Console.Write(VC.CTRL_holoPathPose[id - 1].pos[1].ToString().Replace(',', '.') + ", ");
                                            Console.Write(VC.CTRL_holoPathPose[id - 1].pos[2].ToString().Replace(',', '.') + "],[");
                                            Console.Write(VC.CTRL_holoPathPose[id - 1].ori[0].ToString().Replace(',', '.') + ", ");
                                            Console.Write(VC.CTRL_holoPathPose[id - 1].ori[1].ToString().Replace(',', '.') + ", ");
                                            Console.Write(VC.CTRL_holoPathPose[id - 1].ori[2].ToString().Replace(',', '.') + ", ");
                                            Console.Write(VC.CTRL_holoPathPose[id - 1].ori[3].ToString().Replace(',', '.') + "];");
                                            if (move)
                                                Console.WriteLine(" move");
                                            if (simulate)
                                                Console.WriteLine(" simulate");


                                            // check if all poses are transmittet propperly
                                            bool messagesComplete = true;
                                            for (int i = 0; i < num; i++)
                                            {
                                                messagesComplete = messagesComplete & holoPathReceivePose[i];
                                            }
                                            if (messagesComplete) {
                                                Console.WriteLine("TCP\tpose transmission complete: " + messagesComplete);
                                                holoPathReceiveTimeoutActive = false;

                                                for (int i = 0; i < 10; i++)
                                                    holoPathReceivePose[i] = false;

                                                command.HOLOPATH.Move = move;
                                                command.HOLOPATH.Simulate = simulate;
                                            }

                                        }

                                    }
                                    /*
                                    {
                                        if (tools.GetHoloPathCommand(received, "simulate"))
                                            Console.WriteLine("TCP\tsimulate received");

                                    }
                                    */
                                }

                            }


                        }
                        else
                        {
                            Console.WriteLine("holo\tconnected: " + holo.connected);
                            MessageBox.Show("HoloLens2 connection lost");
                            status = 0;
                        }

                        // timeout for holo Path Pose transmission
                        if (holoPathReceiveTimeoutActive)
                        {
                            if ((DateTime.Now - holoPathReceiveTimeout).TotalMilliseconds > 500)
                            {
                                MessageBox.Show("TCP\tholopath message incomplete!");
                                holoPathReceiveTimeoutActive = false;
                                for (int i = 0; i < 10; i++)
                                    holoPathReceivePose[i] = false;
                            }

                        }
                        break;
                }

                Thread.Sleep(5);
            }

        }

        private void Recover()
        {
            Console.WriteLine("TCP\trecover");
            End();
            Start(); 
        }

        public void Start() 
        {
           // Console.WriteLine("TCP\tstart");
            IPAddress serverIP = IPAddress.Parse(IP);
            IPEndPoint localEndPoint = new IPEndPoint(serverIP, PORT);

            server = new Socket(serverIP.AddressFamily,
                                    SocketType.Stream,
                                    ProtocolType.Tcp);

            if (server.Connected) {
                server.Shutdown(SocketShutdown.Both);
                server.Disconnect(true);
                Console.WriteLine("TCP\tserver: " + server.Connected);
            }
            Console.WriteLine("TCP\tstarting server: " + localEndPoint);
            Console.WriteLine("TCP\twaiting for connection");

           // string localIP = tools.GetLocalIPAddress(IP);
           // Console.WriteLine("TCP\tIP: " + localIP);

            if ( !(tools.GetLocalIPAddress(IP) ||  IP == "127.0.0.1") )
            {
                MessageBox.Show("ERROR IP-addresse not matching\ntrying:\t" + IP);
                holo.connected = false;
            }
           // }
           // else { 
            // while (!connection)
            //  {
                try
                {
                    
                using (Socket s = new Socket(serverIP.AddressFamily,
                    SocketType.Stream,
                    ProtocolType.Tcp)) 
                    {
 
 
                        s.Bind(localEndPoint);
                        s.Listen(10);

                        DateTime timeoutStart = DateTime.Now;
                        while (true) {
                            /*
                            s.Listen(backlog); // max 10 connections
                            Console.WriteLine(backlog);
                            if (backlog > 0)
                            { 
                                server = s.Accept();
                                if (server.Connected)
                                {
                                    connection = true;
                                    
                                    MessageBox.Show("connected");
                                    break;
                                }
                            }
                            */
                            IAsyncResult result;
                            Action action = () =>
                            {
                                try
                                {
                                    server = s.Accept();
                                    if (server.Connected)
                                    {
                                        holo.connected = true;
                                    }
                                }
                                catch (Exception)
                                { 
                                }
                            };

                            result = action.BeginInvoke(null, null);

                            if (result.AsyncWaitHandle.WaitOne(connectionTimeout))
                            {
                                //action.EndInvoke(result);
                                break;
                            }
                            else
                            {
                                holo.connected = false;
                                MessageBox.Show("TCP\tTimeout connecting to HoloLens2");
                                holo.TCP.Stop = true;
                               // action.EndInvoke(result);
                                break;
                            }                 

                        }
                    
                    }
                   
                }
                catch (Exception e )
                {
                    if (server.Connected)
                        Console.WriteLine("still connected");
                    Console.WriteLine("TCP\tconnection error: " + e);
                    End();
                }
            
            //}
            Console.WriteLine("TCP\tconnection to client established: " + holo.connected);
        }

        public void End()
        {
            Console.WriteLine("TCP\tclosing connection");
            //if (server.Connected)
            //{
            try
            {
                Console.WriteLine("TCP\tShutdown");
                server.Shutdown(SocketShutdown.Both);
                //Console.WriteLine("TCP\tDisconnect start");
                //server.Disconnect(true);
                //Console.WriteLine("TCP\tDisconnect end");
                //s.Shutdown(SocketShutdown.Both);
                //s.Disconnect(true);
            }
            catch (Exception e)
            {
                Console.WriteLine("TCP\tERROR while terminating connection: " + e);
            }
            finally
            {
                server.Close();
                
                //    }
            }
            server.Dispose();
            Console.WriteLine("TCP\tconnection closed and disposed");
        }

        public void Send(string msg)
        {
            //Console.WriteLine("TCP\tstart sending");
            if (holo.connected)
            {
                try
                {
                    if (server.Connected)
                    {
                        byte[] msgByte = Encoding.UTF8.GetBytes(msg);
                        int i = server.Send(msgByte);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("TCP\tsending failed: \n" + e);
                    holo.connected = false;
                    //End();
                    Recover();
                }
            }
            //Console.WriteLine("TCP\tdone sending");

        }

        public string Receive()
        {

            try
            {
                if (server.Connected && server.Available > 0)
                {
                    byte[] buffReceived = new byte[256];
                    int nRecv = server.Receive(buffReceived);

                    string ReceivedAngle = Encoding.ASCII.GetString(buffReceived, 0, nRecv);
                    //Console.WriteLine("TCP\treceived: " + ReceivedAngle);

                    //startTime = DateTime.Now;
                    return ReceivedAngle;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("TCP\treceive failed: " + e);
                //holo.connected = false;
                //End();
                Recover();
            }
            return null;
        }

    }
 
}
