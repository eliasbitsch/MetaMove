using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FB_GaugeToggle : MonoBehaviour
{
    public GameObject Gauge1;
    public GameObject Gauge2;
    public GameObject Gauge3;
    public GameObject Gauge4;
    public GameObject Gauge5;
    public GameObject Gauge6;
    public GameObject GO_dashboard;
    bool visible = false; 
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (!visible)
        {
            GO_dashboard.SetActive(true);
            GO_dashboard.transform.localPosition = new Vector3(0.35f, 0.5f, 0);
            Gauge1.SetActive(true);
            Gauge2.SetActive(true);
            Gauge3.SetActive(true);
            Gauge4.SetActive(true);
            Gauge5.SetActive(true);
            Gauge6.SetActive(true);
            visible = true; 
        }
        else
        {
            GO_dashboard.SetActive(false);
            Gauge1.SetActive(false);
            Gauge2.SetActive(false);
            Gauge3.SetActive(false);
            Gauge4.SetActive(false);
            Gauge5.SetActive(false);
            Gauge6.SetActive(false);
            visible = false; 
        }

        gameObject.GetComponent<FB_GaugeToggle>().enabled = false;
    }
}
