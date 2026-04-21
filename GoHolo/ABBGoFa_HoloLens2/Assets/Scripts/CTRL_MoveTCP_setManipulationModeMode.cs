using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CTRL_MoveTCP_setManipulationModeMode : MonoBehaviour
{
    public GameObject GO_ManipulationHide;
    public string mode; 
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        GO_ManipulationHide.GetComponent<CTRL_MoveTCP_HideOnManipulation>().mode = mode;
        Debug.Log("set mode to: " + mode);
        gameObject.GetComponent<CTRL_MoveTCP_setManipulationModeMode>().enabled = false;
    }
}
