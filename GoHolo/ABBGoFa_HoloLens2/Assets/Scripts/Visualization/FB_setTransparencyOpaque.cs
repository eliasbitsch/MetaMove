using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FB_setTransparencyOpaque : MonoBehaviour
{
    public int Link = 1;
    public Material matGoFaGrey;
    public Material matGoFaWhite;
    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        Debug.Log("Opaque Axis " + Link);
        if (Link == 4 || Link == 5)
            gameObject.GetComponent<MeshRenderer>().material = matGoFaWhite;
        else
            gameObject.GetComponent<MeshRenderer>().material = matGoFaGrey;

        gameObject.GetComponent<FB_setTransparencyOpaque>().enabled = false;
    }
}
