using System.Collections;
using System.Collections.Generic;
using Microsoft.MixedReality.Toolkit.UI;
using UnityEngine;

public class CTRL_SliderValueInit : MonoBehaviour
{
    public GameObject GO_data;
    public int Axis; 

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        //Debug.Log("SLIDER init");
        GO_data.GetComponent<data>().VC_CTRL.angle[Axis - 1] = GO_data.GetComponent<data>().VC.angle[Axis - 1];
        //gameObject.GetComponent<PinchSlider>().SliderValue = map(GO_data.GetComponent<data>().VC.angle[Axis-1], GO_data.GetComponent<data>().axisLimit[(Axis - 1), 0], GO_data.GetComponent<data>().axisLimit[(Axis - 1), 1], 0, 1);
        gameObject.GetComponent<CTRL_SliderValueInit>().enabled = false; 
    }

    float map(float x, float in_min, float in_max, float out_min, float out_max)
    {
        return (x - in_min) * (out_max - out_min) / (in_max - in_min) + out_min;
    }

}
