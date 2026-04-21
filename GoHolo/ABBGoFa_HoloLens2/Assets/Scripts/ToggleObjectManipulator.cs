using System.Collections;
using System.Collections.Generic;
using Microsoft.MixedReality.Toolkit.UI;
using Microsoft.MixedReality.Toolkit.UI.BoundsControl;
using UnityEngine;

public class ToggleObjectManipulator : MonoBehaviour
{

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {

        gameObject.GetComponent<ToggleObjectManipulator>().enabled = false;

        if (gameObject.GetComponent<ObjectManipulator>().enabled) 
        { 
            gameObject.GetComponent<ObjectManipulator>().enabled = false;
            gameObject.GetComponent<BoundsControl>().enabled = false;
            gameObject.GetComponent<BoxCollider>().enabled = false;
        }
        else
        {
            gameObject.GetComponent<ObjectManipulator>().enabled = true;
            gameObject.GetComponent<BoxCollider>().enabled = true;
            gameObject.GetComponent<BoundsControl>().enabled = true;
        }
                
            
    }
}
