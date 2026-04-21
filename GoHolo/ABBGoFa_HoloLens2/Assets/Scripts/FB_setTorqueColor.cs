using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FB_setTorqueColor : MonoBehaviour
{

    public GameObject data;
    public int setAxis = 1;

    // color for gauge
    float S = 0.79f;
    float V = 0.98f;
    float HueGreen = 0.247f;
    float HueRed = 0.0011f;


    void Start()
    {
        
    }

    void Update()
    {
        if (data.GetComponent<data>().SETUP.showColorTorque)
        {
            float torque = data.GetComponent<data>().VC.torque[setAxis - 1];
            gameObject.GetComponent<MeshRenderer>().material.color = Color.HSVToRGB(getColor(Math.Abs(torque)), S, V);
            if (torque == 255)
                gameObject.GetComponent<MeshRenderer>().material.color = Color.HSVToRGB(getColor(Math.Abs(torque)), 0, V);
        }
    }

    float getColor(float tor)
    {
        float val;
        if (tor > data.GetComponent<data>().maxTorque)
            tor = data.GetComponent<data>().maxTorque;

        val = (map(Mathf.Abs(tor), 0, data.GetComponent<data>().maxTorque, 0, 1));

        float clr = HueRed + (1 - val) * (HueGreen - HueRed);

        return clr;



    }

    float map(float x, float in_min, float in_max, float out_min, float out_max)
    {

        return (x - in_min) * (out_max - out_min) / (in_max - in_min) + out_min;
    }


}
