// source
// https://docs.unity3d.com/Manual/InstantiatingPrefabs.html


using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CTRL_createWaypoint : MonoBehaviour
{

    public GameObject GO_WPprefab;
    public GameObject GO_data; 
    private GameObject GO_WP;
    public GameObject GO_GoFa;

    void Start()
    {
        
    }

    void Update()
    {
        GO_WP = Instantiate(GO_WPprefab, new Vector3(0, (float)0.68996, (float)-0.57368), Quaternion.identity);
        GO_WP.transform.localScale = GO_GoFa.transform.localScale;
        GO_WP.transform.SetParent(GO_GoFa.transform);
        GO_WP.transform.localPosition = new Vector3(0, (float)0.68996, (float)-0.57368);
        GO_WP.transform.localRotation = Quaternion.Euler(new Vector3(270, 0, 0)); // Quaternion.identity;

        GO_WP.gameObject.name = "WP_" + GO_data.GetComponent<data>().waypointNumber;
        GO_data.GetComponent<data>().waypointNumber++;
        //Debug.Log("waypoint added: " + GO_WP.gameObject.name);
        GO_WP.SetActive(true);
        gameObject.GetComponent<CTRL_createWaypoint>().enabled = false;
    }
}
