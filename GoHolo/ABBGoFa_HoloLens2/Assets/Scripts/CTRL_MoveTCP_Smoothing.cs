using System.Collections;
using System.Collections.Generic;
using Microsoft.MixedReality.Toolkit.UI;
using Microsoft.MixedReality.Toolkit.UI.BoundsControl;
using UnityEngine;

public class CTRL_MoveTCP_Smoothing : MonoBehaviour
{
    public GameObject GO_TCP;
    public string mode;
    private bool toggle; 
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        switch (mode) 
        {
            case "toggle":
                if (toggle)
                {
                    GO_TCP.GetComponent<ObjectManipulator>().MoveLerpTime = 0.965f;
                    GO_TCP.GetComponent<ObjectManipulator>().RotateLerpTime = 1;
                    GO_TCP.GetComponent<BoundsControl>().RotateLerpTime = 0.95f;
                    toggle = false; 
                }
                else
                {
                    GO_TCP.GetComponent<ObjectManipulator>().MoveLerpTime = 0.83f;
                    GO_TCP.GetComponent<ObjectManipulator>().RotateLerpTime = 0.83f;
                    GO_TCP.GetComponent<BoundsControl>().RotateLerpTime = 0.719f;
                    toggle = true; 
                }

                break;
            case "fine":
                GO_TCP.GetComponent<ObjectManipulator>().MoveLerpTime = 0.965f;
                GO_TCP.GetComponent<ObjectManipulator>().RotateLerpTime = 1;
                GO_TCP.GetComponent<BoundsControl>().RotateLerpTime = 0.95f;
                toggle = false; 
                break;
            case "rough":
                GO_TCP.GetComponent<ObjectManipulator>().MoveLerpTime = 0.83f;
                GO_TCP.GetComponent<ObjectManipulator>().RotateLerpTime = 0.83f;
                GO_TCP.GetComponent<BoundsControl>().RotateLerpTime = 0.719f;
                toggle = true; 
                break;
        }
        /*
        GO_TCP.GetComponent<ObjectManipulator>().MoveLerpTime = 0.95f;
        GO_TCP.GetComponent<ObjectManipulator>().RotateLerpTime = 1;
        GO_TCP.GetComponent<BoundsControl>().RotateLerpTime = 0.95f;
        */
        gameObject.GetComponent<CTRL_MoveTCP_Smoothing>().enabled = false; 
    }
}
