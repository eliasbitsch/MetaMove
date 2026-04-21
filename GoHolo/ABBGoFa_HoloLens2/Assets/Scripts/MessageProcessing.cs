using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace GoHolo
{
    public class MessageProcessing
    {
        // data = "joint" || "torque")
        public bool GetData(string msg, string data, ref float[] val) 
        {
            //Console.WriteLine(msg);

            string reqData = "{\""+data+"\":[";
            string reqDataEnd = "]\""+data.Substring(0,1)+"\"}";

            //Debug.Log("getting: " + data);

            //Console.WriteLine("reqData: " + reqData);
            //Console.WriteLine("regDataEnd: " + reqDataEnd);

            
            //check if message is no empty
            if (msg != null)
            {

                //check if message is complete
                if (CheckFor(msg, "{\"VC\":") && CheckFor(msg, "}}") && CheckFor(msg, reqData) && CheckFor(msg, reqDataEnd))
                {
                    // check if message starts correctly
                    if (msg.IndexOf("}}") < msg.IndexOf("{\"VC\":")) 
                    { 
                        msg = msg.Substring((msg.IndexOf("}}") + "}}".Length));
                        //Console.WriteLine("start check: " + msg);
                    }
                    //pick the required information from incomming string
                    msg = msg.Substring(msg.IndexOf(reqData)+reqData.Length - 1 , msg.IndexOf(reqDataEnd) - (msg.IndexOf(reqData) + reqData.Length - 2));
                    //Console.WriteLine("substring: " + msg);

                    // shorten message if still too long
                    /*
                    if (msg.Length > (msg.IndexOf(reqDataEnd) + reqDataEnd.Length))
                    {
                        msg = msg.Remove(msg.IndexOf(reqDataEnd) + reqDataEnd.Length);
                        Console.WriteLine("shorten: " + msg);
                    }
                    */
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
                                Debug.Log("\nUnable to parse");
                            }
                            msg = msg.Remove(0, msg.IndexOf(',') + 1);
                            //Console.WriteLine(val[i]);
                            //Debug.Log(val[i]);
                        }

                        /*
                        Link01.transform.localRotation = Quaternion.Euler(new Vector3(0, -feedbackAngle[0], 0));
                        Link02.transform.localRotation = Quaternion.Euler(new Vector3(0, 0, feedbackAngle[1]));
                        Link03.transform.localRotation = Quaternion.Euler(new Vector3(0, 0, feedbackAngle[2]));
                        Link04.transform.localRotation = Quaternion.Euler(new Vector3(feedbackAngle[3], 0, 0));
                        Link05.transform.localRotation = Quaternion.Euler(new Vector3(0, 0, feedbackAngle[4]));
                        Link06.transform.localRotation = Quaternion.Euler(new Vector3(feedbackAngle[5], 0, 0));
                        */
                    }

                }

            }
            return true; 

        }
        public bool GetValidation(string msg, string data, ref bool[] val)
        {


            //Console.WriteLine(msg);
            // {"validation":[True,False,False]"v"}

            string reqData = "{\"" + data + "\":[";
            string reqDataEnd = "]\"" + data.Substring(0, 1) + "\"}";
            //Debug.Log("GETTING VALIDATION");
            //Debug.Log("getting: " + data);

            //Console.WriteLine("reqData: " + reqData);
            //Console.WriteLine("regDataEnd: " + reqDataEnd);


            //check if message is no empty
            if (msg != null)
            {


                //check if message is complete
                if (CheckFor(msg, "{\"VC\":") && CheckFor(msg, "}}") && CheckFor(msg, reqData) && CheckFor(msg, reqDataEnd))
                {
                    // check if message starts correctly
                    if (msg.IndexOf("}}") < msg.IndexOf("{\"VC\":"))
                    {
                        msg = msg.Substring((msg.IndexOf("}}") + "}}".Length));
                        //Console.WriteLine("start check: " + msg);
                    }
                    //pick the required information from incomming string
                    msg = msg.Substring(msg.IndexOf(reqData) + reqData.Length - 1, msg.IndexOf(reqDataEnd) - (msg.IndexOf(reqData) + reqData.Length - 2));
                    //Console.WriteLine("substring: " + msg);

                    // shorten message if still too long
                    /*
                    if (msg.Length > (msg.IndexOf(reqDataEnd) + reqDataEnd.Length))
                    {
                        msg = msg.Remove(msg.IndexOf(reqDataEnd) + reqDataEnd.Length);
                        Console.WriteLine("shorten: " + msg);
                    }
                    */
                    if (!CheckFor(msg, reqData))
                    {
                        int posBeg = msg.IndexOf("[");
                        int posEnd = msg.IndexOf("]");

                        msg = msg.Remove(posEnd, msg.Length - posEnd);
                        msg = msg.Remove(0, posBeg + 1);

                        //Console.WriteLine(msg);

                        int cnt = 0;
                        Debug.Log("Setting Validation");

                        for (int i = 0; i < 10; i++)
                            val[i] = true;

                        while (msg.Length > 0)
                        {
                            int IndexTrue = msg.IndexOf("True");
                            int IndexFalse = msg.IndexOf("False");

                            if (IndexTrue < 0)
                                IndexTrue = 255;
                            if (IndexFalse < 0)
                                IndexFalse = 255;

                            //Console.WriteLine("false: " + IndexFalse);
                            //Console.WriteLine("true: " + IndexTrue);


                            if (IndexTrue < IndexFalse)
                                val[cnt] = true;
                            else if (IndexFalse < IndexTrue)
                                val[cnt] = false;
                            else
                                break;

                            if (msg.IndexOf(",") > 0)
                                msg = msg.Substring(msg.IndexOf(",") + 1);
                            else
                                msg = String.Empty;

                            //Console.WriteLine();
                            //Console.WriteLine(msg);
                            cnt++;
                        }

                    }

                }

            }
            return true;

        }
        /*
        void GetTorque(string msg)
        {
            //{ "simulation":{ "torque":[26.36,-49.23,-20.81,-4.13,11.03,6.84]"t"}}
            //check if message is no empty
            float[] tor = new float[6];
            if (msg != null)
            {

                //check if message contains order and is complete
                if (checkFor(msg, "{\"simulation\":{\"torque\":[") && checkFor(msg, "\"t\"}}"))
                {

                    if (msg.IndexOf("\"t\"}}") < msg.IndexOf("{\"simulation\":{\"torque\":["))
                        msg = msg.Substring((msg.IndexOf("\"t\"}}") + "\"t\"}}".Length));

                    //pick just one order from the incomming message
                    msg = msg.Substring(msg.IndexOf("{\"simulation\":{\"torque\":["), (msg.IndexOf("}") - (msg.IndexOf("{\"simulation\":{\"torque\":[") - 2)));
                    Debug.Log("Substring:  " + msg);

                    if (msg.Length > (msg.IndexOf("\"t\"}}") + "\"t\"}}".Length))
                        msg = msg.Remove((msg.IndexOf("\"t\"}}") + "\"t\"}}".Length));
                    Debug.Log("removed " + msg);

                    if (checkFor(msg, "{\"simulation\":{\"torque\":["))
                    {
                        int posEnd = msg.IndexOf("]");
                        int posBeg = msg.IndexOf("[");

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
                                tor[i] = float.Parse(strAngle.Replace('.', ','));
                            }
                            catch (FormatException)
                            {
                                Debug.Log("Unable to parse");
                            }
                            msg = msg.Remove(0, msg.IndexOf(',') + 1);
                            //Debug.Log(tor[i]);
                            data.GetComponent<data>().feedbackTorque[i] = tor[i];
                        }

                    }

                }

            }

        }

        */
        public string JointsForRobot(float[] val)
        {

                string jointMsg = "{\"joint\":[";
                float value;

                value = (float)(Math.Round(val[0] * 100) / 100);
                jointMsg += value.ToString().Replace(',', '.');
                jointMsg += ",";
                value = (float)(Math.Round(val[1] * 100) / 100);
                jointMsg += value.ToString().Replace(',', '.');
                jointMsg += ",";
                value = (float)(Math.Round(val[2] * 100) / 100);
                jointMsg += value.ToString().Replace(',', '.');
                jointMsg += ",";
                value = (float)(Math.Round(val[3] * 100) / 100);
                jointMsg += value.ToString().Replace(',', '.');
                jointMsg += ",";
                value = (float)(Math.Round(val[4] * 100) / 100);
                jointMsg += value.ToString().Replace(',', '.');
                jointMsg += ",";
                value = (float)(Math.Round(val[5] * 100) / 100);
                jointMsg += value.ToString().Replace(',', '.');
                jointMsg += "]\"j\"}";

                Debug.Log(jointMsg);
                return jointMsg;

        }

        bool CheckFor(string msg, string check)
        {
            if (msg.IndexOf(check) != -1 && msg.IndexOf(check) != msg.Length)
                return true;
            else
                return false;
        }

    }
}
