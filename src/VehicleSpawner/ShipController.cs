using UnityEngine;

namespace VehicleSpawner
{
    /// <summary>Simple hover-flight for any Rigidbody. Controls (when this ship is the active one):
    /// arrows = forward/back/strafe, RightShift = up, RightCtrl = down, comma/period = yaw.
    /// (Arrow keys avoid clashing with the character's WASD.)</summary>
    internal class ShipController : MonoBehaviour
    {
        public bool Active;
        private Rigidbody _rb;

        private void Awake() { _rb = GetComponent<Rigidbody>(); }

        public void SetActive(bool on)
        {
            Active = on;
            if (_rb == null) _rb = GetComponent<Rigidbody>();
            if (_rb == null) return;
            _rb.useGravity = !on;
            _rb.drag = on ? 1.5f : _rb.drag;
            _rb.angularDrag = on ? 4f : _rb.angularDrag;
            _rb.constraints = on ? RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ : RigidbodyConstraints.None;
        }

        private void FixedUpdate()
        {
            if (!Active || _rb == null) return;
            float z = Axis(KeyCode.UpArrow, KeyCode.DownArrow);
            float x = Axis(KeyCode.RightArrow, KeyCode.LeftArrow);
            float y = Axis(KeyCode.RightShift, KeyCode.RightControl);
            float yaw = Axis(KeyCode.Period, KeyCode.Comma);

            var target = (transform.forward * z + transform.right * x + Vector3.up * y).normalized * Plugin.ShipSpeed.Value;
            _rb.velocity = Vector3.Lerp(_rb.velocity, target, 2f * Time.fixedDeltaTime);
            _rb.angularVelocity = Vector3.up * (yaw * 1.2f);
        }

        private static float Axis(KeyCode pos, KeyCode neg) => (Input.GetKey(pos) ? 1f : 0f) - (Input.GetKey(neg) ? 1f : 0f);
    }
}
