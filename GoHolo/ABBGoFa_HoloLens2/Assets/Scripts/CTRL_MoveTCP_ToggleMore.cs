using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CTRL_MoveTCP_ToggleMore : MonoBehaviour
{
    public GameObject GO_moreSettings;
    private bool enab; 
    void Start()
    {
        enab = false; 
    }

    // Update is called once per frame
    void Update()
    {
        if (enab) 
        { 
            GO_moreSettings.SetActive(false);
            enab = false; 
        }
        else
        {
            GO_moreSettings.SetActive(true);
            enab = true;
        }

        gameObject.GetComponent<CTRL_MoveTCP_ToggleMore>().enabled = false; 
    }
}
