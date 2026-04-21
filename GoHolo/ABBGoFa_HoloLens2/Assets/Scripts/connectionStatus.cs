using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class connectionStatus : MonoBehaviour
{

    new private MeshRenderer renderer; 

    void Start()
    {

        GameObject.Find("TCPIP").GetComponent<TCPIPclient>();
        renderer = gameObject.GetComponent<MeshRenderer>();
 
    }

    void Update()
    {
        Color colorRed = new Color(1f, 0.2f, 0f, 1f);
        Color colorGreen = new Color(0.8f, 1f, 0f, 1f);

        transform.Rotate(1, 1, 1);

        if (TCPIPclient.serverConnected)
            renderer.material.color = colorGreen;
        else 
            renderer.material.color = colorRed;

       
    }
}
