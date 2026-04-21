using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CTRL_holoPathDestroy : MonoBehaviour
{

    private GameObject Pose;
    public GameObject GO_data;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {

        for (int i = 0; i < GO_data.GetComponent<data>().waypointNumber; i++)
        {
            Pose = GameObject.Find("/ABB_GoFa/WP_" + i.ToString());
            Debug.Log("Destroy: WP_" + i.ToString());
            Destroy(Pose);
        }

        GO_data.GetComponent<data>().waypointNumber = 0;

        gameObject.GetComponent<CTRL_holoPathDestroy>().enabled = false; 
    }
}
