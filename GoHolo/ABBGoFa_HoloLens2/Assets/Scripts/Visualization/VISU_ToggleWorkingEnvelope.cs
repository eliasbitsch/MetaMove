using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VISU_ToggleWorkingEnvelope : MonoBehaviour
{
    bool active; 
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (active)
        {
            gameObject.transform.GetChild(0).gameObject.SetActive(false);
            active = false;
        }

        else
        {
            gameObject.transform.GetChild(0).gameObject.SetActive(true);
            active = true;
        }

        gameObject.GetComponent<VISU_ToggleWorkingEnvelope>().enabled = false; 
    }
}
