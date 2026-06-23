using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UI;

public class AirConditioner : MonoBehaviour
{
	[SerializeField]
	private ParticleSystem snowParticle;

	[SerializeField]
	private ParticleSystem smoke;

	[SerializeField]
	private Transform snow;

	[SerializeField]
	private float moveSpeed;

	[SerializeField]
	private float maxHeight;

	[SerializeField]
	private float minHeight;

	[SerializeField]
	private float maxTemperature;

	[SerializeField]
	private float minTemperature;

	[SerializeField]
	private float minSlider;

	[SerializeField]
	private float maxSlider;

	[SerializeField]
	private Renderer button;

	[SerializeField]
	private Transform sliderTransform;

	[SerializeField]
	private Slider slider;

	[SerializeField]
	private float targetTemperature;

	[SerializeField]
	private AudioSource source;

	[SerializeField]
	private AudioClip turnOn;

	[SerializeField]
	private AudioClip turnOff;

	[SerializeField]
	private AudioClip turnSnow;

	public static readonly float NormalTemperature;

	private bool snowing;

	private bool soundPlayed;

	private bool isOn;

	private bool hasSnow;

	private float time;

	private Material mat;

	public static float temperature;

	public static AirConditioner instance;

	public float TargetTemperature
	{
		get => targetTemperature;
		set => targetTemperature = value;
	}

	public bool Power
	{
		get => isOn;
		set
		{
			isOn = value;

			var m = mat;
			var s = smoke;

			if (value)
			{
				m.EnableKeyword("_EMISSION");
				m.SetColor("_EmissionColor", new UnityEngine.Color(1f, 0f, 0f, 1f));
				s.Play();
				UpdateSnow();
			}
			else
			{
				m.DisableKeyword("_EMISSION");
				s.Stop();
				UpdateSnow();
			}
		}
	}

	private void Awake()
    {
        instance = this;
        if (button != null) mat = button.material;
    }

	private IEnumerator Start()
	{
		yield return new WaitForEndOfFrame();
		float normalized = Conversion.Map(targetTemperature, minTemperature, maxTemperature, 0f, 1f);
		if (slider != null) slider.value = normalized;
	}

	public void Switch()
	{
		Power = !Power;
		if (source != null) source.PlayOneShot(Power ? turnOn : turnOff);
	}

	private void UpdateSnow()
	{
		float currentTemp = isOn ? targetTemperature : NormalTemperature;
		temperature = currentTemp;
		bool shouldSnow = currentTemp <= -10f;
		if (shouldSnow != snowing)
		{
			snowing = shouldSnow;
			if (snowing)
			{
				hasSnow = true;
				soundPlayed = false;
				if (snowParticle != null) snowParticle.Play();
				if (snow != null && snow.gameObject != null) snow.gameObject.SetActive(true);
			}
			else
			{
				if (snowParticle != null) snowParticle.Stop();
			}
		}
	}

	public void ChangeTemperature(float f)
	{
		if (sliderTransform == null) return;
		f = Mathf.Clamp01(f);
		Vector3 pos = sliderTransform.localPosition;
		pos.y = minSlider + f * (maxSlider - minSlider);
		sliderTransform.localPosition = pos;
		targetTemperature = minTemperature + f * (maxTemperature - minTemperature);
		if (smoke != null)
		{
			var main = smoke.main;
			float alpha = (50f - f * 50f) / 255f;
			main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, alpha));
		}
		UpdateSnow();
	}

	private void Update()
    {
        if (!snowing)
        {
            if (time > 0f)
            {
                time -= Time.deltaTime * moveSpeed;
            }
            else if (hasSnow)
            {
                hasSnow = false;
                if (snow != null && snow.gameObject != null)
                    snow.gameObject.SetActive(false);
            }
        }
        else
        {
            if (time >= 1f && !soundPlayed)
            {
                soundPlayed = true;
                SoundManager.Instance?.PlayOneShot(turnSnow);
            }
            else if (time < 1f)
            {
                time += Time.deltaTime * moveSpeed;
            }
        }

        if (snow != null)
        {
            Vector3 pos = snow.position;
            float t = Mathf.Clamp01(time);
            pos.y = minHeight + t * (maxHeight - minHeight);
            snow.position = pos;
        }
    }
}
