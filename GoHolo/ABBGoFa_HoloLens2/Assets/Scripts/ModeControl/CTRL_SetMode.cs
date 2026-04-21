using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CTRL_SetMode : MonoBehaviour
{
    public GameObject GO_data;
    public int MODE; 

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {

        if (ValidateMode(MODE))
        {
            GO_data.GetComponent<data>().MODE = MODE;
            GO_data.GetComponent<data>().MODEsend = true;
            Debug.Log("\nMODE: " + GO_data.GetComponent<data>().MODE + " called by: " + gameObject.name);
        }
        else
            Debug.Log("\nMODE not valid");

        gameObject.GetComponent<CTRL_SetMode>().enabled = false; 
    }

    bool ValidateMode(int m)
    {
        bool valid = false;

        valid = valid || (m == GO_data.GetComponent<data>().CMD_DONOTHING);
        valid = valid || (m == GO_data.GetComponent<data>().CMD_EGMPOSE);
        valid = valid || (m == GO_data.GetComponent<data>().CMD_HOLOPATH);
        valid = valid || (m == GO_data.GetComponent<data>().CMD_JOINTCONTROL);
        valid = valid || (m == GO_data.GetComponent<data>().CMD_RANDOMPATH);
        valid = valid || (m == GO_data.GetComponent<data>().CMD_WIZARD);

        return valid; 
    }
}
