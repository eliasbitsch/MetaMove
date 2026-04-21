using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class VISU_angleValue : MonoBehaviour
{
    // Start is called before the first frame update
    private GameObject GO_pivot;
    private GameObject GO_value;
    private GameObject GO_parent;
    private Text value;
    public GameObject GO_data;
    public int Axis; 

    void Start()
    {
        GO_pivot = gameObject.transform.Find("Pivot").gameObject;
        GO_parent = GO_pivot.transform.Find("ContentParent").gameObject; 
        GO_value = GO_parent.transform.Find("value").gameObject;
        value = GO_value.transform.GetChild(0).GetComponent<Text>(); 
    }

    // Update is called once per frame
    void Update()
    {
        float angle = GO_data.GetComponent<data>().VC.angle[Axis - 1];
        value.text = (Math.Round(angle*100)/100).ToString() + "°";
    }
}
