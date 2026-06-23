using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PC.Component.Software
{
	public class FileManager : App
	{
		private struct FileBlock
		{
			public Image block;

			public Text text;
		}

		[SerializeField]
		private Button storagePrefab;

		[SerializeField]
		private Button filePrefab;

		[SerializeField]
		private Transform storageParent;

		[SerializeField]
		private Transform fileParent;

		[SerializeField]
		private InputField fileNameInput;

		[SerializeField]
		private Button cutButton;

		[SerializeField]
		private Button copyButton;

		[SerializeField]
		private Button pasteButton;

		[SerializeField]
		private Button renameButton;

		[SerializeField]
		private Button deleteButton;

		[SerializeField]
		private Toggle hiddenToggle;

		[SerializeField]
		private Color selectedColor;

		private bool copy;

		private int sourceFileIndex;

		private Storage sourceStorage;

		private int selectedFile = -1;

		private Storage selectedStorage;

		private List<FileBlock> fileBlocks = new List<FileBlock>();

		private string extension;

		protected override void Start()
		{
			base.Start();

			var os = system;
			if (os == null || os.AllStorage == null) return;

			os.TakeResource();

			var storages = os.AllStorage;

			for (int i = 0; i < storages.Count; i++)
			{
				var storage = storages[i];
				if (storage == null) continue;

				if (i != 0 && !string.IsNullOrEmpty(storage.password)) continue;

				var button = Instantiate(storagePrefab, storageParent);
				int index = i;
				button.onClick.AddListener(() => SelectStorage(index));

				var text = button.GetComponentInChildren<Text>();
				if (text != null) text.text = i + ". " + storage.storageName;
			}

			SelectStorage(0);
		}

		private void SelectStorage(int index)
		{
			var os = system;
			if (os == null || os.AllStorage == null) return;
			if (index < 0 || index >= os.AllStorage.Count) return;

			selectedStorage = os.AllStorage[index];
			RefreshItem();
		}

		private void RefreshItem()
		{
			if (fileParent == null) return;

			foreach (Transform child in fileParent)
				Destroy(child.gameObject);

			if (fileBlocks != null) fileBlocks.Clear();

			var storage = selectedStorage;
			if (storage == null || storage.files == null)
			{
				SelectFile(-1);
				return;
			}

			var files = storage.files;

			for (int index = 0; index < files.Count; index++)
			{
				var file = files[index];
				if (file == null) continue;

				var button = Instantiate(filePrefab, fileParent);
				int capturedIndex = index;
				button.onClick.AddListener(() => SelectFile(capturedIndex));

				var text = button.GetComponentInChildren<Text>();
				if (text != null) text.text = file.path;
				bool protect = file.path == "System/boot.bin" || file.hidden;
				if (text != null)
				{
					float v = protect ? 0.5f : 0f;
					text.color = new Color(v, v, v, 1f);
				}
				if (fileBlocks != null)
				{
					var image = button.GetComponent<Image>();
					fileBlocks.Add(new FileBlock { block = image, text = text });
				}
			}

			SelectFile(-1);
		}

		private void SelectFile(int index)
		{
			if (fileBlocks == null) return;

			if (selectedFile == index) index = -1;
			selectedFile = index;

			for (int i = 0; i < fileBlocks.Count; i++)
			{
				var fb = fileBlocks[i];
				if (fb.block != null) fb.block.color = Color.white;
			}

			if (selectedFile == -1)
			{
				if (cutButton != null) cutButton.interactable = false;
				if (copyButton != null) copyButton.interactable = false;
				if (renameButton != null) renameButton.interactable = false;
				if (deleteButton != null) deleteButton.interactable = false;
				if (hiddenToggle != null) hiddenToggle.interactable = false;
				return;
			}

			if (selectedFile < 0 || selectedFile >= fileBlocks.Count) return;

			var selectedBlock = fileBlocks[selectedFile];
			if (selectedBlock.block != null) selectedBlock.block.color = selectedColor;

			var file = GetSelectedFile();
			if (file == null)
			{
				if (cutButton != null) cutButton.interactable = false;
				if (copyButton != null) copyButton.interactable = false;
				if (renameButton != null) renameButton.interactable = false;
				if (deleteButton != null) deleteButton.interactable = false;
				if (hiddenToggle != null) hiddenToggle.interactable = false;
				return;
			}

			var ext = File.Extension(file.path);
			bool protect = ext == ".exe" || file.path == "System/boot.bin";

			if (cutButton != null) cutButton.interactable = !protect;
			if (copyButton != null) copyButton.interactable = !protect;
			if (renameButton != null) renameButton.interactable = !protect;
			if (deleteButton != null) deleteButton.interactable = !protect;

			if (hiddenToggle != null)
			{
				hiddenToggle.interactable = true;
				hiddenToggle.isOn = file.hidden;
			}
		}

		public void OnValueChangedHidden(bool hidden)
		{
			var file = GetSelectedFile();
			if (file == null) return;

			file.hidden = hidden;

			if (fileBlocks == null || selectedFile < 0 || selectedFile >= fileBlocks.Count) return;

			var block = fileBlocks[selectedFile];
			if (block.text != null)
			{
				float v = hidden ? 0.5f : 0f;
				block.text.color = new Color(v, v, v, 1f);
			}
		}

		public void Cut()
		{
			copy = false;
			sourceFileIndex = selectedFile;
			sourceStorage = selectedStorage;
			if (pasteButton != null) pasteButton.interactable = true;
		}

		public void Copy()
		{
			copy = true;
			sourceFileIndex = selectedFile;
			sourceStorage = selectedStorage;
			if (pasteButton != null) pasteButton.interactable = true;
		}

		public void Paste()
		{
			var srcStorage = sourceStorage;
			if (srcStorage == null || srcStorage.files == null) return;
			if (sourceFileIndex < 0 || sourceFileIndex >= srcStorage.files.Count) return;

			var srcFile = srcStorage.files[sourceFileIndex];
			var target = selectedStorage;
			if (srcFile == null || target == null) return;

			if (!copy)
			{
				bool added = target.AddFile(srcFile);
				if (added) srcStorage.files.RemoveAt(sourceFileIndex);
				if (pasteButton != null) pasteButton.interactable = false;
				RefreshItem();
				return;
			}

			var clone = new File(srcFile.path, srcFile.content, srcFile.hidden, srcFile.size);

			target.AddFile(clone);
			RefreshItem();
		}

		public void Rename()
		{
			if (fileNameInput == null) return;

			var go = fileNameInput.gameObject;
			if (go == null) return;

			go.SetActive(true);

			var file = GetSelectedFile();
			if (file == null) return;

			var nameWithoutExt = File.NameWithoutExtension(file.path);
			fileNameInput.text = nameWithoutExt;
			fileNameInput.ActivateInputField();

			extension = File.Extension(file.path);
		}

		public void ApplyRename()
		{
			if (fileNameInput == null) return;

			var inputGo = fileNameInput.gameObject;
			if (inputGo == null) return;

			inputGo.SetActive(false);

			var newName = fileNameInput.text + extension;
			var storage = selectedStorage;
			if (storage == null || storage.files == null) return;

			var files = storage.files;

			for (int i = 0; i < files.Count; i++)
			{
				var f = files[i];
				if (f == null) continue;
				if (string.Equals(f.path, newName)) return;
			}

			if (selectedFile < 0 || selectedFile >= files.Count) return;

			var file = files[selectedFile];
			if (file == null) return;

			file.path = newName;

			if (fileBlocks == null || selectedFile < 0 || selectedFile >= fileBlocks.Count) return;

			var block = fileBlocks[selectedFile];
			if (block.text != null) block.text.text = newName;
		}

		public void Delete()
		{
			var storage = selectedStorage;
			if (storage == null || storage.files == null) return;
			if (selectedFile < 0 || selectedFile >= storage.files.Count) return;

			storage.files.RemoveAt(selectedFile);

			if (fileBlocks != null && selectedFile >= 0 && selectedFile < fileBlocks.Count)
			{
				var fb = fileBlocks[selectedFile];
				if (fb.block != null) Destroy(fb.block.gameObject);
			}

			RefreshItem();
		}

		private File GetSelectedFile()
		{
			var storage = selectedStorage;
			if (storage == null || storage.files == null) return null;
			if (selectedFile < 0 || selectedFile >= storage.files.Count) return null;
			return storage.files[selectedFile];
		}

		public override void Close()
		{
			var os = system;
			if (os != null)
			{
				os.ReleaseResource();
				base.Close();
			}
		}
	}
}
