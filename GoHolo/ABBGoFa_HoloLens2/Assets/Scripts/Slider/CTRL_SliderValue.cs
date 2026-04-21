using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.MixedReality.Toolkit.UI;
using TMPro;
using UnityEngine;

public class CTRL_SliderValue : MonoBehaviour
{
    public GameObject GO_data;
    public GameObject GO_label; 
    public int Axis = 1; 
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        float increment = GO_data.GetComponent<data>().sliderIncrement; 
        //Debug.Log(gameObject.GetComponent<PinchSlider>().SliderValue);
        float mappedValue = map(gameObject.GetComponent<PinchSlider>().SliderValue, 0 , 1, -increment, increment);


        //GO_data.GetComponent<data>().axisLimit[(Axis-1),0], GO_data.GetComponent<data>().axisLimit[(Axis-1), 1]

        GO_data.GetComponent<data>().VC_CTRL.angle[Axis - 1] += mappedValue;
        if (GO_data.GetComponent<data>().VC_CTRL.angle[Axis - 1] <= GO_data.GetComponent<data>().axisLimit[(Axis - 1), 0])
            GO_data.GetComponent<data>().VC_CTRL.angle[Axis - 1] = GO_data.GetComponent<data>().axisLimit[(Axis - 1), 0];
        else if (GO_data.GetComponent<data>().VC_CTRL.angle[Axis - 1] >= GO_data.GetComponent<data>().axisLimit[(Axis - 1), 1])
            GO_data.GetComponent<data>().VC_CTRL.angle[Axis - 1] = GO_data.GetComponent<data>().axisLimit[(Axis - 1), 1];

        GO_label.GetComponent<TextMeshPro>().text = (Math.Round(GO_data.GetComponent<data>().VC_CTRL.angle[Axis - 1]*100)/100).ToString();
    }





    float map(float x, float in_min, float in_max, float out_min, float out_max)
    {
        return (x - in_min) * (out_max - out_min) / (in_max - in_min) + out_min;
    }
}
