using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class clearFlags : MonoBehaviour
{

    private Camera cam; 
    // Start is called before the first frame update
    void Start()
    {
        cam = gameObject.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
