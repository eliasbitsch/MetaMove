using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ToggleTorque : MonoBehaviour
{
    public GameObject GO_dashboard; 
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {

        gameObject.GetComponent<ToggleTorque>().enabled = false;
        if (gameObject.GetComponent<data>().SETUP.showColorTorque) 
        { 
            gameObject.GetComponent<data>().SETUP.showColorTorque = false;
            GO_dashboard.SetActive(false);
        }
        else
        {
            gameObject.GetComponent<data>().SETUP.showColorTorque = true;
            GO_dashboard.transform.localPosition = new Vector3(-0.35f, 0.5f,0);
            GO_dashboard.SetActive(true);
        }
            

    }
}