using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.PlayerLoop;

namespace PC.Component
{
	public class Supply : Hardware
	{
		[SerializeField]
		private GameObject spark;

		[SerializeField]
		private bool sound;

        [Header("=== Burn Settings ===")]
        [SerializeField] private Color burnedColor = Color.black;
        [SerializeField] private GameObject burnParticle;
        [SerializeField] private float burnTime = 3f;

        [Header("=== Explosion Settings ===")]
        [SerializeField] private bool canExplode = false;
        [SerializeField] private GameObject explodeEffect;

        [Header("Explosion Physics")]
        [SerializeField] private float explosionForce = 800f;
        [SerializeField] private float explosionRadius = 3f;
        [SerializeField] private float upwardsModifier = 0.5f;

        private bool burning = false;

        private AudioSource source;

		private FanSpin fan;

		protected override void Start()
        {
			source = GetComponent<AudioSource>();
			fan = GetComponent<FanSpin>();
			PowerChanged += UpdatePower;
        }

		private void UpdatePower(bool on)
        {
            if (on)
			{
				if (!sound || source == null)
        			return;
                if (!source.isPlaying)
                {
					StartCoroutine(nameof(FanSound));
                }
            }
        }

		private IEnumerator FanSound()
		{
			if (source == null || fan == null)
				yield break;

			source.Play();

			while (true)
			{
				if (fan == null || source == null)
					yield break;

				if (fan.Velocity <= 0f)
				{
					source.volume = 0f;
					source.Stop();
					yield break;
				}

				float value = fan.Velocity / fan.MaxSpeed;
				source.volume = value;
				source.pitch = value;

				yield return null;
			}
		}

        public void Overload()
        {
            if (Damaged || burning)
                return;

            burning = true;

            base.Damage(); // ставит Broken статус

            if (spark != null)
            {
                var obj = Instantiate(spark, transform.position, transform.rotation, transform);
                Destroy(obj, 4f);
            }

            StartCoroutine(BurnRoutine());
        }

        public override void Damage()
        {
            base.Damage();
            StartCoroutine(RenderBurn());
        }

        private IEnumerator BurnRoutine()
        {
            // просто останавливаем звук
            if (source != null)
                source.Stop();

            GameObject burnObj = null;

            if (burnParticle != null)
                burnObj = Instantiate(burnParticle, transform);

            yield return new WaitForSeconds(burnTime);

            if (canExplode)
            {
                Explode();
            }
        }

        private void Explode()
        {
            // визуальный эффект
            if (explodeEffect != null)
                Instantiate(explodeEffect, transform.position, transform.rotation);

            // физический взрыв
            Collider[] colliders = Physics.OverlapSphere(transform.position, explosionRadius);

            foreach (Collider col in colliders)
            {
                // толкаем rigidbody
                Rigidbody rb = col.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.AddExplosionForce(
                        explosionForce,
                        transform.position,
                        explosionRadius,
                        upwardsModifier,
                        ForceMode.Impulse
                    );
                }

                // если рядом другой Hardware — ломаем его
                Hardware hw = col.GetComponent<Hardware>();
                if (hw != null && !hw.Damaged)
                {
                    hw.Damage();
                }
            }
        }

        private IEnumerator RenderBurn()
        {
            var renderer = GetComponent<Renderer>();
            if (renderer == null)
                yield break;

            var mat = renderer.material;
            Color startColor = mat.color;

            float t = 0f;

            while (t < 1f)
            {
                t += Time.deltaTime;
                mat.color = Color.Lerp(startColor, burnedColor, t);
                yield return null;
            }
        }
    }
}
