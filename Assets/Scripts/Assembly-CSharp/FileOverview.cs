using UnityEngine;
using UnityEngine.UI;

public class FileOverview : MonoBehaviour
{
	[SerializeField]
	private ListView view;

	[SerializeField]
	private RawImage texPrefab;

	[SerializeField]
	private Transform texParent;

	private string[] filePaths;

	private readonly string tex = "iVBORw0KGgoAAAANSUhEUgAAA.*?(?=[\",])";

	private void Start()
	{
		filePaths = System.IO.Directory.GetFiles(SaveManagement.SaveUtility.GetFolderPath(), "*.pc");
		if (filePaths != null && filePaths.Length > 0 && view != null)
		{
			for (int i = 0; i < filePaths.Length; i++)
			{
				var name = System.IO.Path.GetFileNameWithoutExtension(filePaths[i]);
				view.Add(new ListViewItem { icon = null, text = name });
			}
			view.SelectedIndexChanged += View_SelectedIndexChanged;
		}
	}

	private void View_SelectedIndexChanged(int index)
	{
		if (texParent != null)
		{
			foreach (Transform child in texParent)
				Destroy(child.gameObject);
		}
		if (index == -1 || view == null || filePaths == null || index < 0 || index >= filePaths.Length) return;

		var regex = new System.Text.RegularExpressions.Regex(tex);
		var path = filePaths[index];
		var data = SaveManagement.SaveUtility.EncryptDecrypt(SaveManagement.SaveUtility.Load(path));
		var matches = regex.Matches(data);
		foreach (System.Text.RegularExpressions.Match m in matches)
		{
			var img = Instantiate(texPrefab, texParent);
			var texture = FormatConverter.StringToTexture(m.Value);
			img.texture = texture;
		}
	}
}
