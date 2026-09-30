using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MetaMove.Demo.Kitting
{
    // Finished kits leave the cell: a copy of the full kit slides onto the roller conveyor
    // and rides through the wall breach into the hall, where it sinks out of sight. The
    // real kit on the table is emptied at the same moment, so the robot keeps going.
    public class KitConveyor : MonoBehaviour
    {
        public Transform kitModel;            // the kit shell + foam on the table
        public Transform start;               // conveyor start, next to the table
        public Transform end;                 // just past the wall breach
        public float slideTime = 0.6f;
        public float speed = 0.35f;           // m/s on the rollers

        public void Ship(List<Transform> slots)
        {
            if (kitModel == null || start == null || end == null) return;
            var copy = Instantiate(kitModel.gameObject, kitModel.parent);
            copy.name = "KitShipped";
            copy.transform.SetPositionAndRotation(kitModel.position, kitModel.rotation);
            foreach (var s in slots)
                foreach (Transform part in s)
                    if (part.gameObject.activeSelf)
                    {
                        var p = Instantiate(part.gameObject, copy.transform, true);
                        p.transform.SetPositionAndRotation(part.position, part.rotation);
                    }
            StartCoroutine(Ride(copy.transform));
        }

        IEnumerator Ride(Transform kit)
        {
            Vector3 p0 = kit.position;
            for (float t = 0f; t < 1f; t += Time.deltaTime / slideTime)
            {
                kit.position = Vector3.Lerp(p0, start.position, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }
            while ((kit.position - end.position).sqrMagnitude > 1e-4f)
            {
                kit.position = Vector3.MoveTowards(kit.position, end.position, speed * Time.deltaTime);
                yield return null;
            }
            // past the breach: sink away instead of popping out
            Vector3 e = kit.position;
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.5f)
            {
                kit.position = e + Vector3.down * 0.25f * t;
                kit.localScale = Vector3.one * (1f - t);
                yield return null;
            }
            Destroy(kit.gameObject);
        }
    }
}
