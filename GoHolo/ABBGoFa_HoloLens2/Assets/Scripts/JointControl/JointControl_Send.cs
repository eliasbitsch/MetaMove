using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class JointControl_Send : MonoBehaviour
{
    public GameObject GO_data;
    public GameObject GO_Menu; 
    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        
        GO_data.GetComponent<data>().CMD.SendJoint = true;
        Debug.Log("sending");
        //GO_Menu.SetActive(false);
        gameObject.GetComponent<JointControl_Send>().enabled = false;

        Debug.Log("\nMODE: " + GO_data.GetComponent<data>().MODE);

    }
}
