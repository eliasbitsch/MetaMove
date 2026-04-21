using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VISU_setTransparent : MonoBehaviour
{

    public Material matGoFaWhite_Fade;
    public Material matGoFaGrey_Fade;
    //float transparencyVal = 0.6f;

    public GameObject Link00;
    public GameObject Link01;
    public GameObject Link02;
    public GameObject Link03;
    public GameObject Link04;
    public GameObject Link05;
    public GameObject Link06;

    public GameObject Motor01;
    public GameObject Motor02;
    public GameObject Motor03;
    public GameObject Motor04;
    public GameObject Motor05;
    public GameObject Motor06;

    public GameObject GO_data;

    public float transparencyVal = 0.6f;

    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

        Debug.Log("VISU\t setting links transparent");

        if (transparencyVal == 0)
        {
            Link04.SetActive(false);
            Link05.SetActive(false);
            Link06.SetActive(false);
            Debug.Log("VISU\t setting links 4-6 inactive");
        }
        else 
        {
            Link04.SetActive(true);
            Link05.SetActive(true);
            Link06.SetActive(true);
            Debug.Log("VISU\t setting links 4-6 active");
        }
            // links in gray color
            {
            // get color of material and change alpha
            var col = matGoFaGrey_Fade.color;
            col.a = transparencyVal;

            // set material with redering mode "fade" 
            Link00.GetComponent<MeshRenderer>().material = matGoFaGrey_Fade;
            Link01.GetComponent<MeshRenderer>().material = matGoFaGrey_Fade;
            Link02.GetComponent<MeshRenderer>().material = matGoFaGrey_Fade;
            Link03.GetComponent<MeshRenderer>().material = matGoFaGrey_Fade;
            Link06.GetComponent<MeshRenderer>().material = matGoFaGrey_Fade;

            // set color of new material
            Link00.GetComponent<MeshRenderer>().material.color = col;
            Link01.GetComponent<MeshRenderer>().material.color = col;
            Link02.GetComponent<MeshRenderer>().material.color = col;
            Link03.GetComponent<MeshRenderer>().material.color = col;
            Link06.GetComponent<MeshRenderer>().material.color = col;
        }
        // links in white color
        {
            // get color of material and change alpha
            var col = matGoFaWhite_Fade.color;
            col.a = transparencyVal;

            // set material with redering mode "fade" 

            Link04.GetComponent<MeshRenderer>().material = matGoFaWhite_Fade;
            Link05.GetComponent<MeshRenderer>().material = matGoFaWhite_Fade;

            // set color of new material
            Link04.GetComponent<MeshRenderer>().material.color = col;
            Link05.GetComponent<MeshRenderer>().material.color = col;

        }

        if (GO_data.GetComponent<data>().SETUP.showColorTorque)
        {
            Motor01.SetActive(true);
            Motor02.SetActive(true);
            Motor03.SetActive(true);
            Motor04.SetActive(true);
            Motor05.SetActive(true);
            Motor06.SetActive(true);
        }
        else
        {
            Motor01.SetActive(false);
            Motor02.SetActive(false);
            Motor03.SetActive(false);
            Motor04.SetActive(false);
            Motor05.SetActive(false);
            Motor06.SetActive(false);
        }


        gameObject.GetComponent<VISU_setTransparent>().enabled = false;
    }

}
