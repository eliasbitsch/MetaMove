using System.Collections;
using System.Collections.Generic;
using TMPro;
using System;
using UnityEngine;
using UnityEngine.UI;

public class CTRL_holoPathDisplayPosition : MonoBehaviour
{
    public GameObject GO_data;
    int id;

    public GameObject GO_pose;

    private GameObject GO_PosX;
    private Text GO_PosX_Text;

    private GameObject GO_PosY;
    private Text GO_PosY_Text;

    private GameObject GO_PosZ;
    private Text GO_PosZ_Text;

    private GameObject GO_OriX;
    private Text GO_OriX_Text;

    private GameObject GO_OriY;
    private Text GO_OriY_Text;

    private GameObject GO_OriZ;
    private Text GO_OriZ_Text;

    private GameObject GO_ID;
    private Text GO_ID_Text;

    void Start()
    {
        GameObject par = gameObject.transform.parent.parent.parent.parent.gameObject;
        string parName = par.name.Substring("WP_".Length, par.name.Length - "WP_".Length);
        id = int.Parse(parName);
        /*
        txt = gameObject.transform.GetChild(0).GetComponent<Text>();

        GameObject par = gameObject.transform.parent.parent.parent.parent.parent.gameObject;
        string parName = par.name.Substring("WP_".Length, par.name.Length - "WP_".Length);
        id = int.Parse(parName);
        */
        Debug.Log("ID: " + id);

        //txt.text = "hallo Welt";


        GO_PosX = gameObject.transform.Find("X").gameObject;
        GO_PosY = gameObject.transform.Find("Y").gameObject;
        GO_PosZ = gameObject.transform.Find("Z").gameObject;

        GO_OriX = gameObject.transform.Find("RX").gameObject;
        GO_OriY = gameObject.transform.Find("RY").gameObject;
        GO_OriZ = gameObject.transform.Find("RZ").gameObject;

        GO_ID = gameObject.transform.Find("ID").gameObject;


        /*
        if (GO_PosX != null)
            Debug.Log("found child");
        else
            Debug.Log("child NOT found");
        */

        GO_PosX_Text = GO_PosX.transform.GetChild(0).GetComponent<Text>();
        GO_PosY_Text = GO_PosY.transform.GetChild(0).GetComponent<Text>();
        GO_PosZ_Text = GO_PosZ.transform.GetChild(0).GetComponent<Text>();

        GO_OriX_Text = GO_OriX.transform.GetChild(0).GetComponent<Text>();
        GO_OriY_Text = GO_OriY.transform.GetChild(0).GetComponent<Text>();
        GO_OriZ_Text = GO_OriZ.transform.GetChild(0).GetComponent<Text>();

        GO_ID_Text = GO_ID.transform.GetChild(0).GetComponent<Text>();

    }

    // Update is called once per frame
    void Update()
    {
        GO_ID_Text.text = (id + 1).ToString();

        double value;

        //value = GO_data.GetComponent<data>().holoPathPose[id, 0]*1000;
        value = -Math.Truncate(100 * GO_pose.transform.localPosition.z * 1000) / 100;
        GO_PosX_Text.text = value.ToString();

        //value = GO_data.GetComponent<data>().holoPathPose[id, 1] * 1000;
        value = Math.Truncate(100 * GO_pose.transform.localPosition.x * 1000) / 100;
        GO_PosY_Text.text = value.ToString();

        //value = GO_data.GetComponent<data>().holoPathPose[id, 2] * 1000;
        value = Math.Truncate(100 * GO_pose.transform.localPosition.y * 1000) / 100;
        GO_PosZ_Text.text = value.ToString();


        /*
        Pose[0] = -Math.Truncate(10000 * gameObject.transform.localPosition.z) / 10000;
        Pose[1] = Math.Truncate(10000 * gameObject.transform.localPosition.x) / 10000;
        Pose[2] = Math.Truncate(10000 * gameObject.transform.localPosition.y) / 10000;
        */
        /*
        Pose[3] = Math.Truncate(10000 * gameObject.transform.localRotation.w) / 10000;
        Pose[4] = Math.Truncate(10000 * gameObject.transform.localRotation.z) / 10000;
        Pose[5] = -Math.Truncate(10000 * gameObject.transform.localRotation.x) / 10000;
        Pose[6] = -Math.Truncate(10000 * gameObject.transform.localRotation.y) / 10000;
        */
        Quaternion Q = new Quaternion();
        Q.w = GO_pose.transform.localRotation.w;
        Q.x = GO_pose.transform.localRotation.z;
        Q.y = -GO_pose.transform.localRotation.x;
        Q.z = -GO_pose.transform.localRotation.y;

        EulerAngles E = ToEulerAngles(Q);
        //Debug.Log("rotation:" + E.roll + ", " + E.pitch + ", " + E.yaw);

        value = -Math.Truncate(100 * E.roll) / 100;
        GO_OriX_Text.text = value.ToString();

        value = -Math.Truncate(100 * E.pitch) / 100;
        GO_OriY_Text.text = value.ToString();

        value = -Math.Truncate(100 * E.yaw) / 100;
        GO_OriZ_Text.text = value.ToString();

        /*
        switch (showValue)
        {
            case "X":
                
        
        
        }
        */

        /*
                 double[] Pose = new double[7]; // x,y,z,q1,q2,q3,q4
        Pose[0] = -Math.Truncate(10000 * gameObject.transform.localPosition.z) / 10000;
        Pose[1] =  Math.Truncate(10000 * gameObject.transform.localPosition.x) / 10000;
        Pose[2] =  Math.Truncate(10000 * gameObject.transform.localPosition.y) / 10000;
        Pose[3] =  Math.Truncate(10000 * gameObject.transform.localRotation.w) / 10000;
        Pose[4] =  Math.Truncate(10000 * gameObject.transform.localRotation.z) / 10000;
        Pose[5] = -Math.Truncate(10000 * gameObject.transform.localRotation.x) / 10000;
        Pose[6] = -Math.Truncate(10000 * gameObject.transform.localRotation.y) / 10000;

        Debug.Log("rotation:" + gameObject.transform.localEulerAngles.x + ", " + gameObject.transform.localEulerAngles.y + ", " + gameObject.transform.localEulerAngles.z);

        for(int i = 0; i < 7; i++)
            GO_data.GetComponent<data>().holoPathPose[id,i] = Pose[i];
        */

    }


    public struct Quaternion
    {
        public double w, x, y, z;
    };

    struct EulerAngles
    {
        public double roll, pitch, yaw;
    };

    EulerAngles ToEulerAngles(Quaternion q)
    {
        EulerAngles angles;

        // roll (x-axis rotation)
        double sinr_cosp = 2 * (q.w * q.x + q.y * q.z);
        double cosr_cosp = 1 - 2 * (q.x * q.x + q.y * q.y);
        angles.roll = Math.Atan2(sinr_cosp, cosr_cosp) * 180 / Math.PI;

        // pitch (y-axis rotation)
        double sinp = 2 * (q.w * q.y - q.z * q.x);
        if (Math.Abs(sinp) >= 1)
        {
            if (sinp < 0)
                angles.pitch = -90;//Math.PI / 2;
            else
                angles.pitch = 90;// Math.PI / 2;
        }
        else
            angles.pitch = Math.Asin(sinp) * 180 / Math.PI;

        // yaw (z-axis rotation)
        double siny_cosp = 2 * (q.w * q.z + q.x * q.y);
        double cosy_cosp = 1 - 2 * (q.y * q.y + q.z * q.z);
        angles.yaw = Math.Atan2(siny_cosp, cosy_cosp) * 180 / Math.PI;

        return angles;
    }

}
