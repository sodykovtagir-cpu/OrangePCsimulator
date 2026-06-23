using System.Collections.Generic;
using System.Linq;
using Google.GData.Spreadsheets;
using Newtonsoft.Json.Linq;
using PC.Component.Software;
using UnityEngine;

namespace PC.Component
{
	public class Storage : Hardware
	{
		[SerializeField]
		private float health;

		[SerializeField]
		private bool breakable;

		[SerializeField]
		private GameObject explode;

		[SerializeField]
		private float uptime;

		public string storageName;

		public string password;

		public List<File> files;

		public float Uptime
		{
			get
			{
				return uptime;
			}
			private set
            {
				uptime = value;
            }
		}

		void OnCollisionEnter(Collision collision)
		{
			float impulseMagnitude = collision.impulse.magnitude;

			if (impulseMagnitude >= 20f && !Damaged && breakable)
			{
				health -= impulseMagnitude;

				if (health <= 0f)
				{
					Damage();
				}
			}
		}

		private void Update()
        {
			if (Power) uptime += Time.deltaTime; 
        }

		public int Usage()
		{
			return files == null ? 0 : files.Sum(f => { return f.StorageSize; });
		}

		public File Write(string path, string content)
		{

			if (TryGetFile(path, out var file))
			{
				int currentUsage = Usage();
				int existingSize = file.size;
				int required = currentUsage - existingSize + content.Length;

				if (required > Capacity) return file;

				file.content = content;
				return file;
			}

			var created = new File(path, content);
			AddFile(created);
			return created;
		}

		public bool AddFile(PC.Component.Software.File file)
		{
			if (file == null) throw new System.ArgumentNullException(nameof(file));

			int used = Usage();
			int size = file.size;

			if (used + size > Capacity) return false;

			if (ContainsFile(file.path))
			{
				var name = File.NameWithoutExtension(file.path);
				var ext  = File.Extension(file.path);

				int n = 1;
				string candidate;
				do
				{
					candidate = string.Format("{0} ({1}){2}", name, n, ext);
					n++;
				} while (ContainsFile(candidate));

				file.path = candidate;
			}

			files.Add(file);
			return true;
		}

		public bool ContainsFile(string path)
		{
			if (files == null) return false;

			foreach (var f in files)
			{
				if (f != null && string.Equals(f.path, path, System.StringComparison.Ordinal))
					return true;
			}

			return false;
		}

		public bool TryGetFile(string path, out File file)
		{
			file = null;
			if (files == null) return false;

			foreach (var f in files)
			{
				if (f == null) continue;
				if (string.Equals(f.path, path, System.StringComparison.Ordinal))
				{
					file = f;
					return true;
				}
			}

			return false;
		}

		public override string GetInfo()
		{
			string baseInfo = base.GetInfo();
			string[] values = new string[5];
			values[0] = baseInfo;
			values[1] = "\n";
			values[2] = Localization.GetText("Health");
			values[3] = ": ";
			values[4] = health.ToString("F0");
			string info = string.Concat(values);
			info += "\n" + storageName;

			return info;
		}

		public override void ToData(JObject jObject)
		{
			jObject.Add("storageName", JToken.FromObject(storageName));
			jObject.Add("password", JToken.FromObject(password));
			jObject.Add("files", JToken.FromObject(files));
			jObject.Add("uptime", JToken.FromObject(uptime));
			jObject.Add("health", JToken.FromObject(health));
			base.ToData(jObject);
		}

		public override void FromData(JObject jObject)
		{
			JToken filesToken;

			if (jObject.TryGetValue("storageData", out var storageDataToken) && storageDataToken is JObject storageData)
			{
				var nameTok = storageData["storageName"];
				var passTok = storageData["userPassword"];
				filesToken  = storageData["files"];
				storageName = nameTok.ToString();
				password    = passTok.ToString();
			}
			else
			{
				var nameTok = jObject["storageName"];
				var passTok = jObject["password"];
				filesToken  = jObject["files"];
				storageName = nameTok.ToString();
				password    = passTok.ToString();
			}

			files = filesToken.ToObject<List<File>>();

			uptime = jObject.Value<float?>("uptime") ?? 0f;
			health = jObject.Value<float?>("health") ?? health;

			base.FromData(jObject);
		}

		public void Explode()
		{
			var original = explode;
			var t = transform;
			Instantiate(original, t.position, t.rotation);
			Damage();
		}
	}
}
