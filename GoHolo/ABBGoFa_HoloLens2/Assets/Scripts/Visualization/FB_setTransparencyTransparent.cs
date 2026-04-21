using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FB_setTransparencyTransparent : MonoBehaviour
{
    public int Link = 1;

    public Material matGoFaWhite_Fade;
    public Material matGoFaGrey_Fade;
    float transparencyVal = 0.6f;

    // Start is called before the first frame update
    void Start()
    {
        //gameObject.GetComponent<FB_setTransparencyTransparent>().enabled = false;
    }

    // Update is called once per frame
    void Update()
    {
        if (Link == 4 || Link == 5)
        {
            // get color of material and change alpha
            var col = matGoFaWhite_Fade.color;
            col.a = transparencyVal;

            // set material with redering mode "fade" 

            gameObject.GetComponent<MeshRenderer>().material = matGoFaWhite_Fade;

            // set color of new material
            gameObject.GetComponent<MeshRenderer>().material.color = col;
        }
        else
        {

            // get color of material and change alpha
            var col = matGoFaGrey_Fade.color;
            col.a = transparencyVal;

            // set material with redering mode "fade" 

            gameObject.GetComponent<MeshRenderer>().material = matGoFaGrey_Fade;

            // set color of new material
            gameObject.GetComponent<MeshRenderer>().material.color = col;
        }
        gameObject.GetComponent<FB_setTransparencyTransparent>().enabled = false;
    }
}
