using System.Collections;
using System.Collections.Generic;
using TMPro;
using System;
using UnityEngine;
using UnityEngine.UI;

public class FB_pose : MonoBehaviour
{
    public GameObject GO_data;

    private GameObject GO_PosX;
    private Text GO_PosX_Text;

    private GameObject GO_PosY;
    private Text GO_PosY_Text;

    private GameObject GO_PosZ;
    private Text GO_PosZ_Text;

    private GameObject GO_OriX;
    private Text GO_OriX_Text;

    private GameObject GO_OriY;
    private Text GO_OriY_Text;

    private GameObject GO_OriZ;
    private Text GO_OriZ_Text;

    private GameObject GO_ID;
    private Text GO_ID_Text;

    void Start()
    {

        GO_PosX = gameObject.transform.Find("X").gameObject;
        GO_PosY = gameObject.transform.Find("Y").gameObject;
        GO_PosZ = gameObject.transform.Find("Z").gameObject;

        GO_OriX = gameObject.transform.Find("RX").gameObject;
        GO_OriY = gameObject.transform.Find("RY").gameObject;
        GO_OriZ = gameObject.transform.Find("RZ").gameObject;

        GO_PosX_Text = GO_PosX.transform.GetChild(0).GetComponent<Text>();
        GO_PosY_Text = GO_PosY.transform.GetChild(0).GetComponent<Text>();
        GO_PosZ_Text = GO_PosZ.transform.GetChild(0).GetComponent<Text>();

        GO_OriX_Text = GO_OriX.transform.GetChild(0).GetComponent<Text>();
        GO_OriY_Text = GO_OriY.transform.GetChild(0).GetComponent<Text>();
        GO_OriZ_Text = GO_OriZ.transform.GetChild(0).GetComponent<Text>();

    }

    // Update is called once per frame
    void Update()
    {
        double value;

        value = (Math.Round(GO_data.GetComponent<data>().VC.pose[0] * 100) / 100);
        GO_PosX_Text.text = value.ToString() + "mm";

        value = (Math.Round(GO_data.GetComponent<data>().VC.pose[1] * 100) / 100);
        GO_PosY_Text.text = value.ToString() + "mm";

        value = (Math.Round(GO_data.GetComponent<data>().VC.pose[2] * 100) / 100);
        GO_PosZ_Text.text = value.ToString() + "mm";

        value = (Math.Round(GO_data.GetComponent<data>().VC.pose[3] * 100) / 100);
        GO_OriX_Text.text = value.ToString() + "°";

        value = (Math.Round(GO_data.GetComponent<data>().VC.pose[4] * 100) / 100);
        GO_OriY_Text.text = value.ToString() + "°";

        value = (Math.Round(GO_data.GetComponent<data>().VC.pose[5] * 100) / 100);
        GO_OriZ_Text.text = value.ToString() + "°";

    }

}
