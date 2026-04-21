using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using GoHolo; 

public class TCPIPclient : MonoBehaviour
{

    public GameObject GO_TCP;
    public GameObject GO_data;

    // public float[] feedbackAngle = new float[6];
    //public float[] feedbackTorque = { 1f, 2f, 3f, 4f, 5f, 6f };

    string IP = "192.168.1.10";
    //string IP = "192.168.125.5";
    //string IP = "127.0.0.1";
    int PORT = 5515;

    Socket client = null;
    public static bool serverConnected = false;

    int timeoutCounter = 0;
    int maxTimeouts = 50;

    int EGMinterval = 50; // [ms] // jakobnode was 20

    private DateTime LastModeUpdate;
    private int LastMode;

    int holoPathSendState = 0;
    int holoPathPoseCount = 0;
    bool holoPathSendCommand = false; 
    //int sendDecelerateCounter = 0; 

    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        if (!serverConnected)
        {
            //Debug.Log("\ntrying to connect");
            client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);


            try
            {
                IPAddress ip = null;
                IPAddress.TryParse(IP, out ip);
                IPEndPoint socketdata = new IPEndPoint(ip, PORT);

                IAsyncResult result = client.BeginConnect(ip, PORT, null, null);

                bool succes = result.AsyncWaitHandle.WaitOne(10, true);

                if (client.Connected)
                {
                    Debug.Log("\nconnection established");
                    serverConnected = true;
                }
                else
                {
                    client.Close();
                    throw new ApplicationException();
                }

            }
            catch (Exception)
            {
                //Debug.Log("\nTCP\tconnection timeout");
            }

        }
        
        if (serverConnected)
        {
            try
            {
                if (client.Connected)
                {
                    if (client.Available > 0)
                    {
                        //Debug.Log("\n______________");
                        byte[] buffReceived = new byte[256];
                        int nRecv = client.Receive(buffReceived);
                        string receivedMsg = Encoding.ASCII.GetString(buffReceived, 0, nRecv);
                        Debug.Log("\nData received: " + receivedMsg);

                        MessageProcessing msgPrc = new MessageProcessing();
                        msgPrc.GetData(receivedMsg, "joint", ref GO_data.GetComponent<data>().VC.angle);
                        //Debug.Log("\nangle:\t" + GO_data.GetComponent<data>().VC.angle);
                        msgPrc.GetData(receivedMsg, "pose", ref GO_data.GetComponent<data>().VC.pose);
                        Debug.Log("\npose:\t" + GO_data.GetComponent<data>().VC.pose);
                        msgPrc.GetData(receivedMsg, "torque", ref GO_data.GetComponent<data>().VC.torque);
                        //Debug.Log("\ntorque:\t" + GO_data.GetComponent<data>().VC.torque);
                        msgPrc.GetValidation(receivedMsg, "validation", ref GO_data.GetComponent<data>().VC.validation);
                        //Debug.Log("\nvalidation:\t" + GO_data.GetComponent<data>().VC.validation);

                        timeoutCounter = 0; 
                    }
                    else 
                    {
                        timeoutCounter++;
                    }

                }
                else
                {
                    //serverConnected = false;
                    End();
                }
        
                if (timeoutCounter >= maxTimeouts) {
                    Debug.Log("\nmaxTimeouts");
                    timeoutCounter = 0;
                    serverConnected = false;

                }
                }
            catch (Exception e)
            {
                Debug.Log("\nTCP\treceive error: " + e);
                End();
            }

        }

        // just send every 3 frames
        //sendDecelerateCounter++; 
        if (serverConnected/* && (sendDecelerateCounter>=3)*/) {
            //sendDecelerateCounter = 0;

            try
            {
                if (client.Connected)
                {

                    // switch did not work?!

                    // MESSAGES (HoloLens2 -> GoHolo)
                    // {"holo":{"EGM":{"pos":[-1.9263,0,0.1899],"ori":[-0.7071,0,-0.7071,0]}"e"}}
                    // {"holo":{"joint":[0,0,0,0,0,0]"j"}}
                    // {"holo":{"mode":0,"m"}}
                    // {"holo":{"path":{"simulate":true}"p"}}
                    // {"holo":{"path":{"id":1,"of":3,"pos":[0.5736,0,0.6899],"ori":[-0.7071,0,-0.7071,0]}"p"}}

                    if (GO_data.GetComponent<data>().MODEsend)
                    //if (GO_data.GetComponent<data>().MODE != LastMode)
                    {
                        string msg = "{\"holo\":{\"mode\":";
                        msg += GO_data.GetComponent<data>().MODE.ToString();
                        msg += ",\"m\"}}";

                        Debug.Log("\nsending: " + msg);
                        byte[] bytemsg = Encoding.UTF8.GetBytes(msg);
                        int i = client.Send(bytemsg);

                        //   LastMode = GO_data.GetComponent<data>().MODE;
                        GO_data.GetComponent<data>().MODEsend = false;
                    }
                    // MODE Joint Control
                    else if (GO_data.GetComponent<data>().MODE == GO_data.GetComponent<data>().CMD_JOINTCONTROL)
                    {
                        if (GO_data.GetComponent<data>().CMD.SendJoint) 
                        {
                            MessageProcessing msgPrc = new MessageProcessing();

                            string msg = "{\"holo\":";
                            msg += msgPrc.JointsForRobot(GO_data.GetComponent<data>().VC_CTRL.angle); 
                            
                            msg += "}";
                            //Debug.Log("\nsending: " + msg);
                            byte[] bytemsg = Encoding.UTF8.GetBytes(msg);
                            //byte[] bytemsg = Encoding.UTF8.GetBytes(updatePose());
                            int i = client.Send(bytemsg); // i is number von sent bytes
                                                          //Console.WriteLine(message + " = " + + "Bytes");
                            GO_data.GetComponent<data>().CMD.SendJoint = false;
                            //GO_data.GetComponent<data>().MODE = GO_data.GetComponent<data>().CMD_DONOTHING;
                            // jakobnode handschake damit nicht immer gesendet wird. 
                            //GO_data.GetComponent<data>().MODE = GO_data.GetComponent<data>().CMD_DONOTHING;
                        }
                    }

                    // MODE Move TCP (EGM) 
                    else if (GO_data.GetComponent<data>().MODE == GO_data.GetComponent<data>().CMD_EGMPOSE)
                    {
                        TimeSpan duration = DateTime.Now - LastModeUpdate;
                        if (duration.TotalMilliseconds > EGMinterval)
                        {
                            LastModeUpdate = DateTime.Now;

                            string msg = "{\"holo\":";
                            double[] pose = new double[7];
                            pose[0] = -Math.Truncate(10000 * GO_TCP.transform.localPosition.z) / 10000;
                            pose[1] =  Math.Truncate(10000 * GO_TCP.transform.localPosition.x) / 10000;
                            pose[2] =  Math.Truncate(10000 * GO_TCP.transform.localPosition.y) / 10000;
                            pose[3] =  Math.Truncate(10000 * GO_TCP.transform.localRotation.w) / 10000;
                            pose[4] =  Math.Truncate(10000 * GO_TCP.transform.localRotation.z) / 10000;
                            pose[5] = -Math.Truncate(10000 * GO_TCP.transform.localRotation.x) / 10000;
                            pose[6] = -Math.Truncate(10000 * GO_TCP.transform.localRotation.y) / 10000;
                            msg += updatePose("EGM", "e", pose);
                            msg += "}";

                            Debug.Log("\nsending: " + msg);
                            byte[] bytemsg = Encoding.UTF8.GetBytes(msg);
                            int i = client.Send(bytemsg);

                        }
                    }

                    // MODE holo path
                    else if (GO_data.GetComponent<data>().MODE == GO_data.GetComponent<data>().CMD_HOLOPATH)
                    {
                        if (GO_data.GetComponent<data>().CMD.PathSend && GO_data.GetComponent<data>().waypointNumber > 0)
                        {
                            double[] pose = new double[7];
                            for (int k = 0; k < 7; k++)
                                pose[k] = GO_data.GetComponent<data>().holoPathPose[holoPathPoseCount, k];

                            string msg = "{\"holo\":";
                            msg += "{\"path\":{\"id\":" + (holoPathPoseCount + 1) + ",\"of\":" + GO_data.GetComponent<data>().waypointNumber;
                            msg += ",\"simulate\":" + GO_data.GetComponent<data>().CMD.PathSimulate + ",\"move\":" + GO_data.GetComponent<data>().CMD.PathMove + ",";
                            string posestring = updatePose("path", "p", pose);
                            posestring = posestring.Substring(9);
                            msg += posestring;
                            msg += "}";

                            Debug.Log("\nsending: " + msg);
                            byte[] bytemsg = Encoding.UTF8.GetBytes(msg);
                            client.Send(bytemsg);
                            holoPathPoseCount++;
                            if (holoPathPoseCount >= GO_data.GetComponent<data>().waypointNumber)
                            {
                                GO_data.GetComponent<data>().CMD.PathSend = false;
                                holoPathPoseCount = 0;
                                holoPathSendCommand = true;
                                if (GO_data.GetComponent<data>().CMD.PathSimulate)
                                    GO_data.GetComponent<data>().CMD.PathSimulate = false;
                                if (GO_data.GetComponent<data>().CMD.PathMove)
                                    GO_data.GetComponent<data>().CMD.PathMove = false;
                            }

                        }
                        else
                            GO_data.GetComponent<data>().CMD.PathSend = false;
                        /*
                        if (GO_data.GetComponent<data>().CMD.PathSimulate && holoPathSendCommand)
                        {
                            //{ "path":{"simulate":true} "p"}
                            string msg = "{\"holo\":";
                            msg += "{\"path\":{\"simulate\":true}\"p\"}";
                            msg += "}";

                            Debug.Log("\nsending: " + msg);
                            byte[] bytemsg = Encoding.UTF8.GetBytes(msg);
                            client.Send(bytemsg);

                            GO_data.GetComponent<data>().CMD.PathSimulate = false;
                            holoPathSendCommand = false; 
                        }
                        */


                    }    

                    // MODE sending mode to GoHolo (every second)
                    /*
                    TimeSpan duration = DateTime.Now - LastModeUpdate;
                    if (duration.TotalMilliseconds > 1000)
                    {
                        LastModeUpdate = DateTime.Now;

                        string msg = "{\"holo\":{\"mode\":[";
                        msg += GO_data.GetComponent<data>().MODE.ToString();
                        msg += "]\"m\"}}";

                        Debug.Log("\nsending: " + msg);
                        byte[] bytemsg = Encoding.UTF8.GetBytes(msg);
                        int i = client.Send(bytemsg);
                    }
                    */
                    

                 

                }
                else
                {
                    End();
                    //serverConnected = false;
                }
            }
            catch (Exception e){
                Debug.Log("\nTCP\tsend error: " + e);
            }
        }
        
    }

    void End()
    {
        serverConnected = false;
        client.Close();
        client.Dispose();
    }

    string updatePose(string type, string delimeter, double[] pose){
        //{"holo":{"EGM":{"pos":[-1.9263,0,0.1899],"orient":[-0.7071,0,-0.7071,0]}"e"} }
        string positionToString;
        string message = "{\""+type+"\":{\"pos\":[";
        double value;

        value = pose[0];
        positionToString = value.ToString();
        positionToString = positionToString.Replace(',', '.');
        message += positionToString;
        message += ",";

        value = pose[1];
        positionToString = value.ToString();
        positionToString = positionToString.Replace(',', '.');
        message += positionToString;
        message += ",";

        value = pose[2];
        positionToString = value.ToString();
        positionToString = positionToString.Replace(',', '.');
        message += positionToString;
        message += "],\"ori\":[";

        //// ORIENTATION ////
        value = pose[3];
        positionToString = value.ToString();
        positionToString = positionToString.Replace(',', '.');
        message += positionToString;
        message += ",";

        value = pose[4];
        positionToString = value.ToString();
        positionToString = positionToString.Replace(',', '.');
        message += positionToString;
        message += ",";

        value = pose[5];
        positionToString = value.ToString();
        positionToString = positionToString.Replace(',', '.');
        message += positionToString;
        message += ",";

        value = pose[6];
        positionToString = value.ToString();
        positionToString = positionToString.Replace(',', '.');
        message += positionToString;
        message += "]}\""+delimeter+"\"}";
        
        return message;

    }

}

