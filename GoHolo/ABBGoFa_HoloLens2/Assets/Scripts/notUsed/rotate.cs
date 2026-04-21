using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class rotate : MonoBehaviour
{
    public GameObject Link2; 
    //private GameObject cube; 
    // Start is called before the first frame update
    void Start()
    {
      //cube = GameObject.Find("superCube");
        //Link2.transform.Rotate(0,3,0);
    }

    // Update is called once per frame
    void Update()
    {
      //cube.transform.Rotate(0,3,0);
       Link2.transform.Rotate(0,0,1);
    }
}
