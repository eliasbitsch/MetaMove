using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CTRL_holoPathMaster : MonoBehaviour
{
    public int waypointNumber = new int();
    public double[,] holoPathPose = new double[10,7];
    void Start()
    {
        waypointNumber = 1; 
    }

   
    void Update()
    {
        
    }


    /*
    string updatePose()
    {
        //{"holo":{"EGM":{"pos":[-1.9263,0,0.1899],"orient":[-0.7071,0,-0.7071,0]}"e"} }
        string positionToString;
        string message = "{\"EGM\":{\"pos\":[";
        double value;

        value = -Math.Truncate(10000 * GO_TCP.transform.localPosition.z) / 10000;
        positionToString = value.ToString();
        positionToString = positionToString.Replace(',', '.');
        message += positionToString;
        message += ",";

        value = Math.Truncate(10000 * GO_TCP.transform.localPosition.x) / 10000;
        positionToString = value.ToString();
        positionToString = positionToString.Replace(',', '.');
        message += positionToString;
        message += ",";

        value = Math.Truncate(10000 * GO_TCP.transform.localPosition.y) / 10000;
        positionToString = value.ToString();
        positionToString = positionToString.Replace(',', '.');
        message += positionToString;
        message += "],\"orient\":[";

        //// ORIENTATION ////
        value = Math.Truncate(10000 * GO_TCP.transform.localRotation.w) / 10000;
        positionToString = value.ToString();
        positionToString = positionToString.Replace(',', '.');
        message += positionToString;
        message += ",";

        value = Math.Truncate(10000 * GO_TCP.transform.localRotation.z) / 10000;
        positionToString = value.ToString();
        positionToString = positionToString.Replace(',', '.');
        message += positionToString;
        message += ",";

        value = -Math.Truncate(10000 * GO_TCP.transform.localRotation.x) / 10000;
        positionToString = value.ToString();
        positionToString = positionToString.Replace(',', '.');
        message += positionToString;
        message += ",";

        value = -Math.Truncate(10000 * GO_TCP.transform.localRotation.y) / 10000;
        positionToString = value.ToString();
        positionToString = positionToString.Replace(',', '.');
        message += positionToString;
        message += "]}\"e\"}";

        return message;

    }
    */

}
