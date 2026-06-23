using PC.Component.Software.OS;
using UnityEngine;
using UnityEngine.UI;

namespace PC.Component.Software
{
	public class DeviceItem : MonoBehaviour
	{
		[SerializeField]
		private Image iconImage;

		[SerializeField]
		private Text nameText;

		[SerializeField]
		private Text statusText;

		[SerializeField]
		private Button connectButton;

		[SerializeField]
		private Button renameButton;

		[SerializeField]
		private Button removeButton;

		public DeviceDetail detail;

		private bool connected;

		public Sprite Icon
		{
			set
            {
                iconImage.sprite = value;
            }
		}

		public string Name
		{
			set
            {
                nameText.text = value;
            }
		}

		public string Status
		{
			set
            {
                statusText.text = value;
            }
		}

		public Button.ButtonClickedEvent OnRenameClick => renameButton.onClick;

		public bool Connected
		{
			get => connected;
			set
            {
                connected = value;
				if (connectButton == null) return;
				connectButton.gameObject.SetActive(!value);
				if (renameButton != null) renameButton.gameObject.SetActive(value);
				if (removeButton != null) removeButton.gameObject.SetActive(value);
            }
		}

		public void Refresh(DeviceDetail detail)
		{
			this.detail = detail;
			nameText.text = detail.name;
		}

		public void Connect()
		{
			Connected = true;
		}

		public void Disconnect()
		{
			Connected = false;
		}
	}
}
