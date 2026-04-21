using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FB_setTransparentToggle : MonoBehaviour
{
    /*
    public Material matGoFaGrey;
    public Material matGoFaGrey_Fade;
    public Material matGoFaWhite;
    public Material matGoFaWhite_Fade;

    
    public int Link = 1;
    */

    public GameObject GO_data;

    public GameObject GO_setTransparent;
    public GameObject GO_setOpaque; 



    float transparencyVal = 0.6f;

    private bool transparent = false; 

    void Start()
    {
        //gameObject.GetComponent<FB_setTransparentToggle>().enabled = false;
    }

    void Update()
    {
        
        if (!transparent)
        {
            Debug.Log("set transparent");
            GO_setTransparent.GetComponent<VISU_setTransparent>().enabled = true; 

            /*
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
            */
            transparent = true; 

        }
        else
        {
            GO_setOpaque.GetComponent<VISU_setOpaque>().enabled = true;
            /*
            Debug.Log("set opaque");
            if (Link == 4 || Link == 5)
                gameObject.GetComponent<MeshRenderer>().material = matGoFaWhite;
            else
                gameObject.GetComponent<MeshRenderer>().material = matGoFaGrey;
            */
            transparent = false; 
        }

        gameObject.GetComponent<FB_setTransparentToggle>().enabled = false; 


    }


}
