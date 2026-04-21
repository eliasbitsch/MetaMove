using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VISU_facingCamera : MonoBehaviour
{
    public Camera m_Camera;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        transform.LookAt(transform.position + m_Camera.transform.rotation * Vector3.forward);//,m_Camera.transform.rotation * Vector3.up); // second part for "roll" of the head
    }
}
