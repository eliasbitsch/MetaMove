using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class CTRL_MoveTCP_ConstraintSelect : MonoBehaviour
{
    public GameObject GO_constraint;
    public string select;

    public GameObject GO_TextMeshProX;
    public GameObject GO_TextMeshProY;
    public GameObject GO_TextMeshProZ;

    private Component constraintManager;
    void Start()
    {
        constraintManager = GO_constraint.GetComponent<CTRL_MoveTCP_Constraint>();
    }

    // Update is called once per frame
    void Update()
    {

        
        switch (select)
        { 
            case "x":
                if (GO_constraint.GetComponent<CTRL_MoveTCP_Constraint>().lockX)
                {
                    GO_constraint.GetComponent<CTRL_MoveTCP_Constraint>().lockX = false;
                    GO_TextMeshProX.GetComponent<TextMeshPro>().color = Color.white;
                }
                else 
                { 
                    GO_constraint.GetComponent<CTRL_MoveTCP_Constraint>().lockX = true;
                    GO_TextMeshProX.GetComponent<TextMeshPro>().color = Color.red;
                }
                break;

            case "y":
                if (GO_constraint.GetComponent<CTRL_MoveTCP_Constraint>().lockY)
                {
                    GO_constraint.GetComponent<CTRL_MoveTCP_Constraint>().lockY = false;
                    GO_TextMeshProY.GetComponent<TextMeshPro>().color = Color.white;
                }
                else
                {
                    GO_constraint.GetComponent<CTRL_MoveTCP_Constraint>().lockY = true;
                    GO_TextMeshProY.GetComponent<TextMeshPro>().color = Color.red;
                }
                break;

            case "z":
                if (GO_constraint.GetComponent<CTRL_MoveTCP_Constraint>().lockZ)
                {
                    GO_constraint.GetComponent<CTRL_MoveTCP_Constraint>().lockZ = false;
                    GO_TextMeshProZ.GetComponent<TextMeshPro>().color = Color.white;
                }
                else
                {
                    GO_constraint.GetComponent<CTRL_MoveTCP_Constraint>().lockZ = true;
                    GO_TextMeshProZ.GetComponent<TextMeshPro>().color = Color.red;
                }
                break;

            case "reset":
                GO_constraint.GetComponent<CTRL_MoveTCP_Constraint>().lockX = false;
                GO_constraint.GetComponent<CTRL_MoveTCP_Constraint>().lockY = false;
                GO_constraint.GetComponent<CTRL_MoveTCP_Constraint>().lockZ = false;

                GO_TextMeshProX.GetComponent<TextMeshPro>().color = Color.white;
                GO_TextMeshProY.GetComponent<TextMeshPro>().color = Color.white;
                GO_TextMeshProZ.GetComponent<TextMeshPro>().color = Color.white;

                break;    
        }
        gameObject.GetComponent<CTRL_MoveTCP_ConstraintSelect>().enabled = false; 
    }
}
