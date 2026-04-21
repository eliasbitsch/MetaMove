using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CTRL_holoPathValidationColor : MonoBehaviour
{
    public GameObject GO_data;
    public GameObject GO_getId;
    public Material matWhite;
    public Material matRed;
    public GameObject GO_MoveButton; 


    int GO_id;

    void Start()
    {
        // gameObject.GetComponent<MeshRenderer>().
        GO_id = GO_getId.GetComponent<CTRL_holoPathUpdatePose>().id;
    }

    // Update is called once per frame
    void Update()
    {
        Debug.Log("my id is " + GO_id + " and my validation is " + GO_data.GetComponent<data>().VC.validation[GO_id]);
        if (GO_data.GetComponent<data>().VC.validation[GO_id])
            gameObject.GetComponent<MeshRenderer>().material = matWhite;
        else {
            gameObject.GetComponent<MeshRenderer>().material = matRed;
            GO_MoveButton.SetActive(false);
        }

    }
}
