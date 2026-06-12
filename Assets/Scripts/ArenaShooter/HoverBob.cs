using UnityEngine;

namespace ArenaShooter
{
    /// <summary>
    /// Slow vertical hover oscillation for floating stadium furniture — paired
    /// with the animated jet plumes it sells the structures as genuinely riding
    /// their own thrust rather than being bolted to the sky.
    /// </summary>
    public sealed class HoverBob : MonoBehaviour
    {
        public float Amplitude = 0.4f;
        public float CyclesPerSecond = 0.08f;
        public float Phase;

        private Vector3 restPosition;

        private void Awake()
        {
            restPosition = transform.localPosition;
        }

        private void Update()
        {
            var wave = Mathf.Sin((Time.time * CyclesPerSecond + Phase) * Mathf.PI * 2f);
            transform.localPosition = restPosition + Vector3.up * (wave * Amplitude);
        }
    }
}
