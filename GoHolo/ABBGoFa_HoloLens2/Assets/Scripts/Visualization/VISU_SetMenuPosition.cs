using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VISU_SetMenuPosition : MonoBehaviour
{
    public GameObject GO_Menu;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        GO_Menu.transform.position = new Vector3(0, 0, 2);
        Debug.Log("Setting Menu Posiion");
        GO_Menu.GetComponent<VISU_SetMenuPosition>().enabled = false; 
    }
}
