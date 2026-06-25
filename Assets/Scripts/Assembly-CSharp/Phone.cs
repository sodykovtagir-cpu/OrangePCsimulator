using UnityEngine;
using System.Collections;

public class Phone : MonoBehaviour
{
    [SerializeField] private AudioSource audioSource;

    [SerializeField] private AudioClip ringingSound;
    [SerializeField] private AudioClip beforeAnswerSound;
    [SerializeField] private AudioClip afterAnswerSound;

    [SerializeField] private float ringDelay = 30f;

    private bool isRinging;
    private bool answered;

    private void Start()
    {
        StartCoroutine(StartRing());
    }

    private IEnumerator StartRing()
    {
        yield return new WaitForSeconds(ringDelay);

        isRinging = true;

        audioSource.clip = ringingSound;
        audioSource.loop = true;
        audioSource.Play();
    }

    private void OnMouseDown()
    {
        if (isRinging)
        {
            isRinging = false;
            answered = true;

            audioSource.Stop();
            audioSource.loop = false;

            audioSource.PlayOneShot(beforeAnswerSound);
            return;
        }

        if (answered)
        {
            audioSource.PlayOneShot(afterAnswerSound);
        }
    }
}