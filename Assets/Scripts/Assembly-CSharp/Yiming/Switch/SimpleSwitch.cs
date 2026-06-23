using UnityEngine;

namespace Yiming.Switch
{
    public class SimpleSwitch : Switch, IReceiverDown
    {
        public enum Transition
        {
            Transform = 0,
            Material = 1
        }

        public Transition transition;

        public Transform button;

        public Renderer rend;

        public Material onMaterial;

        public Material offMaterial;

        public AudioClip onSound;

        public AudioClip offSound;

        public AudioSource source;

        protected override void OnToggleChanged()
        {
            if (transition == Transition.Material)
            {
                rend.material = IsOn ? onMaterial : offMaterial;
            } 
            else if (transition == Transition.Transform)
            {
                transform.localRotation *= Quaternion.Euler(180f, 0f, 0f);
            }
        }

        public void Hit()
        {
            IsOn = !IsOn;
            var src = source ?? GetComponent<AudioSource>();
            var clip = IsOn ? onSound : offSound;
            src.PlayOneShot(clip);
        }
    }
}
