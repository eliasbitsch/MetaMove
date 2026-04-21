using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HideCapsule : MonoBehaviour
{
    public GameObject GO_data;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (GO_data.GetComponent<data>().MODE == GO_data.GetComponent<data>().CMD_JOINTCONTROL)
            gameObject.SetActive(false);
        else
            gameObject.SetActive(true);// = true; 
    }
}
