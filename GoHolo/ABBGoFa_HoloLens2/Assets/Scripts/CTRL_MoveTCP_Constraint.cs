using System.Collections;
using System.Collections.Generic;
using Microsoft.MixedReality.Toolkit.UI;
using UnityEngine;

public class CTRL_MoveTCP_Constraint : MonoBehaviour
{
    //public GameObject GO_TCP;  
    public bool lockX;
    public bool lockY;
    public bool lockZ;

    private bool prevX;
    private bool prevY;
    private bool prevZ;

    private bool changes;

    void Start()
    {
        lockX = false;
        lockY = false;
        lockZ = false;
    }


    void Update()
    {
        if (lockX != prevX || lockY != prevY || lockZ != prevZ)
        {
            prevX = lockX;
            prevY = lockY;
            prevZ = lockZ;

            gameObject.GetComponent<MoveAxisConstraint>().ConstraintOnMovement = (Microsoft.MixedReality.Toolkit.Utilities.AxisFlags)RigidbodyConstraints.None;

            changes = true; 

        }


        if (lockZ && changes) 
        { 
            gameObject.GetComponent<MoveAxisConstraint>().ConstraintOnMovement |= (Microsoft.MixedReality.Toolkit.Utilities.AxisFlags)RigidbodyConstraints.FreezePositionX;
            Debug.Log("lock Z");
        }
        if (lockY && changes)
        {
            gameObject.GetComponent<MoveAxisConstraint>().ConstraintOnMovement |=  (Microsoft.MixedReality.Toolkit.Utilities.AxisFlags)1; //RigidbodyConstraints.FreezePositionZ; jakobnode: FreezePositionZ gives the wrong flag
            Debug.Log("Lock Y");
        }
        if (lockX && changes)
        {
            gameObject.GetComponent<MoveAxisConstraint>().ConstraintOnMovement |= (Microsoft.MixedReality.Toolkit.Utilities.AxisFlags)RigidbodyConstraints.FreezePositionY;
            Debug.Log("Lock X");
        }

        changes = false; 
    }
}
