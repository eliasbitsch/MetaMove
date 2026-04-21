using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CTRL_holoPathSend : MonoBehaviour
{
    public GameObject GO_data;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        GO_data.GetComponent<data>().CMD.PathSend = true; 
        gameObject.GetComponent<CTRL_holoPathSend>().enabled = false;
        Debug.Log("CMD.PathSend = true");
    }
}
