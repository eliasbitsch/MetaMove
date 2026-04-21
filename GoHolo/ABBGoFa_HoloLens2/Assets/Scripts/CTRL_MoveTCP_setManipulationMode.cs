using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CTRL_MoveTCP_setManipulationMode : MonoBehaviour
{
    public GameObject GO_ManipulationHide;
    public string mode;
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (mode == "toggle")
        {
            if (GO_ManipulationHide.GetComponent<CTRL_MoveTCP_HideOnManipulation>().hide)
            {
                GO_ManipulationHide.GetComponent<CTRL_MoveTCP_HideOnManipulation>().hide = false;
                Debug.Log("\nhide on manipulation: FALSE");
            }

            else
            {
                GO_ManipulationHide.GetComponent<CTRL_MoveTCP_HideOnManipulation>().hide = true;
                Debug.Log("\nhide on manipulation: TRUE");
            }
        }
        else if (mode == "reset")
        {
            GO_ManipulationHide.GetComponent<CTRL_MoveTCP_HideOnManipulation>().hide = false;
        }

        gameObject.GetComponent<CTRL_MoveTCP_setManipulationMode>().enabled = false; 
    }
}
