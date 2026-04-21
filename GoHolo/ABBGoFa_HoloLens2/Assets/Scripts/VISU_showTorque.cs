using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;

public class VISU_showTorque : MonoBehaviour
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
        GO_watch = gameObject.transform.Find("watchValue").gameObject;
        GO_value = gameObject.transform.Find("value").gameObject;
        GO_value_Text = GO_value.transform.GetChild(0).GetComponent<Text>();
    }

    void Update()
    {
        float torque = GO_data.GetComponent<data>().VC.torque[Axis - 1];

        if (torque < 0)
            GO_watch.GetComponent<Image>().fillClockwise = true;
        else
            GO_watch.GetComponent<Image>().fillClockwise = false;

        float tor = torque;
        if (tor > GO_data.GetComponent<data>().maxTorque)
            tor = GO_data.GetComponent<data>().maxTorque; 

        float m = map(Math.Abs(tor), 0, GO_data.GetComponent<data>().maxTorque, 0, 0.25f);
        if (m >= 0.25f)
            m = 0.25f;
        GO_watch.GetComponent<Image>().fillAmount = m;
        GO_watch.GetComponent<Image>().color = Color.HSVToRGB(getColor(tor), S, V);

        int t = (int)torque; 
        GO_value_Text.text = t.ToString() + "Nm";
    }

    float getColor(float tor)
    {
        float val;
        if (tor > GO_data.GetComponent<data>().maxTorque)
            tor = GO_data.GetComponent<data>().maxTorque;

        val = (map(Mathf.Abs(tor), 0, GO_data.GetComponent<data>().maxTorque, 0, 1));

        float clr = HueRed + (1 - val) * (HueGreen - HueRed);

        return clr;
    }

    float map(float x, float in_min, float in_max, float out_min, float out_max)
    {
        return (x - in_min) * (out_max - out_min) / (in_max - in_min) + out_min;
    }

}


