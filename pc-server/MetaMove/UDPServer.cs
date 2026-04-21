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

// for socket
using System.Net.Sockets;
using System.Net;

// for protobuf
using Google.Protobuf;
using Abb.Egm;

// for MessageBox
using System.Windows;

// for Threading
using System.Threading;
using System.IO;

namespace MetaMove
{
    class UDPserver
    {
        readonly private RobotFeedback controller = new RobotFeedback();
        readonly CommandClass command = new CommandClass();
        public UDPserver(ref RobotFeedback ctrl, CommandClass cmd)
        {
            controller = ctrl;
            command = cmd;
        }

        readonly Tools tools = new Tools();

        private string IP;
        private int PORT;
       
        private bool started = false;
        private bool ERROR = new bool(); 

        UdpClient serv;
        IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);

        private Thread UDPTHREAD;
        public void StartThread() {

            UDPTHREAD = new Thread(new ThreadStart(UDPThread));
            UDPTHREAD.Start();

        }

        private byte[] data = new byte[1024];

        ////////////////////////////////////////                ////////////////////////////////////////
        ////////////////////////////////////////   THREAD UDP   ////////////////////////////////////////
        ////////////////////////////////////////                ////////////////////////////////////////
        private void UDPThread()
        {

            int udpState = 0;
            // timeout 
            DateTime startTime = new DateTime();

            while (true)
            {
                if (ERROR)
                {
                    ERROR = false;
                    udpState = 1;
                }

                if (controller.UDP.Stop)
                {
                    if (controller.UDP.Enable)
                        End();

                    controller.UDP.Online = false;
                    udpState = 0;

                    controller.MsgJoint = String.Empty;

                    controller.UDP.Stop = false;
                    ERROR = false;
                }

                switch (udpState)
                {
                    case 0:
                        if (controller.UDP.Start && controller.UDP.Enable)
                        {
                            udpState++;
                            controller.UDP.Start = false;
                        }
                        break;

                    case 1:
                        if (controller.UDP.Enable)
                            Start();
                        udpState++;
                        break;

                    case 2:
                        //Console.WriteLine("waiting for " + controller.type);

                        if (Receive())
                        {
                            // EGM timeout
                            controller.UDP.Online = true;
                            startTime = DateTime.Now;

                            //Console.WriteLine("received");

                            EgmRobot VC_robData = Tools.DeSerialize(data);
                            if (VC_robData != null)
                            {
                                controller.MsgJoint = tools.JointsFromFeedback(VC_robData);
                                for (int i = 0; i < 6; i++)
                                    controller.positionEGM[i] = (Math.Round(VC_robData.FeedBack.Joints.Joints[i] * 100) / 100);

                                controller.cartesianPosition[0] = (Math.Round(VC_robData.FeedBack.Cartesian.Pos.X * 100) / 100);
                                controller.cartesianPosition[1] = (Math.Round(VC_robData.FeedBack.Cartesian.Pos.Y * 100) / 100);
                                controller.cartesianPosition[2] = (Math.Round(VC_robData.FeedBack.Cartesian.Pos.Z * 100) / 100);
                                controller.eulerOrientation[0] = (Math.Round(VC_robData.FeedBack.Cartesian.Euler.X * 100) / 100);
                                controller.eulerOrientation[1] = (Math.Round(VC_robData.FeedBack.Cartesian.Euler.Y * 100) / 100);
                                controller.eulerOrientation[2] = (Math.Round(VC_robData.FeedBack.Cartesian.Euler.Z * 100) / 100);

                                controller.MsgPose = tools.PoseFromFeedback(VC_robData);

                                //Console.WriteLine(VC_robData);

                                if (command.MODE == command.CMD_EGMPOSE)
                                {
                                    EgmSensor sensor = tools.CreateSensorMessageCartesian(controller.CTRL_EGMTargetPos, controller.CTRL_EGMTargetOri);
                                    //EgmSensor sensor = tools.CreateSensorMessageJoint(controller.CTRL_jointTarget);
                                    //Console.WriteLine(sensor);
                                    using (MemoryStream msg = new MemoryStream())
                                    {
                                        sensor.WriteTo(msg);
                                        Send(msg);
                                    }
                                }
                            }
                        }

                        // timeout
                        TimeSpan duration = DateTime.Now - startTime;
                        if (duration.TotalMilliseconds > 500)
                        {
                            startTime = DateTime.Now;
                            controller.UDP.Online = false;
                        }

                        break;
                }

                Thread.Sleep(2);
            }

        }



        ////////////////////////////////////////      start     ////////////////////////////////////////
        private void Start()
        {
            IP = controller.UDP.IP;
            PORT = controller.UDP.PORT;

            try { 
                IPEndPoint ep = new IPEndPoint(IPAddress.Parse(IP), PORT);
                Console.WriteLine("UDP\tstarting server:\t" + IP + ":" + PORT);

                serv = new UdpClient(ep);
                started = true; 
            }
            catch(Exception e)
            {
                Console.WriteLine("UDP\tERROR: " +e);
                MessageBox.Show("Invalid IP-addresse: " + IP);
            }

        }

        ////////////////////////////////////////      END       ////////////////////////////////////////
        private void End()
        {
            if (started)
            {
                Console.WriteLine("UDP\tclosing server:\t\t" + IP + ":" + PORT);

                serv.Close();
                serv.Dispose();
                started = false; 
            }

        }

        ////////////////////////////////////////     receive    ////////////////////////////////////////
        private bool Receive() {
            try
            {
                if (started)
                {
                    if (serv.Available > 0)
                    {
                        data = serv.Receive(ref sender);
                        //Console.WriteLine(sender);
                        return true;
                    }
                }
            }
            catch (Exception)
            {
                //Console.WriteLine("TCP\treceiving failed");
                ERROR = true; 
                End();
            }
            return false;

        }

        ////////////////////////////////////////      send      ////////////////////////////////////////
        public void Send(MemoryStream sendData)
        {
            try
            {
                   
                int i = serv.Send(sendData.ToArray(), (int)sendData.Length, sender);

            }
            catch (Exception)
            {
                Console.WriteLine("TCP\tsending failed");
                End();
                ERROR = true; 
            }
        }

    }

}
