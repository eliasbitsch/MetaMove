using UnityEngine;

namespace MetaMove.Demo.Kitting
{
    // Turns a part about an axis given in its parent's space (the exhaust fan rotor about the
    // fan axis). Spins up from rest so it does not start at full speed when the cell appears.
    public class Spin : MonoBehaviour
    {
        public Vector3 parentAxis = Vector3.forward;
        public float degreesPerSecond = 420f;
        public float spinUp = 2.5f;

        Vector3 _axis;
        float _speed;

        void Start() => _axis = Quaternion.Inverse(transform.localRotation) * parentAxis.normalized;

        void Update()
        {
            _speed = Mathf.MoveTowards(_speed, degreesPerSecond, degreesPerSecond / Mathf.Max(0.01f, spinUp) * Time.deltaTime);
            transform.Rotate(_axis, _speed * Time.deltaTime, Space.Self);
        }
    }
}
