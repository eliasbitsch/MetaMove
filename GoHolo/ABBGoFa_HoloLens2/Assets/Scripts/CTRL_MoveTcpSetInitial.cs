using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CTRL_MoveTcpSetInitial : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        //gameObject.transform.localPosition = new Vector3(0, (float)0.48564, (float)-0.3236); // YuMí

        //gameObject.transform.localPosition = new Vector3(0, (float)0.68996, (float)-0.57368); // GoFa
        //gameObject.transform.localRotation = Quaternion.Euler(new Vector3(270, 0, 0));
        gameObject.transform.localPosition = new Vector3(0, (float)0.77153, (float)-0.59003); // GoFa
        gameObject.transform.localRotation = Quaternion.Euler(new Vector3(210, 0, 0));
        gameObject.GetComponent<CTRL_MoveTcpSetInitial>().enabled = false;  

    }
}
