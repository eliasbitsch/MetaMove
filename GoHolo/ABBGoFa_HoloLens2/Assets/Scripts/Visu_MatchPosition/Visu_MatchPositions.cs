using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Visu_MatchPositions : MonoBehaviour
{
    public GameObject GO_GoFaImageTarget;
    public GameObject GO_GoFa; 
    void Start()
    {
        
    }

    void Update()
    {

        GO_GoFa.GetComponent<Transform>().position = GO_GoFaImageTarget.transform.position;
        GO_GoFa.GetComponent<Transform>().rotation = GO_GoFaImageTarget.transform.rotation;
        GO_GoFa.GetComponent<Transform>().localScale = GO_GoFaImageTarget.GetComponent<Transform>().localScale;

        Debug.Log("ImageTarget\tMatching Positions");

        gameObject.GetComponent<Visu_MatchPositions>().enabled = false;
        gameObject.SetActive(false);
    }
}
