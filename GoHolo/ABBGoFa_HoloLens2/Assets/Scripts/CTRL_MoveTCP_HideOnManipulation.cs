using System.Collections;
using System.Collections.Generic;
using Microsoft.MixedReality.Toolkit.UI.BoundsControl;
using UnityEngine;

public class CTRL_MoveTCP_HideOnManipulation : MonoBehaviour
{
    public GameObject GO_BoundsControl;
    public GameObject GO_TCP;
    public string mode;
    public bool hide; 

    void Start()
    {
        hide = false;    
    }

    void Update()
    {
        Debug.Log("change visibility");
        switch (mode)
        {
            case "hide":
                if (hide)
                {
                    //GO_BoundsControl.GetComponent<BoundsControl>().enabled = false;
                    GO_TCP.SetActive(false);
                    Debug.Log("\nhide on manipulation: HIDE");
                }

                break;
            case "show":
                if (hide)
                {
                    //GO_BoundsControl.GetComponent<BoundsControl>().enabled = true;
                    GO_TCP.SetActive(true);
                    Debug.Log("\nhide on manipulation: SHOW");
                }
                break;
                /*
            case "toggle":
                if (hide)
                    hide = false;
                else
                    hide = true; 
                break;
                */
        }
        gameObject.GetComponent<CTRL_MoveTCP_HideOnManipulation>().enabled = false; 
    }

    public string setMode(string md)
    {
        return md; 
    
    }
}
