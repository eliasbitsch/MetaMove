using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CTRL_holoPathCommandMove : MonoBehaviour
{
    public GameObject GO_data;
    public GameObject GO_Menu;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {

        GO_data.GetComponent<data>().CMD.PathMove = true;
        //GO_Menu.SetActive(false);
        gameObject.GetComponent<CTRL_holoPathCommandMove>().enabled = false;

        Debug.Log("\nMODE: " + GO_data.GetComponent<data>().MODE + "HP: MOVE");

    }
}
