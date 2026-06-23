using UnityEngine;
using UnityEngine.UI;

namespace PC.Component.Software.OS
{
	public class MessageBox : MonoBehaviour
	{
		[SerializeField]
		private Text titleText;

		[SerializeField]
		private Text messageText;

		public void Show(string title, string message)
		{
			var t = titleText;
			if (t != null) t.text = title;
			var m = messageText;
			if (m != null) m.text = message;
			var go = gameObject;
			if (go != null) go.SetActive(true);
		}

		public void Ok()
		{
			var go = gameObject;
			if (go != null) go.SetActive(false);
		}
	}
}
