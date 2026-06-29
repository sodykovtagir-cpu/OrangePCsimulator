using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.SocialPlatforms;
using UnityEngine.UI;

public class PauseMenu : MonoBehaviour
{
    [SerializeField]
    private Slider fovSlider;

    [SerializeField]
    private Text fovText;

    [SerializeField]
	private Slider volumeSlider;

	[SerializeField]
	private Slider sensitivitySlider;

	[SerializeField]
	private ConfirmationDialog warningDialog;

	[SerializeField]
	private Text volumeText;

	[SerializeField]
	private Text sensitivityText;

	private static int videoCount;

    private void Start()
    {
        if (volumeSlider != null)
            volumeSlider.value = PlayerPrefs.GetFloat("Volume", 1f);

        if (sensitivitySlider != null)
        {
            var p = Player.Instance;
            if (p != null)
                sensitivitySlider.value = PlayerPrefs.GetFloat("Sensitivity", p.Sensitivity);
        }

        if (fovSlider != null)
        {
            Camera cam = Camera.main;

            if (cam != null)
            {
                float fov = PlayerPrefs.GetFloat("FOV", cam.fieldOfView);
                cam.fieldOfView = fov;
                fovSlider.value = fov;
            }
        }

        if (volumeSlider != null)
            OnVolumeChanged(volumeSlider.value);

        if (sensitivitySlider != null)
            OnSensitivityChanged(sensitivitySlider.value);

        if (fovSlider != null)
            OnFOVChanged(fovSlider.value);

        if (volumeSlider != null)
            volumeSlider.onValueChanged.AddListener(OnVolumeChanged);

        if (sensitivitySlider != null)
            sensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);

        if (fovSlider != null)
            fovSlider.onValueChanged.AddListener(OnFOVChanged);
    }

    public void ExitWithoutSave()
	{
		warningDialog.Show(() =>
		{
			MainMenu();	
		});
	}

	public void Home()
	{
		if (SaveManager.Instance.SaveData())
        {
			MainMenu();
        }
	}

	public void MainMenu()
	{
		SceneManager.LoadScene("Menu");
	}

	public void OnSensitivityChanged(float value)
	{
		Player.Instance.Sensitivity = value;
		var t = Localization.GetText("Sensitivity");
		var v = value.ToString("0.#");
		sensitivityText.text = t + " x" + v;
	}

	public void OnVolumeChanged(float value)
	{
		AudioListener.volume = value;
		var t = Localization.GetText("Volume");
		var v = (value * 100f).ToString("0");
		volumeText.text = t + ": " + v + "%";
	}

    public void OnFOVChanged(float value)
    {
        Camera cam = Camera.main;

        if (cam != null)
            cam.fieldOfView = value;

        PlayerPrefs.SetFloat("FOV", value);
        PlayerPrefs.Save();

        var t = Localization.GetText("FOV");
        fovText.text = t + ": " + value.ToString("0");
    }

    public void Restart()
	{
		SceneManager.LoadScene(SceneManager.GetActiveScene().name);
	}

	private void OnDestroy()
	{
		if (volumeSlider != null) PlayerPrefs.SetFloat("Volume", volumeSlider.value);
		if (sensitivitySlider != null) PlayerPrefs.SetFloat("Sensitivity", sensitivitySlider.value);
        if (fovSlider != null) PlayerPrefs.SetFloat("FOV", fovSlider.value);
    }
}
