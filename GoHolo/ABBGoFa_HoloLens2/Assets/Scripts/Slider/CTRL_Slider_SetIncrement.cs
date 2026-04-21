using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CTRL_Slider_SetIncrement : MonoBehaviour
{
    public GameObject GO_data;
    public float incrementFine = 0.1f;
    public float incrementRough = 0.5f;
    public string incrementType = "fine"; 

    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        switch (incrementType) 
        {
            case "fine":
                GO_data.GetComponent<data>().sliderIncrement = incrementFine; 
                break;
            case "rough":
                GO_data.GetComponent<data>().sliderIncrement = incrementRough;
                break;
        }
        gameObject.GetComponent<CTRL_Slider_SetIncrement>().enabled = false; 
    }
}
