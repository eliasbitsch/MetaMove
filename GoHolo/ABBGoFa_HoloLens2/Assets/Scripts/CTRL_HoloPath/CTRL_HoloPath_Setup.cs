using System.Collections;
using System.Collections.Generic;
using Microsoft.MixedReality.Toolkit.UI;
using Microsoft.MixedReality.Toolkit.UI.BoundsControl;
using UnityEngine;

public class CTRL_HoloPath_Setup : MonoBehaviour
{
    public GameObject GO_pose;
    public GameObject GO_visibilityChild;
    public GameObject GO_visibilityParent; 

    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        gameObject.GetComponent<BoundsControl>().RotateLerpTime = GO_pose.GetComponent<BoundsControl>().RotateLerpTime;
        gameObject.GetComponent<ObjectManipulator>().RotateLerpTime = GO_pose.GetComponent<ObjectManipulator>().RotateLerpTime;
        gameObject.GetComponent<ObjectManipulator>().MoveLerpTime = GO_pose.GetComponent<ObjectManipulator>().MoveLerpTime;

        if (GO_visibilityParent.active)
            GO_visibilityChild.SetActive(true);
        else
            GO_visibilityChild.SetActive(false); 
    }
}
