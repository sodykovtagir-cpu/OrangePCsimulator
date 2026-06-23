using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PC.VR
{
	public class VirtualWorld : MonoBehaviour
	{
		[SerializeField]
		private Text infoText;

		[SerializeField]
		private Text healthText;

		[SerializeField]
		private Player player;

		[SerializeField]
		private GameObject loading;

		[SerializeField]
		private GameObject action;

		[SerializeField]
		private GameObject close;

		[SerializeField]
		private GameObject control;

		[SerializeField]
		private GameObject end;

		[SerializeField]
		private GameObject barrier;

		[SerializeField]
		private GameObject explode;

		[SerializeField]
		private GameObject bitcoin;

		[SerializeField]
		private ParticleSystem waveParticle;

		[SerializeField]
		private ParticleSystem smokeParticle;

		[SerializeField]
		private AudioClip bitcoinSound;

		[SerializeField]
		private Camera cam;

		[SerializeField]
		private GameObject eventSystem;

		[Header("Enemy")]
		[SerializeField]
		private GameObject enemyPrefab;

		[SerializeField]
		private Transform[] spawnPoints;

		[SerializeField]
		private float spawnDelay = 2;

		[SerializeField]
		private int maxEnemy = 10;

		private float health = 100;

		private AudioSource source;

		private bool fuelDone;

		private bool isEnd;

		private bool ready;

		public static VirtualWorld Instance { get; private set; }

		private void Awake()
		{
			Instance = this;
			if (player != null) player.Sensitivity = PlayerPrefs.GetFloat("Sensitivity", player.Sensitivity);
		}

		private IEnumerator Start()
		{
			Enemy.count = 0;
			if (UnityEngine.SceneManagement.SceneManager.sceneCount >= 2 && eventSystem != null) eventSystem.SetActive(false);
			source = GetComponent<AudioSource>();
			if (healthText != null) healthText.text = health.ToString("0");
			if (infoText != null) infoText.text = "";
			if (cam != null) cam.farClipPlane = 1f;
			yield return StartCoroutine(TextAnimation("Initializing system...\n"));
			yield return new WaitForSeconds(1f);

			if (infoText != null)
			{
				yield return StartCoroutine(TextAnimation("Loading terrain..."));
				var oldText = infoText.text;
				yield return StartCoroutine(TextAnimation("[0%]"));
				yield return new WaitForSeconds(1f);

				float t = 0f;
				while (t < 1f)
				{
					float dt = Time.deltaTime;
					t += dt;
					if (cam != null) cam.farClipPlane = Mathf.Clamp01(t) * 40f;
					if (infoText != null)
					{
						var pct = (t * 100f).ToString("0");
						infoText.text = oldText + "[" + pct + "%]";
					}
					yield return null;
				}

				if (cam != null) cam.farClipPlane = 40f;
				infoText.text = oldText + "[100%]\n";
			}

			yield return StartCoroutine(TextAnimation("Preparing..." + "\n"));
			yield return new WaitForSeconds(1f);

			if (infoText != null) infoText.text += "Done";
			if (loading != null) loading.SetActive(false);
			if (action != null) action.SetActive(true);
			if (control != null) control.SetActive(true);
			ready = true;
			if (player != null) player.pauseControls = false;
			if (close != null) close.SetActive(true);
			InvokeRepeating("SpawnEnemy", 5f, spawnDelay);
			yield return new WaitForSeconds(3f);

			for (int i = 0; i < 2; i++)
			{
				if (infoText != null) infoText.enabled = true;
				yield return new WaitForSeconds(0.2f);
				if (infoText != null) infoText.enabled = false;
				yield return new WaitForSeconds(0.2f);
			}
		}

		private void Update()
		{
			if (ready && Input.GetButtonDown("Cancel")) Close();
		}

		private IEnumerator TextAnimation(string str)
		{
			foreach (var ch in str)
			{
				if (infoText != null) infoText.text += ch.ToString();
				yield return new WaitForSeconds(0.02f);
			}
		}

		private void SpawnEnemy()
		{
			if (isEnd || Enemy.count >= maxEnemy) return;
			if (spawnPoints == null || spawnPoints.Length == 0) return;
			int i = UnityEngine.Random.Range(0, spawnPoints.Length);
			var t = spawnPoints[i];
			if (t == null) return;
			Instantiate(enemyPrefab, t.position, Quaternion.identity);
		}

		public void EnoughFuel()
		{
			if (fuelDone) return;
			fuelDone = true;
			if (waveParticle != null) waveParticle.Play();
			if (smokeParticle != null) smokeParticle.Play();
		}

		public void ClearBarrier()
		{
			if (!fuelDone) return;
			if (barrier != null) barrier.SetActive(false);
			if (explode != null) explode.SetActive(true);
		}

		public void Hurt(float value)
		{
			if (isEnd) return;
			health -= value;
			if (healthText != null) healthText.text = health.ToString("0");
			if (health <= 0f) GameOver();
		}

		public void GameOver()
		{
			isEnd = true;
			if (player != null) player.pauseControls = true;
			if (control != null) control.SetActive(false);
			if (end != null) end.SetActive(true);
		}

		public void Close()
		{
			var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
			var fallback = default(UnityEngine.SceneManagement.Scene);

			for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
			{
				var s = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
				if (s.IsValid() && s.handle != active.handle)
				{
					fallback = s;
					break;
				}
			}

			if (fallback.IsValid()) UnityEngine.SceneManagement.SceneManager.SetActiveScene(fallback);
			UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(active.buildIndex);
			Main.Instance.ExitVirtualWorld();
		}

		public void AddBitcoin()
		{
			var current = (float)BitcoinManager.Bitcoin;
			BitcoinManager.Bitcoin = (Yiming.AntiCheat.FloatShadow)(current + 1f);
			BitcoinManager.Save();

			CloudOnceManager.Instance.GetAchievementFromId("vr_master")?.Unlock(null);

			if (source != null) source.PlayOneShot(bitcoinSound);
			if (bitcoin != null) bitcoin.SetActive(false);
		}
	}
}
