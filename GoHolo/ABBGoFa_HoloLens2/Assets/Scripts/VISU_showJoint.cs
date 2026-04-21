using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;


public class VISU_showJoint : MonoBehaviour
{
    public GameObject GO_data;
    private GameObject GO_watch;
    private GameObject GO_value;
    private Text GO_value_Text;

    public int Axis = 1;

    // color for gauge
    float S = 0.79f;
    float V = 0.98f;
    float HueGreen = 0.247f;
    float HueRed = 0.0011f;

    void Start()
    {
        //Debug.Log(TCPIPdata.GetComponent<TCPIPclient>().feedbackAngle[0]);
        GO_watch = gameObject.transform.Find("watchValue").gameObject;
        GO_value = gameObject.transform.Find("value").gameObject;
        GO_value_Text = GO_value.transform.GetChild(0).GetComponent<Text>();
    }


    void Update()
    {
        float angle = GO_data.GetComponent<data>().VC.angle[Axis - 1];

        if (angle < 0)
            GO_watch.GetComponent<Image>().fillClockwise = true;
        else
            GO_watch.GetComponent<Image>().fillClockwise = false;

        GO_watch.GetComponent<Image>().fillAmount = map(Math.Abs(angle), 0, 360, 0, 1);
        GO_watch.GetComponent<Image>().color = Color.HSVToRGB(getColor(angle), S, V);

        GO_value_Text.text = angle.ToString() + "°";

    }

    float getColor(float ang)
    {

        float val;
        if (ang < 0)
            val = (map(ang, GO_data.GetComponent<data>().axisLimit[Axis - 1, 0], 0, 1, 0));
        else
            val = (map(ang, 0, GO_data.GetComponent<data>().axisLimit[Axis - 1, 1], 0, 1));

        float clr = HueRed + (1 - val) * (HueGreen - HueRed);

        return clr;
    }

    float map(float x, float in_min, float in_max, float out_min, float out_max)
    {

        return (x - in_min) * (out_max - out_min) / (in_max - in_min) + out_min;
    }
}
