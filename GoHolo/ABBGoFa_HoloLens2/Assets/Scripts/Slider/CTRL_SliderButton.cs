using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CTRL_SliderButton : MonoBehaviour
{
    public GameObject GO_data;
    public int Axis = 1;
    public float increment = 0.5f;
    public int multiplier = 1; 
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        GO_data.GetComponent<data>().VC_CTRL.angle[Axis - 1] += GO_data.GetComponent<data>().sliderIncrement*multiplier;
        if (GO_data.GetComponent<data>().VC_CTRL.angle[Axis - 1] <= GO_data.GetComponent<data>().axisLimit[(Axis - 1), 0])
            GO_data.GetComponent<data>().VC_CTRL.angle[Axis - 1] = GO_data.GetComponent<data>().axisLimit[(Axis - 1), 0];
        else if (GO_data.GetComponent<data>().VC_CTRL.angle[Axis - 1] >= GO_data.GetComponent<data>().axisLimit[(Axis - 1), 1])
            GO_data.GetComponent<data>().VC_CTRL.angle[Axis - 1] = GO_data.GetComponent<data>().axisLimit[(Axis - 1), 1];

        gameObject.GetComponent<CTRL_SliderButton>().enabled = false; 
    }
}
