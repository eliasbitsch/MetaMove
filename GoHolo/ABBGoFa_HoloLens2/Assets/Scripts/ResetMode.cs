using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ResetMode : MonoBehaviour
{
   // public GameObject Mode_Change;
    public GameObject Visu_Opaque;
    public GameObject Mode_Jointcontrol;
    public GameObject Mode_MoveTCP;
    public GameObject Mode_HoloPath;
    public GameObject TCP;
    public GameObject Link0;
    public GameObject Mode_HoloPath_DestroyWaypoints;
    public GameObject BK;
    public GameObject HP_MoveButton;
    public GameObject GO_SliderRough;
    public GameObject GO_SliderSettings;
    public GameObject GO_PathRough;
    public GameObject GO_PathSettings;
    public GameObject GO_PathHideReset;
    public GameObject GO_EGMRough;
    public GameObject GO_EGMSettings;
    public GameObject GO_EGMResetConstraint;
    public GameObject GO_EGMHideReset;


    public string ModeToReset;

    // Start is called before the first frame update

    Vector3 startPos = new Vector3(0, 0, 2);

    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        switch (ModeToReset)
        {
            case "JointControl":
                Debug.Log("RESET JointControl");
                Mode_Jointcontrol.transform.position = startPos;
                Mode_Jointcontrol.SetActive(false);
                GO_SliderRough.GetComponent<CTRL_Slider_SetIncrement>().enabled = true;
                GO_SliderSettings.SetActive(false);
                //Visu_Opaque.GetComponent<VISU_setOpaque>().enabled = true;
                break;
            case "EGM":
                Debug.Log("RESET EGM");
                Mode_MoveTCP.transform.position = startPos;
                Mode_MoveTCP.SetActive(false);
                TCP.SetActive(false);
                Link0.SetActive(true);
                GO_EGMRough.GetComponent<CTRL_MoveTCP_Smoothing>().enabled = true;
                GO_EGMSettings.SetActive(false);
                GO_EGMResetConstraint.GetComponent<CTRL_MoveTCP_ConstraintSelect>().enabled = true;
                GO_EGMHideReset.GetComponent<CTRL_MoveTCP_setManipulationMode>().enabled = true;
                //Visu_Opaque.GetComponent<VISU_setOpaque>().enabled = true;

                break;
            case "HoloPath":
                Debug.Log("RESET HoloPath");
                Mode_HoloPath.transform.position = startPos;
                Mode_HoloPath_DestroyWaypoints.GetComponent<CTRL_holoPathDestroy>().enabled = true;
                Mode_HoloPath.SetActive(false);
                BK.SetActive(false);
                HP_MoveButton.SetActive(false);
                GO_PathRough.GetComponent<CTRL_MoveTCP_Smoothing>().enabled = true;
                GO_PathSettings.SetActive(false);
                GO_PathHideReset.GetComponent<CTRL_MoveTCP_setManipulationMode>().enabled = true; 
                break;      
        
        }

        

        //Mode_Change.GetComponent<CTRL_SetMode>().enabled = true;


        gameObject.GetComponent<ResetMode>().enabled = false; 
    }
}
