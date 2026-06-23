using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public AudioSource mainSource;

    public static SoundManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        if (mainSource == null)
        {
            mainSource = gameObject.AddComponent<AudioSource>();
        }
    }

    public void PlayOneShot(AudioClip clip)
    {
        if (mainSource != null && clip != null)
        {
            mainSource.PlayOneShot(clip);
        }
    }

    public static void SPlayOneShot(AudioClip clip)
    {
        if (clip == null) return;

        GameObject tempGO = new GameObject("TempAudio");
        AudioSource aSource = tempGO.AddComponent<AudioSource>();
        aSource.PlayOneShot(clip);
        Object.Destroy(tempGO, clip.length);
    }

    public static void PlayClipAtPoint(AudioClip clip, Vector3 position, float volume = 1f, float minDistance = 1f)
    {
        if (clip == null) return;

        GameObject tempGO = new GameObject("TempAudio");
        tempGO.transform.position = position;
        AudioSource aSource = tempGO.AddComponent<AudioSource>();
        aSource.clip = clip;
        aSource.volume = volume;
        aSource.minDistance = minDistance;
        aSource.spatialBlend = 1f;
        aSource.Play();
        Object.Destroy(tempGO, clip.length);
    }
}
