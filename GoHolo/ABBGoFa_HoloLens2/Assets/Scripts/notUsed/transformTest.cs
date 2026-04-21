using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class transformTest : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        //transform.Translate(3 * Time.deltaTime,0,0);
        transform.Rotate(0,1,0);
    }
}
