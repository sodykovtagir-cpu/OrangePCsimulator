using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Slider))]
public class SliderHandler : MonoBehaviour
{
	private Slider slider;

	private void Start()
    {
		slider = GetComponent<Slider>();
    }

	private void Update()
    {
		return;
    }
}
