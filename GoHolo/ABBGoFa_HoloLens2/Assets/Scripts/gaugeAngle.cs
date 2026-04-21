using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System; 

public class gaugeAngle : MonoBehaviour
{
    public GameObject GO_data;
    public int Axis = 1;

    // angle limit for axes
    static int[,] axisLimit = { {-180,180}, {-180,180}, {-225,85}, {-180,180}, {-180,180},{-180,180} };
    
    // color for gauge
    float S = 0.79f;
    float V = 0.98f;
    float HueGreen = 0.247f;
    float HueRed = 0.0011f;

    void Start()
    {
        //Debug.Log(TCPIPdata.GetComponent<TCPIPclient>().feedbackAngle[0]);
    }


    void Update()
    {
        ;
        float angle = GO_data.GetComponent<data>().VC.angle[Axis - 1];


        if (angle < 0)
            gameObject.GetComponent<Image>().fillClockwise = false;
        else
            gameObject.GetComponent<Image>().fillClockwise = true;

        gameObject.GetComponent<Image>().fillAmount = map(Math.Abs(angle), 0, 360, 0, 1);
        gameObject.GetComponent<Image>().color = Color.HSVToRGB(getColor(angle), S, V);

    }

    float getColor(float ang) {

        float val;
        if (ang < 0)
            val = (map(ang, axisLimit[Axis-1, 0], 0, 1, 0));
        else
            val = (map(ang, 0, axisLimit[Axis-1, 1], 0, 1));

        float clr = HueRed + (1-val) * (HueGreen - HueRed);

        return clr;
    }

    float map(float x, float in_min, float in_max, float out_min, float out_max) {

            return (x - in_min) * (out_max - out_min) / (in_max - in_min) + out_min;
    }
}
