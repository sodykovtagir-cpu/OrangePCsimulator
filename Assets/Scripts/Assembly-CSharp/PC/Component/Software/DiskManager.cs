using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UI;

namespace PC.Component.Software
{
	public class DiskManager : App
	{
		[SerializeField]
		private DiskBlock diskPrefab;

		[SerializeField]
		private Transform diskParent;

		[SerializeField]
		private GameObject general;

		[SerializeField]
		private GameObject clone;

		[SerializeField]
		private GameObject encryption;

		[SerializeField]
		private Button formatButton;

		[SerializeField]
		private Text lockText;

		[SerializeField]
		private Text uptimeText;

		[SerializeField]
		private Image nameImage;

		[SerializeField]
		private InputField nameInput;

		[SerializeField]
		private InputField passwordInput;

		[SerializeField]
		private Sprite selectedSprite;

		private DiskBlock[] blocks;

		private List<Storage> storages;

		private bool cloneMode;

		private int selected;

		private Coroutine cloneCoroutine;

		private Coroutine formatCoroutine;

		private Color evenColor = new Color(0.9f, 1, 1, 1);

		protected override void Start()
		{
			base.Start();

			var os = system;
			if (os == null) return;

			os.TakeResource();

			storages = os.AllStorage;
			if (storages == null) return;

			blocks = new DiskBlock[storages.Count];

			for (int i = 0; i < blocks.Length; i++)
			{
				var block = Instantiate(diskPrefab, diskParent);
				blocks[i] = block;

				if (block == null) continue;

				var go = block.gameObject;
				if (go == null) continue;

				var button = go.GetComponent<Button>();
				if (button == null) continue;

				int index = i;
				button.onClick.AddListener(() => SelectStorage(index));
			}

			DrawColor();
			RefreshDisks();
			SelectStorage(0);
			StartCoroutine(ShowTime());
		}

		private void RefreshDisks()
		{
			if (blocks == null || storages == null) return;

			for (int i = 0; i < blocks.Length; i++)
			{
				var block = blocks[i];
				if (block == null || block.graphic == null) continue;

				bool hasStorage = i < storages.Count;
				block.graphic.SetActive(hasStorage);
				if (!hasStorage) continue;

				var storage = storages[i];
				if (storage == null) continue;

				if (block.nameText != null)
					block.nameText.text = i + ". " + storage.storageName;

				if (block.lockImage != null)
					block.lockImage.enabled = !string.IsNullOrEmpty(storage.password);

				int used = storage.Usage();
				int total = storage.Capacity;

				if (block.infoText != null)
					block.infoText.text = Conversion.Size(used) + "/" + Conversion.Size(total);

				if (block.usage != null && total > 0)
				{
					float t = (float)used / total;
					block.usage.localScale = new Vector3(t, 1f, 1f);
				}
			}
		}

		private void DrawColor()
		{
			if (blocks == null) return;

			bool useEven = false;

			for (int i = 0; i < blocks.Length; i++)
			{
				var block = blocks[i];
				if (block == null || block.graphic == null) continue;
				if (!block.graphic.activeSelf) continue;

				if (block.background == null) continue;

				block.background.color = useEven ? evenColor : Color.white;
				useEven = !useEven;
			}
		}

		private void SelectStorage(int index)
		{
			if (blocks == null) return;

			for (int i = 0; i < blocks.Length; i++)
			{
				var block = blocks[i];
				if (block != null && block.background != null)
					block.background.sprite = null;
			}

			if (!cloneMode)
			{
				selected = index;

				if (formatButton != null)
					formatButton.interactable = index != 0;

				if (index < 0 || index >= blocks.Length) return;

				var block = blocks[index];
				if (block != null && block.background != null)
					block.background.sprite = selectedSprite;

				if (storages == null || index < 0 || index >= storages.Count) return;

				var storage = storages[selected];
				if (nameInput != null && storage != null)
					nameInput.text = storage.storageName;

				if (lockText != null && storage != null)
				{
					bool hasPassword = !string.IsNullOrEmpty(storage.password);
					var key = hasPassword ? "Unlock" : "Lock";
					var text = Localization.GetText(key);
					lockText.text = text;
				}

				RefreshTime();
			}
			else
			{
				if (storages == null) return;
				if (selected < 0 || selected >= storages.Count) return;
				if (index < 0 || index >= storages.Count) return;

				var from = storages[selected];
				var target = storages[index];
				if (from == null || target == null) return;

				var routine = CloneProgress(from, target);
				cloneCoroutine = StartCoroutine(routine);
			}
		}

		public void CloneDisk()
		{
			cloneMode = true;

			if (general != null) general.SetActive(false);
			if (clone != null) clone.SetActive(true);

			if (blocks == null || storages == null) return;

			int sourceIndex = selected;
			if (sourceIndex < 0 || sourceIndex >= storages.Count) return;

			var source = storages[sourceIndex];
			if (source == null) return;

			int sourceUsage = source.Usage();

			for (int i = 0; i < blocks.Length; i++)
			{
				var block = blocks[i];
				if (block == null || block.graphic == null) continue;

				if (i == 0 || i == sourceIndex)
				{
					block.graphic.SetActive(false);
					continue;
				}

				if (i >= storages.Count)
				{
					block.graphic.SetActive(false);
					continue;
				}

				var target = storages[i];
				if (target == null)
				{
					block.graphic.SetActive(false);
					continue;
				}

				if (target.Capacity < sourceUsage)
					block.graphic.SetActive(false);
				else
					block.graphic.SetActive(true);
			}

			DrawColor();
		}

		public void CancelClone()
		{
			cloneMode = false;

			if (general != null) general.SetActive(true);
			if (clone != null) clone.SetActive(false);

			RefreshDisks();
			DrawColor();
		}

		private IEnumerator CloneProgress(Storage from, Storage target)
		{
			CancelClone();

			var os = system;
			var bar = os != null ? os.ProgressBar : null;
			if (bar == null || from == null || target == null) yield break;

			var title = Localization.GetText("Copying") + "...";
			var color = new Color(0.3f, 1f, 0.3f, 1f);
			Action cancelCallback = () => { StopCoroutine(cloneCoroutine); };

			bar.CallProgressBar(title, color, cancelCallback);

			int minScore = from.Score < target.Score ? from.Score : target.Score;
			int minCapacity = from.Capacity < target.Capacity ? from.Capacity : target.Capacity;

			float speed = 0f;
			if (minCapacity > 0) speed = (float)minScore / minCapacity * 50f;

			float t = 0f;

			while (t < 1f)
			{
				if (speed <= 0f)
				{
					bar.SetProgress(1f);
					break;
				}

				t += Time.deltaTime * speed;
				if (t > 1f) t = 1f;

				bar.SetProgress(t);
				yield return null;
			}

			bar.CloseProgressBar();

			target.password = from.password;
			target.files = DeepClone(from.files);

			RefreshDisks();
		}

		public static T DeepClone<T>(T a)
		{
			if (ReferenceEquals(a, null)) return default(T);
			using (var stream = new System.IO.MemoryStream())
			{
				var formatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
				formatter.Serialize(stream, a);
				stream.Position = 0;
				return (T)formatter.Deserialize(stream);
			}
		}

		public void FormatDisk()
		{
			if (storages == null) return;
			if (selected < 0 || selected >= storages.Count) return;

			var target = storages[selected];
			formatCoroutine = StartCoroutine(FormatProgress(target));
		}

		private IEnumerator FormatProgress(Storage target)
		{
			var os = system;
			var bar = os != null ? os.ProgressBar : null;
			if (bar == null || target == null) yield break;

			var title = Localization.GetText("Formatting") + "...";
			var color = new Color(1f, 0.8f, 0.3f, 1f);
			System.Action cancelCallback = () => { StopCoroutine(formatCoroutine); };

			bar.CallProgressBar(title, color, cancelCallback);

			int capacity = target.Capacity;
			int score = target.Score;

			float t = 0f;
			float speed = capacity > 0 ? (float)score / capacity * 50f : 0f;

			while (t < 1f)
			{
				t += Time.deltaTime * speed;
				if (t > 1f) t = 1f;

				bar.SetProgress(t);
				yield return null;
			}

			bar.CloseProgressBar();

			target.password = "";
			target.files = new List<File>();

			RefreshDisks();
		}

		public void LockDisk()
		{
			if (general != null) general.SetActive(false);
			if (encryption != null) encryption.SetActive(true);
			if (passwordInput != null)
			{
				passwordInput.text = "";
				passwordInput.ActivateInputField();
			}
		}

		public void OnEndEditPassword(string password)
		{
			if (storages == null || selected < 0 || selected >= storages.Count) return;

			var storage = storages[selected];
			if (storage == null) return;

			bool wasEmpty = string.IsNullOrEmpty(storage.password);

			if (wasEmpty)
			{
				storage.password = password;
			}
			else
			{
				if (string.Equals(storage.password, password)) storage.password = "";
			}

			if (general != null) general.SetActive(true);
			if (encryption != null) encryption.SetActive(false);

			RefreshDisks();

			if (storages == null || selected < 0 || selected >= storages.Count) return;

			storage = storages[selected];
			if (storage == null || lockText == null) return;

			bool nowEmpty = string.IsNullOrEmpty(storage.password);
			var key = nowEmpty ? "Lock" : "Unlock";
			var text = Localization.GetText(key);
			lockText.text = text;
		}

		private IEnumerator ShowTime()
		{
			while (true)
			{
				RefreshTime();
				yield return new UnityEngine.WaitForSeconds(1f);
			}
		}

		private void RefreshTime()
		{
			if (storages == null || selected < 0 || selected >= storages.Count) return;

			var storage = storages[selected];
			if (storage == null || uptimeText == null) return;

			float seconds = storage.Uptime;
			var span = System.TimeSpan.FromSeconds(seconds);
			uptimeText.text = span.ToString("d':'hh':'mm':'ss");
		}

		public void EditName()
		{
			if (nameImage != null) nameImage.enabled = true;
			if (nameInput != null) nameInput.ActivateInputField();
		}

		public void OnEndEditName(string name)
		{
			if (string.IsNullOrEmpty(name))
			{
				if (storages == null || selected < 0 || selected >= storages.Count) return;

				var storage = storages[selected];
				if (storage == null || nameInput == null) return;

				nameInput.text = storage.storageName;
			}
			else
			{
				if (storages == null || selected < 0 || selected >= storages.Count) return;

				var storage = storages[selected];
				if (storage == null) return;

				storage.storageName = name;
				RefreshDisks();
			}

			if (nameImage != null) nameImage.enabled = false;
		}

		public override void Close()
		{
			var os = system;
			if (os != null && os.ProgressBar != null)
				os.ProgressBar.CloseProgressBar();

			if (os != null)
			{
				os.ReleaseResource();
				base.Close();
			}
		}
	}
}
