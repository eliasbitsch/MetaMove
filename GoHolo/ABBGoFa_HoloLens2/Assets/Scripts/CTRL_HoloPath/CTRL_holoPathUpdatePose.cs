using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CTRL_holoPathUpdatePose : MonoBehaviour
{
    public GameObject GO_data;
    public int id = new int(); 
    void Start()
    {
        string name = gameObject.name;
        name = name.Substring("WP_".Length, name.Length-"WP_".Length);
        id = int.Parse(name);

    }

    void Update()
    { 
        double[] Pose = new double[7]; // x,y,z,q1,q2,q3,q4
        Pose[0] = -Math.Truncate(10000 * gameObject.transform.localPosition.z) / 10000;
        Pose[1] =  Math.Truncate(10000 * gameObject.transform.localPosition.x) / 10000;
        Pose[2] =  Math.Truncate(10000 * gameObject.transform.localPosition.y) / 10000;
        Pose[3] =  Math.Truncate(10000 * gameObject.transform.localRotation.w) / 10000;
        Pose[4] =  Math.Truncate(10000 * gameObject.transform.localRotation.z) / 10000;
        Pose[5] = -Math.Truncate(10000 * gameObject.transform.localRotation.x) / 10000;
        Pose[6] = -Math.Truncate(10000 * gameObject.transform.localRotation.y) / 10000;

        //Debug.Log("rotation:" + gameObject.transform.localEulerAngles.x + ", " + gameObject.transform.localEulerAngles.y + ", " + gameObject.transform.localEulerAngles.z);

        for (int i = 0; i < 7; i++)
            GO_data.GetComponent<data>().holoPathPose[id,i] = Pose[i];

       // Debug.Log("WAYPOINT ID: " + id + "\tpose: [" + Pose[0] + "," + Pose[1] + "," + Pose[2] + "],[" + Pose[3] + "," + Pose[4] + "," + Pose[5] + "," + Pose[6] + "]");
    }




}
