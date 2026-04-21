using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class data : MonoBehaviour
{

    public setupstruct SETUP = new setupstruct();

    public GameObject VCLink01;
    public GameObject VCLink02;
    public GameObject VCLink03;
    public GameObject VCLink04;
    public GameObject VCLink05;
    public GameObject VCLink06;

    public FeedbackClass VC = new FeedbackClass();
    public FeedbackClass RC = new FeedbackClass();

    public DataClass VC_CTRL = new DataClass();
    public DataClass RC_CTRL = new DataClass();

    public int MODE = new int();
    public bool MODEsend = new bool(); 
    public CommandClass CMD = new CommandClass();

    public int waypointNumber = new int();
    public double[,] holoPathPose = new double[10, 7];

    public byte CMD_DONOTHING { get; } = 0;
    public byte CMD_RANDOMPATH { get; } = 1;
    public byte CMD_EGMPOSE { get; } = 2;
    public byte CMD_JOINTCONTROL { get; } = 3;
    public byte CMD_HOLOPATH { get; } = 4;
    public byte CMD_WIZARD { get; } = 5;

    public float sliderIncrement = 0.5f; 

    // angle limit for axes
    public int[,] axisLimit = { { -180, 180 }, { -180, 180 }, { -225, 85 }, { -180, 180 }, { -180, 180 }, { -180, 180 } };


    public int maxTorque = 100;//80; // in Nm //10 Yumi 80 GoFa

    public struct setupstruct
    {
        public bool showColorTorque;
        public bool showGaugeAngle;
        
    }

    // Start is called before the first frame update
    void Start()
    {
        SETUP.showColorTorque = false;
        SETUP.showGaugeAngle = false;
        MODE = CMD_DONOTHING;
        waypointNumber = 0;

        for (int i = 0; i < 10; i++)
            VC.validation[i] = true; 
    }

    // Update is called once per frame
    void Update()
    {
        if(MODE != CMD_JOINTCONTROL) 
        {
            VCLink01.transform.localRotation = Quaternion.Euler(new Vector3(0, -VC.angle[0], 0));
            VCLink02.transform.localRotation = Quaternion.Euler(new Vector3(0, 0, VC.angle[1]));
            VCLink03.transform.localRotation = Quaternion.Euler(new Vector3(0, 0, VC.angle[2]));
            VCLink04.transform.localRotation = Quaternion.Euler(new Vector3(VC.angle[3], 0, 0));
            VCLink05.transform.localRotation = Quaternion.Euler(new Vector3(0, 0, VC.angle[4]));
            VCLink06.transform.localRotation = Quaternion.Euler(new Vector3(VC.angle[5], 0, 0));
        }

        if (MODE == CMD_JOINTCONTROL)
        {
            VCLink01.transform.localRotation = Quaternion.Euler(new Vector3(0, -VC_CTRL.angle[0], 0));
            VCLink02.transform.localRotation = Quaternion.Euler(new Vector3(0, 0, VC_CTRL.angle[1]));
            VCLink03.transform.localRotation = Quaternion.Euler(new Vector3(0, 0, VC_CTRL.angle[2]));
            VCLink04.transform.localRotation = Quaternion.Euler(new Vector3(VC_CTRL.angle[3], 0, 0));
            VCLink05.transform.localRotation = Quaternion.Euler(new Vector3(0, 0, VC_CTRL.angle[4]));
            VCLink06.transform.localRotation = Quaternion.Euler(new Vector3(VC_CTRL.angle[5], 0, 0));
        }
    }

}

public class FeedbackClass
{
    public float[] angle = new float[6];
    public float[] torque = new float[6];
    public bool[] validation = new bool[10];
    public float[] pose = new float[6];
}

public class DataClass
{
    public float[] angle = new float[6];

}

public class CommandClass
{
    public bool SendJoint = new bool();
    public bool PathSend = new bool();
    public bool PathSimulate = new bool();
    public bool PathMove = new bool();
}