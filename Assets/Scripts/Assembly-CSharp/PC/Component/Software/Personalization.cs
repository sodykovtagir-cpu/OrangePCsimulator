using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UI;

namespace PC.Component.Software
{
	public class Personalization : App
	{
		[SerializeField]
		private Text editPictureText;

		[SerializeField]
		private RawImage userPicture;

		[SerializeField]
		private InputField userNameInput;

		[SerializeField]
		private InputField passwordInput;

		protected override void Start()
		{
			base.Start();
			RefreshPicture();

			var os = system;
			if (os == null) return;

			if (userNameInput != null) userNameInput.text = os.UserName;

			var all = os.AllStorage;
			if (all != null && all.Count > 0)
			{
				var st = all[0] as Storage;
				if (st != null && passwordInput != null) passwordInput.text = st.password;
			}
		}

		private void RefreshPicture()
		{
			var os = system;
			if (os == null) return;

			if (userPicture != null) userPicture.texture = os.UserPicture();

			var hasPath = !string.IsNullOrEmpty(os.UserPicturePath);
			if (editPictureText != null) editPictureText.text = Localization.GetText(hasPath ? "Clear" : "Edit");
		}

		public void EditPicture()
		{
			var os = system;
			if (os == null) return;

			var path = os.UserPicturePath;
			if (string.IsNullOrEmpty(path))
			{
				Action<File> cb = file =>
				{
					if (file == null) return;
					os.UserPicturePath = file.path;
					RefreshPicture();
				};
				os.SelectFile(".pic", cb);
			}
			else
			{
				os.UserPicturePath = "";
				RefreshPicture();
			}
		}

		public void EditUserName()
		{
			var i = userNameInput;
			if (i == null) return;
			i.interactable = true;
			i.ActivateInputField();
		}

		public void OnEndEditUserName(string name)
		{
			var os = system;
			if (os == null) return;
			var input = userNameInput;

			if (string.IsNullOrEmpty(name))
			{
				if (input != null) input.text = os.UserName;
			}
			else
			{
				os.UserName = name;
			}

			StartCoroutine(DisableInput(input));
		}

		public void EditPassword()
		{
			var i = passwordInput;
			if (i == null) return;
			i.interactable = true;
			i.ActivateInputField();
		}

		public void OnEndEditPassword(string password)
		{
			var os = system;
			if (os == null) return;
			var all = os.AllStorage;
			if (all == null || all.Count == 0) return;
			var st = all[0] as Storage;
			if (st == null) return;
			st.password = password;
			StartCoroutine(DisableInput(passwordInput));
		}

		public void ChangeBackground(int index)
		{
			var os = system;
			if (os == null) return;
			os.UpdateBackground(index);
		}

		private IEnumerator DisableInput(InputField input)
		{
			yield return new WaitForEndOfFrame();
			if (input == null) yield break;
			input.interactable = false;
		}
	}
}
