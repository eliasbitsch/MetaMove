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
using Abb.Egm;
using System.Net.Sockets;

namespace MetaMove
{
    public class Tools
    {

        private static uint _seqNumber = 0;

        public string JointsFromFeedback(EgmRobot feedbackRob)
        {
            try
            {

                if (feedbackRob != null)
                {
                    string jointMsg = "{\"joint\":[";
                    double value;

                    value = (Math.Round(feedbackRob.FeedBack.Joints.Joints[0] * 100) / 100);
                    jointMsg += value.ToString().Replace(',', '.');
                    jointMsg += ",";
                    value = (Math.Round(feedbackRob.FeedBack.Joints.Joints[1] * 100) / 100);
                    jointMsg += value.ToString().Replace(',', '.');
                    jointMsg += ",";
                    value = (Math.Round(feedbackRob.FeedBack.Joints.Joints[2] * 100) / 100);
                    jointMsg += value.ToString().Replace(',', '.');
                    jointMsg += ",";
                    value = (Math.Round(feedbackRob.FeedBack.Joints.Joints[3] * 100) / 100);
                    jointMsg += value.ToString().Replace(',', '.');
                    jointMsg += ",";
                    value = (Math.Round(feedbackRob.FeedBack.Joints.Joints[4] * 100) / 100);
                    jointMsg += value.ToString().Replace(',', '.');
                    jointMsg += ",";
                    value = (Math.Round(feedbackRob.FeedBack.Joints.Joints[5] * 100) / 100);
                    jointMsg += value.ToString().Replace(',', '.');
                    jointMsg += "]\"j\"}";

                    return jointMsg;
                }
                else
                    return null;
            }
            catch(Exception)
            { 
                return null;
            }

        }

        public string PoseFromFeedback(EgmRobot feedbackRob)
        {
            try
            {

                if (feedbackRob != null)
                {
                    string jointMsg = "{\"pose\":[";
                    double value;

                    value = (Math.Round(feedbackRob.FeedBack.Cartesian.Pos.X * 100) / 100);
                    jointMsg += value.ToString().Replace(',', '.');
                    jointMsg += ",";
                    value = (Math.Round(feedbackRob.FeedBack.Cartesian.Pos.Y * 100) / 100);
                    jointMsg += value.ToString().Replace(',', '.');
                    jointMsg += ",";
                    value = (Math.Round(feedbackRob.FeedBack.Cartesian.Pos.Z * 100) / 100);
                    jointMsg += value.ToString().Replace(',', '.');
                    jointMsg += ",";
                    value = (Math.Round(feedbackRob.FeedBack.Cartesian.Euler.X * 100) / 100);
                    jointMsg += value.ToString().Replace(',', '.');
                    jointMsg += ",";
                    value = (Math.Round(feedbackRob.FeedBack.Cartesian.Euler.Y * 100) / 100);
                    jointMsg += value.ToString().Replace(',', '.');
                    jointMsg += ",";
                    value = (Math.Round(feedbackRob.FeedBack.Cartesian.Euler.Z * 100) / 100);
                    jointMsg += value.ToString().Replace(',', '.');
                    jointMsg += "]\"p\"}";

                    return jointMsg;
                }
                else
                    return null;
            }
            catch (Exception)
            {
                return null;
            }

        }


        public static void Delay(long millisec)
        {
            DateTime startTime = DateTime.Now;
            while (true)
            {
                DateTime endTime = DateTime.Now;
                TimeSpan duration = endTime - startTime;
                if (duration.TotalMilliseconds > millisec)
                    break;
            }
        }

        public string TorqueFromFeedback(double[] tor)
        {
            if (tor != null)
            {
                string torMsg = "{\"torque\":[";
                double value;

                value = (Math.Round(tor[0] * 100) / 100);
                torMsg += value.ToString().Replace(',', '.');
                torMsg += ",";
                value = (Math.Round(tor[1] * 100) / 100);
                torMsg += value.ToString().Replace(',', '.');
                torMsg += ",";
                value = (Math.Round(tor[2] * 100) / 100);
                torMsg += value.ToString().Replace(',', '.');
                torMsg += ",";
                value = (Math.Round(tor[3] * 100) / 100);
                torMsg += value.ToString().Replace(',', '.');
                torMsg += ",";
                value = (Math.Round(tor[4] * 100) / 100);
                torMsg += value.ToString().Replace(',', '.');
                torMsg += ",";
                value = (Math.Round(tor[5] * 100) / 100);
                torMsg += value.ToString().Replace(',', '.');
                torMsg += "]\"t\"}";

                return torMsg;
            }
            else
                return null;

        }



        public bool ValidateTCPMessage(string device, ref string msg)
        {
            // MESSAGES (HoloLens2 -> GoHolo)
            // {"holo":{"EGM":{"pos":[-1.9263,0,0.1899],"ori":[-0.7071,0,-0.7071,0]}"e"}}
            // {"holo":{"joint":[0,0,0,0,0,0]"j"}}
            // {"holo":{"mode":0,"m"}}
            // {"holo":{"path":{ "id":1,"of":3,"simulate":False,"move":False,"pos":[0.5736,0,0.6899],"ori":[-0.7071,0,-0.7071,0]}"p"}}
            //
            string reqData = "{\"" + device + "\":{";
            string reqDataEnd = "}}";

            //check if message is no empty
            if (msg != null)
            {

                //check if message is complete
                if (CheckFor(msg, reqData) && CheckFor(msg, reqDataEnd))
                {
                    // check if message starts correctly
                    if (msg.IndexOf(reqDataEnd) < msg.IndexOf(reqData))
                    {
                        msg = msg.Substring((msg.IndexOf(reqDataEnd) + reqDataEnd.Length));
                        //Console.WriteLine("start check: " + msg);
                    }
                    // pick the required information from incomming string
                    if (CheckFor(msg, reqData) && CheckFor(msg, reqDataEnd)) { 
                        msg = msg.Substring(msg.IndexOf(reqData) + reqData.Length - 1, msg.IndexOf(reqDataEnd) - (msg.IndexOf(reqData) + reqData.Length - 2));
                        //Console.WriteLine("complete check: " + msg);
                        return true; 
                    }

                    // shorten message if still too long
                    /*
                    if (msg.Length > (msg.IndexOf(reqDataEnd) + reqDataEnd.Length))
                    {
                        msg = msg.Remove(msg.IndexOf(reqDataEnd) + reqDataEnd.Length);
                        Console.WriteLine("shorten: " + msg);
                    }
                    */

                }

            }
            return false;
        }


        public bool GetData(string device, string msg, string data, ref float[] val)
        {
            //Console.WriteLine(msg);

            string reqData = "{\"" + data + "\":[";
            string reqDataEnd = "]\"" + data.Substring(0, 1) + "\"}";

            //Console.WriteLine("reqData: " + reqData);
            //Console.WriteLine("regDataEnd: " + reqDataEnd);

            //check if message is complete
            if (CheckFor(msg, reqData) && CheckFor(msg, reqDataEnd))
            {

                // pick the required information from incomming string
                msg = msg.Substring(msg.IndexOf(reqData) + reqData.Length - 1, msg.IndexOf(reqDataEnd) - (msg.IndexOf(reqData) + reqData.Length - 2));
                //Console.WriteLine("substring: " + msg);

                if (!CheckFor(msg, reqData))
                {
                    int posBeg = msg.IndexOf("[");
                    int posEnd = msg.IndexOf("]");

                    msg = msg.Remove(posEnd, msg.Length - posEnd);
                    msg = msg.Remove(0, posBeg + 1);

                    string strAngle;
                    for (int i = 0; i < 6; i++)
                    {
                        if (i == 5)
                            strAngle = msg;
                        else
                            strAngle = msg.Substring(0, msg.IndexOf(','));

                        try
                        {
                            val[i] = float.Parse(strAngle.Replace('.', ','));
                        }
                        catch (FormatException)
                        {
                            Console.WriteLine("Unable to parse");
                            return false; 
                        }
                        msg = msg.Remove(0, msg.IndexOf(',') + 1);
                        //Console.WriteLine(val[i]);
                        //Debug.Log(feedbackAngle[i]);
                            
                    }
                    return true;
                       
                }

            }

            return false; 
        }

        public bool GetMode(string msg, string data, ref byte mod)
        {
            // Console.WriteLine(msg);
            // {"holo":{"mode":0,"m"}}
            
            string reqData = "{\"" + data + "\":";
            string reqDataEnd = ",\"" + data.Substring(0, 1) + "\"}";

            //Console.WriteLine("reqData: " + reqData);
            //Console.WriteLine("regDataEnd: " + reqDataEnd);

            //check if message is no empty
            if (msg != null)
            {

                //check if message is complete
                if (CheckFor(msg, reqData) && CheckFor(msg, reqDataEnd))
                {
                    // pick the required information from incomming string
                    msg = msg.Substring(msg.IndexOf(reqData), msg.IndexOf(reqDataEnd) + reqDataEnd.Length - (msg.IndexOf(reqData) ));
                  
                    if (CheckFor(msg, reqData))
                    {

                        msg = msg.Substring(msg.IndexOf(reqData)+ reqData.Length, 1);

                        //Console.WriteLine("mode " + msg);
                        try
                        {
                            mod = byte.Parse(msg);
                        }
                        catch (Exception)
                        {
                            Console.WriteLine("TCP\tERROR getting mode");
                            return false; 
                        }
                        return true;

                    }

                }

            }
            return false;
        }

        //public static EgmSensor InterpretMsg(string msg)
        public bool GetDataEGM(string msg, ref float[] valPos, ref float[] valOri)
        {
            
            string msgStart = "{\"EGM\":{";
            string msgEnd = "]}\"e\"}";

            //check if message is no empty
            if (msg != null)
            {
                //check if message contains order and is complete
                if (CheckFor(msg, msgStart) && CheckFor(msg, msgEnd))
                {
                    //check if message startet with "planned" 
                    if (msg.IndexOf(msgEnd) < msg.IndexOf(msgStart))
                        msg = msg.Substring((msg.IndexOf(msgEnd) + msgEnd.Length));

                    //pick just one order from the incomming message
                    msg = msg.Substring(msg.IndexOf(msgStart) + msgStart.Length, msg.IndexOf(msgEnd) - msg.IndexOf(msgStart) - msgStart.Length + 1);
                    //Console.WriteLine("substring:\t" + msg);

                    // "pos":[-1.9263,0,0.1899],"ori":[-0.7071,0,-0.7071,0]
                    if (CheckFor(msg, "pos") && CheckFor(msg, "ori"))
                    {
                        int posPos = msg.IndexOf("\"pos\"");
                        int posOri = msg.IndexOf("\"ori\"");
                        int posEnd = msg.Length;

                        string strOri = msg.Substring(posOri, posEnd - posOri);
                        string strPos = msg.Substring(0, posOri - ",".Length);

                        // EXTRACT POSITION VALUES
                        // "pos":[-1.9263,0,0.1899]
                        strPos = strPos.Substring("\"pos\":[".Length, strPos.IndexOf("]") - "\"pos\":[".Length);
                        //Console.Write("pos: ");
                        for (int i = 0; i < 3; i++)
                        {
                            string strVal = string.Empty;
                            if (i == 2)
                                strVal = strPos;
                            else
                                strVal = strPos.Substring(0, strPos.IndexOf(","));

                            try
                            {
                                valPos[i] = float.Parse(strVal.Replace('.', ','))*1000;
                                valPos[i] = Convert.ToSingle(Math.Round((double)valPos[i] * 100) / 100);
                            }
                            catch (FormatException)
                            {
                                Console.WriteLine("Unable to parse");
                                return false;
                            }
                            strPos = strPos.Remove(0, strPos.IndexOf(',') + 1);
                            //Console.Write(valPos[i] + " ");
                        }
                        //Console.WriteLine();

                        // EXTRACT ORIENTATION VALUES
                        // "ori":[-0.7071,0,-0.7071,0]
                        strOri = strOri.Substring("\"ori\":[".Length, strOri.IndexOf("]") - "\"ori\":[".Length);
                        //Console.Write("ori: ");
                        for (int i = 0; i < 4; i++)
                        {
                            string strVal = string.Empty;
                            if (i == 3)
                                strVal = strOri;
                            else
                                strVal = strOri.Substring(0, strOri.IndexOf(","));

                            try
                            {
                                valOri[i] = float.Parse(strVal.Replace('.', ','));
                                valOri[i] = Convert.ToSingle(Math.Round((double)valOri[i] * 100) / 100);
                            }
                            catch (FormatException)
                            {
                                Console.WriteLine("Unable to parse");
                                return false;
                            }
                            strOri = strOri.Remove(0, strOri.IndexOf(',') + 1);
                            //Console.Write(valOri[i] + " ");
                        }
                        //Console.WriteLine();

                        return true;// 
                    }

                }

            }
            return false;
        }

        public bool GetHoloPath(string msg, ref float[] valPos, ref float[] valOri, ref int id, ref int num, ref bool simulate, ref bool move)
        {
            //{ "path":{ "id":1,"of":3,"simulate":False,"move":False,"pos":[0.5736,0,0.6899],"ori":[-0.7071,0,-0.7071,0]} "p"}
            //msg = "{\"path\":{\"id\":1,\"of\":3,\"pos\":[0.5736,0,0.6899],\"ori\":[-0.7071,0,-0.7071,0]}\"p\"}";
            string msgStart = "{\"path\":{";
            string msgEnd = "]}\"p\"}";

            //check if message is no empty
            if (msg == null)
                return false;

            //check if message contains order and is complete
            if (!(CheckFor(msg, msgStart) && CheckFor(msg, msgEnd)))
                return false; 

            //check if message startet with "planned" 
            if (msg.IndexOf(msgEnd) < msg.IndexOf(msgStart))
                msg = msg.Substring((msg.IndexOf(msgEnd) + msgEnd.Length));

            //pick just one order from the incomming message
            msg = msg.Substring(msg.IndexOf(msgStart) + msgStart.Length, msg.IndexOf(msgEnd) - msg.IndexOf(msgStart) - msgStart.Length + 1);
            //Console.WriteLine("substring:\t" + msg);

            // "id":1,"of":3,"simulate":False,"move":False,"pos":[0.5736,0,0.6899],"ori":[-0.7071,0,-0.7071,0]
            string strIdPos = "\"id\":";
            string strOfPos = ",\"of\":";
            string strSiPos = ",\"simulate\":";
            string strMoPos = ",\"move\":";
            string strEnPos = "\"pos\""; 

            if (!(CheckFor(msg, strIdPos) && CheckFor(msg, strOfPos)))
                return false; 
            
            string strId = msg.Substring(msg.IndexOf(strIdPos) + strIdPos.Length, msg.IndexOf(strOfPos) - msg.IndexOf(strIdPos) - strIdPos.Length);
            id = int.Parse(strId);

            string strSi = msg.Substring(msg.IndexOf(strSiPos) + strSiPos.Length, msg.IndexOf(strMoPos) - msg.IndexOf(strSiPos) - strSiPos.Length);
            if (strSi == "True")
                simulate = true;
            else
                simulate = false; 
            
            string strMo = msg.Substring(msg.IndexOf(strMoPos) + strMoPos.Length, msg.IndexOf(strEnPos) - msg.IndexOf(strMoPos) - strMoPos.Length - 1);
            if (strMo == "True")
                move = true;
            else
                move = false;

            string strNum = msg.Substring(msg.IndexOf(strOfPos) + strOfPos.Length, msg.IndexOf(strSiPos) - msg.IndexOf(strOfPos) - strOfPos.Length);
            num = int.Parse(strNum);





            msg = msg.Substring(msg.IndexOf(strEnPos));

            // "pos":[-1.9263,0,0.1899],"ori":[-0.7071,0,-0.7071,0]
            if (!(CheckFor(msg, "pos") && CheckFor(msg, "ori")))
                return false;


            int posPos = msg.IndexOf("\"pos\"");
            int posOri = msg.IndexOf("\"ori\"");
            int posEnd = msg.Length;

            string strOri = msg.Substring(posOri, posEnd - posOri);
            string strPos = msg.Substring(0, posOri - ",".Length);

            // EXTRACT POSITION VALUES
            // "pos":[-1.9263,0,0.1899]
            strPos = strPos.Substring("\"pos\":[".Length, strPos.IndexOf("]") - "\"pos\":[".Length);
            //Console.Write("pos: ");
            for (int i = 0; i < 3; i++)
            {
                string strVal = string.Empty;
                if (i == 2)
                    strVal = strPos;
                else
                    strVal = strPos.Substring(0, strPos.IndexOf(","));

                try
                {
                    valPos[i] = float.Parse(strVal.Replace('.', ',')) * 1000;
                    valPos[i] = Convert.ToSingle(Math.Round((double)valPos[i] * 100) / 100);
                }
                catch (FormatException)
                {
                    Console.WriteLine("Unable to parse");
                    return false;
                }
                strPos = strPos.Remove(0, strPos.IndexOf(',') + 1);
                //Console.Write(valPos[i] + " ");
            }

            // EXTRACT ORIENTATION VALUES
            // "ori":[-0.7071,0,-0.7071,0]
            strOri = strOri.Substring("\"ori\":[".Length, strOri.IndexOf("]") - "\"ori\":[".Length);
            //Console.Write("ori: ");
            for (int i = 0; i < 4; i++)
            {
                string strVal = string.Empty;
                if (i == 3)
                    strVal = strOri;
                else
                    strVal = strOri.Substring(0, strOri.IndexOf(","));

                try
                {
                    valOri[i] = float.Parse(strVal.Replace('.', ','));
                    valOri[i] = Convert.ToSingle(Math.Round((double)valOri[i] * 1000) / 1000);
                }
                catch (FormatException)
                {
                    Console.WriteLine("Unable to parse");
                    return false;
                }
                strOri = strOri.Remove(0, strOri.IndexOf(',') + 1);
                //Console.Write(valOri[i] + " ");
            }
            return true;
            
        }

        public bool GetHoloPathCommand(string msg, string cmd)
        {
            //{ "path":{"simulate":true} "p"}
            //msg = "{\"path\":{\"simulate\":true}\"p\"}";
            Console.WriteLine(msg);
            string msgStart = "{\"path\":{\"" + cmd + "\":";
            string msgEnd = "}\"p\"}";

            //check if message is no empty
            if (msg == null)
                return false;

            //check if message contains order and is complete
            if (!(CheckFor(msg, msgStart) && CheckFor(msg, msgEnd)))
                return false;

            //check if message startet with "planned" 
            if (msg.IndexOf(msgEnd) < msg.IndexOf(msgStart))
                msg = msg.Substring((msg.IndexOf(msgEnd) + msgEnd.Length));

            //pick just one order from the incomming message
            msg = msg.Substring(msg.IndexOf(msgStart), msg.IndexOf(msgEnd) + msgEnd.Length - msg.IndexOf(msgStart));
            Console.WriteLine("substring:\t" + msg);

            if (!(CheckFor(msg, msgStart) && CheckFor(msg, msgEnd)))
                return false;

            string activate = msg.Substring(msg.IndexOf(msgStart) + msgStart.Length, msg.IndexOf(msgEnd) - msg.IndexOf(msgStart) - msgStart.Length);
            Console.WriteLine(activate);


            return true;

        }

        //////////////////////////////////////////////////////////////////////////
        public EgmSensor CreateSensorMessageCartesian(float[] pos, float[] ori)
        {
            //Console.WriteLine(rx);
            //Console.WriteLine(ry);
            //Console.WriteLine(rz);

            EgmSensor newInstruction = new EgmSensor
            {
                // create a header
                Header = new EgmHeader
                {
                    Seqno = _seqNumber++,
                    Tm = ((uint)DateTime.Now.Ticks),
                    Mtype = EgmHeader.Types.MessageType.MsgtypeCorrection
                },
                // create some sensor data
                Planned = new EgmPlanned
                {
                    Cartesian = new EgmPose
                    {
                        /*
                        Euler = new EgmEuler
                        {
                            X = rx,
                            Y = ry,
                            Z = rz
                        },
                        */
                        Orient = new EgmQuaternion
                        {
                            U0 = ori[0],//u0,
                            U1 = ori[1],//u1,
                            U2 = ori[2],//u2,
                            U3 = ori[3]//u3

                        },
                        Pos = new EgmCartesian
                        {

                            X = pos[0],//x,
                            Y = pos[1],//y,
                            Z = pos[2]//z
                        }
                    }
                }

            };
            //Console.WriteLine("sending " + msgCnt++);
            //Console.WriteLine(newInstruction);

            return newInstruction;
        }

        public EgmSensor CreateSensorMessageJoint(float[] jointTar)
        {

            EgmSensor newInstruction = new EgmSensor
            {
                // create a header
                Header = new EgmHeader
                {
                    Seqno = _seqNumber++,
                    Tm = ((uint)DateTime.Now.Ticks),
                    Mtype = EgmHeader.Types.MessageType.MsgtypeCorrection
                },
                // create some sensor data
                Planned = new EgmPlanned
                {
                    Joints = new EgmJoints
                    {
                        Joints = { jointTar[0], jointTar[1], jointTar[2], jointTar[3], jointTar[4], jointTar[5], }
                    },
                }

            };

            return newInstruction;
        }
        /*
        private static void ExtractValuesPos(string data, ref double[] val)
        {
            //Console.WriteLine(data);

            int posX = data.IndexOf("x");
            int posY = data.IndexOf("y");
            int posZ = data.IndexOf("z");

            string X = data.Substring(posX + 3, posY - posX - 5);
            string Y = data.Substring(posY + 3, posZ - posY - 5);
            string Z = data.Substring(posZ + 3, data.Length - posZ - 4);

            val[0] = double.Parse(X.Replace('.', ','));
            val[1] = double.Parse(Y.Replace('.', ','));
            val[2] = double.Parse(Z.Replace('.', ','));

            //Console.WriteLine(val[0]);
            //Console.WriteLine(val[1]);
            //Console.WriteLine(val[2]);
        }
        */
        /*
        private static void ExtractValuesOri(string data, ref double[] val)
        {
            //Console.WriteLine(data);

            int posU0 = data.IndexOf("u0");
            int posU1 = data.IndexOf("u1");
            int posU2 = data.IndexOf("u2");
            int posU3 = data.IndexOf("u3");

            string U0 = data.Substring(posU0 + 4, posU1 - posU0 - 6);
            string U1 = data.Substring(posU1 + 4, posU2 - posU1 - 6);
            string U2 = data.Substring(posU2 + 4, posU3 - posU2 - 6);
            string U3 = data.Substring(posU3 + 4, data.Length - posU3 - 5);

            val[0] = double.Parse(U0.Replace('.', ','));
            val[1] = double.Parse(U1.Replace('.', ','));
            val[2] = double.Parse(U2.Replace('.', ','));
            val[3] = double.Parse(U3.Replace('.', ','));

            //Console.WriteLine(val[0]);
            //Console.WriteLine(val[1]);
            //Console.WriteLine(val[2]);
        }
        */
        public static bool CheckFor(string msg, string check)
        {
            if (msg.IndexOf(check) != -1 && msg.IndexOf(check) != msg.Length)
                return true;
            else
                return false;
        }


        public static EgmRobot DeSerialize(byte[] data)
        {

            try
            {

                EgmRobot receiveRoboter;
                receiveRoboter = EgmRobot.Parser.ParseFrom(data);

                //Program.sendOut = true;
                return receiveRoboter;


            }
            catch (Exception)
            {
                Console.WriteLine("data broken or wrong");
                return null;
            }


        }

        public bool GetLocalIPAddress(string ipset)
        {
            var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                //Console.WriteLine("TCP\tIP: " + ip.ToString());
                if (ip.ToString() == ipset)
                    return true;
                /*
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
                */
            }
            return false;
            //throw new Exception("No network adapters with an IPv4 address in the system!");
        }

    }

    public class HoloLens2
    {
        public bool connected = new bool();

        public float[] jointControl = new float[6];

        public Tcpstruct TCP = new Tcpstruct();
        public struct Tcpstruct
        {
            public bool Start { get; set; }
            public bool Stop { get; set; }
            public string IP { get; set; }
            public int PORT { get; set; }
            public string IP_default { get; set; }
            public bool UseDefaultIP { get; set; }

        }

    }
    public class CommandClass
    {

        public byte MODE { get; set; }        
        public byte CMD_DONOTHING { get; } = 0;
        public byte CMD_RANDOMPATH { get; } = 1;
        public byte CMD_EGMPOSE { get; } = 2;
        public byte CMD_JOINTCONTROL { get; } = 3;
        public byte CMD_HOLOPATH { get; } = 4;
        public byte CMD_WIZARD { get; } = 5;

        public bool OverrideDataSource = new bool();
        public bool HoloDataSourceSimulation = new bool();

        public WizardStruct WIZARD = new WizardStruct();
        public HoloPathStruct HOLOPATH = new HoloPathStruct();
        public struct WizardStruct
        {
            public bool Simulate { get; set; }
            public bool ModuleDownloadComplete { get; set; }
            public bool ModuleUploadComplete { get; set; }
            public bool VC_start { get; set; }
            public bool RC_start { get; set; }


        }

        public struct HoloPathStruct
        {
            public bool Simulate { get; set; }
            public bool Move { get; set; }

            public bool WaitValidation { get; set; }
        }

    }
    public class RobotFeedback
    {
        public string name = string.Empty;
        public string type = string.Empty;

        public double[] torque = new double[6];
        public double[] position = new double[6];
        public double[] positionEGM = new double[6];
        public double[] extTorque = new double[6];

        public float[]  CTRL_jointTarget = new float[6];
        public bool     CTRL_jointTargetReceived;
        public float[]  CTRL_EGMTargetPos = new float[3];
        public float[]  CTRL_EGMTargetOri = new float[4];
        public bool CTRL_sendMode; 

        public PoseStruct[] CTRL_holoPathPose = new PoseStruct[10];
        public int CTRL_holoPathNumber = new int(); 

        public double[] cartesianPosition = new double[3];
        public double[] eulerOrientation = new double[3];


        // public bool enable_EGM = new bool();
        // public bool online_EGM = new bool();
        /*
        public bool RAPID_executionCycleForever = new bool();
        public byte RAPID_mode;
        public bool RAPID_running = new bool();
        */

        public string MsgTorque = string.Empty;
        public string MsgJoint = string.Empty;
        public string MsgValidation = string.Empty;
        public string MsgPose = string.Empty; 

        public Udpstruct UDP = new Udpstruct();
        public Pcsdkstruct PCSDK = new Pcsdkstruct();
        public Rapidstruct RAPID = new Rapidstruct();

        public struct PoseStruct
        {
            public float[] pos;
            public float[] ori;         
        }

        public struct ErrorStruct
        {

            public bool PCSDK { get; set; }
            public bool UDP { get; set; }
        }

        public struct Rapidstruct
        {

            public bool Running { get; set; }   
            public bool ExecutionCycleForever { get; set; }
            public bool AutomaticMode { get; set; }
            public bool Start { get; set; }
            public bool Stop { get; set; }
        }
        public struct Pcsdkstruct 
        {
            public bool Online { get; set; }
            public bool Enable { get; set; }
            public bool Start { get; set; }
            public bool Stop { get; set; }
            public bool ERROR { get; set; }
        }
        public struct Udpstruct
        {
            public string IP { get; set; }
            public bool Start { get; set; }
            public bool Stop { get; set; }
            public int PORT { get; set; }
            public string IP_default { get; set; }
            public bool UseDefaultIP { get; set; }
            public bool Enable { get; set; }
            public bool Online { get; set; }
            public bool ERROR { get; set; }
        }
    }
}
