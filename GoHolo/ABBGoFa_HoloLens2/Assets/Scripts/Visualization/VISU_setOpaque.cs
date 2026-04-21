using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VISU_setOpaque : MonoBehaviour
{
    public Material matGoFaGrey;
    public Material matGoFaWhite;

    public GameObject Link00;
    public GameObject Link01;
    public GameObject Link02;
    public GameObject Link03;
    public GameObject Link04;
    public GameObject Link05;
    public GameObject Link06;

    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        Link00.SetActive(true);
        Link01.SetActive(true);
        Link02.SetActive(true);
        Link03.SetActive(true);
        Link04.SetActive(true);
        Link05.SetActive(true);
        Link06.SetActive(true);

        Debug.Log("VISU\t setting links opaque");

        Link00.GetComponent<MeshRenderer>().material = matGoFaGrey;
        Link01.GetComponent<MeshRenderer>().material = matGoFaGrey;
        Link02.GetComponent<MeshRenderer>().material = matGoFaGrey;
        Link03.GetComponent<MeshRenderer>().material = matGoFaGrey;
        Link06.GetComponent<MeshRenderer>().material = matGoFaGrey;

        Link04.GetComponent<MeshRenderer>().material = matGoFaWhite;
        Link05.GetComponent<MeshRenderer>().material = matGoFaWhite;

        gameObject.GetComponent<VISU_setOpaque>().enabled = false;
    }
}
