using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ToqqleFB_Pose : MonoBehaviour
{
    public GameObject GO_InformationPose;
    private bool visible = false; 

    void Start()
    {
        
    }


    void Update()
    {

        gameObject.GetComponent<ToqqleFB_Pose>().enabled = false;
        if (visible)
        {
            GO_InformationPose.SetActive(false);
            visible = false;
        }
        else
        {
            GO_InformationPose.SetActive(true);
            visible = true;
        }

    }

}
