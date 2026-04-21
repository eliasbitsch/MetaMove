using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CTRL_SliderSetZero : MonoBehaviour
{
    public GameObject GO_data;
    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        for(int i = 0; i < 6; i++)
            GO_data.GetComponent<data>().VC_CTRL.angle[i] = 0;

        gameObject.GetComponent<CTRL_SliderSetZero>().enabled = false;
    }
}
