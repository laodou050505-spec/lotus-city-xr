using UnityEngine;

namespace PicoTowerDefense
{
    public sealed class BillboardText : MonoBehaviour
    {
        public Transform Target { get; set; }

        private void LateUpdate()
        {
            FaceTarget();
        }

        public void FaceTarget()
        {
            if (Target == null)
            {
                return;
            }

            Vector3 awayFromViewer = transform.position - Target.position;
            if (awayFromViewer.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(awayFromViewer.normalized, Vector3.up);
            }
        }
    }
}
