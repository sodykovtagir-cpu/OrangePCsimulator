using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using PC.Component.Software.OS;
using UnityEngine;
using UnityEngine.UI;

namespace PC.Component.Software
{
	public class Terminal : App
	{
		[SerializeField]
		private Text output;

		[SerializeField]
		private GameObject inputObj;

		[SerializeField]
		private InputField input;

		[SerializeField]
		[TextArea(1, 20)]
		private string help;

		[SerializeField]
		[Range(1f, 20000f)]
		private float frequency;

		[SerializeField]
		private int sampleRate = 44100;

		[SerializeField]
		private float waveLengthInSeconds = 2;

		[SerializeField]
		private float volume = 1;

		private AudioSource audioSource;

		private int timeIndex;

		private bool playing;

		private bool useFile;

		private bool newline;

		private bool hideInput;

		private Regex regex = new Regex(" (?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)");

		public override void Open(string content)
		{
			base.Open(content);

			var b = system != null ? system.Board : null;
			audioSource = b != null ? b.Source : null;

			if (string.IsNullOrEmpty(content))
			{
				useFile = false;
				Write("PCOS 1.0 \n(" + "Type \"help\" to see all commands" + ") ");
				newline = true;
				return;
			}

			useFile = true;
			StartCoroutine(RunFile(content));
		}

		public override void Close()
		{
			if (playing)
			{
				StopAllCoroutines();
				var src = audioSource;
				if (src != null) src.Stop();
				playing = false;
			}
			base.Close();
		}

		public void Enter()
		{
			if (playing) return;
			var i = input;
			if (i == null) return;
			StartCoroutine(Run(i.text));
			i.text = "";
		}

		public IEnumerator Run(string command)
		{
			playing = true;
			if (inputObj != null) inputObj.SetActive(false);
			yield return StartCoroutine(RunCommand(command));
			playing = false;
			if (inputObj != null) inputObj.SetActive(true);
		}

		public IEnumerator RunFile(string commands)
		{
			playing = true;
			if (inputObj != null) inputObj.SetActive(false);
			Write("Running...");
			newline = true;
			yield return new WaitForSeconds(0.5f);

			if (string.IsNullOrEmpty(commands))
			{
				playing = false;
				if (!useFile)
				{
					if (inputObj != null) inputObj.SetActive(true);
				}
				else
				{
					Close();
				}
				yield break;
			}

			commands = commands.Replace("\r", "");
			var lines = commands.Split('\n');
			for (int i = 0; i < lines.Length; i++)
			{
				var line = lines[i];
				if (line == null) continue;
				if (line.StartsWith("//")) continue;
				yield return StartCoroutine(RunCommand(line));
			}

			playing = false;
			if (!useFile)
			{
				if (inputObj != null) inputObj.SetActive(true);
			}
			else
			{
				Close();
			}
		}

		public IEnumerator RunCommand(string line)
		{
			if (!hideInput)
			{
				Write("> " + line);
				newline = true;
			}

			var command = new Queue<string>(SplitCommand(line));
			if (command.Count == 0) yield break;

			var cmd = command.Dequeue();

			if (cmd == "hide-input")
			{
				hideInput = true;
			}
			else if (cmd == "close")
			{
				Close();
				yield break;
			}
			else if (cmd == "system-id")
			{
				var os = system;
				if (os != null)
				{
					var id = os.SystemId;
					Write("System ID is " + id.ToString("X8"));
					newline = true;
				}
			}
			else if (cmd == "help")
			{
				Write(help);
				newline = true;
			}
			else if (cmd == "delay")
			{
				if (command.Count > 0)
				{
					var ms = int.Parse(command.Dequeue());
					yield return new WaitForSeconds(ms / 1000f);
				}
			}
			else if (cmd == "clear")
			{
				ClearOutput();
			}
			else if (cmd == "beep")
			{
				Write(line);
				newline = true;

				if (command.Count > 0) frequency = float.Parse(command.Dequeue());
				timeIndex = 0;

				var src = audioSource;
				if (src != null)
				{
					var clip = AudioClip.Create("Beep", sampleRate, 1, sampleRate, true, OnAudioRead);
					src.loop = true;
					src.clip = clip;
					src.Play();

					if (command.Count > 0)
					{
						var ms = int.Parse(command.Dequeue());
						yield return new WaitForSeconds(ms / 1000f);
						src.Stop();
					}
				}
			}
			else if (cmd == "remote")
			{
				RemoteComputer(command);
			}
			else if (cmd == "shutdown")
			{
				var os = system;
				if (os != null) os.PowerClicked();
			}
			else
			{
				Write(line);
				newline = true;
			}

			var outText = output != null ? output.text : null;
			if (outText != null && outText.Length > 5000) ClearOutput();
		}

		private void RemoteComputer(Queue<string> command)
		{
			if (command == null || command.Count == 0) return;

			var hex = command.Dequeue();
			if (string.IsNullOrEmpty(hex)) return;

			int id = Convert.ToInt32(hex, 16);
			var os = system;
			if (os == null) return;

			string msg = "The computer cannot be used!";
			if (os.SystemId != id)
			{
				var boards = FindObjectsOfType<Motherboard>();
				if (boards != null)
				{
					for (int i = 0; i < boards.Length; i++)
					{
						var mb = boards[i];
						if (mb == null) continue;
						if (mb.Id != id) continue;

						var next = command.Count > 0 ? command.Dequeue() : "";
						if (next == "startup")
						{
							if (!mb.Running)
							{
								var result = mb.Boot();
								msg = string.IsNullOrEmpty(result)
									? $"Starting computer {id:X8}!"
									: $"Computer {id:X8} failed to start!";
							}
							else
							{
								msg = $"Computer {id:X8} is running!";
							}
						}
						else
						{
							var ros = mb.System as OS.OperatingSystem;
							if (mb.Running && ros != null && ros.Ready)
							{
								RunOnSystem(next, command, ros);
								return;
							}
							msg = $"Computer {id:X8} is not ready!";
						}

						break;
					}
				}
			}

			Write(msg);
			newline = true;
		}

		private void RunOnSystem(string operation, Queue<string> command, OS.OperatingSystem system)
		{
			if (operation == "shutdown")
			{
				if (system == null) return;
				system.PowerClicked();
				var id = system.SystemId;
				Write(string.Format("Shutting down computer {0:X8}!", id));
				newline = true;
				return;
			}

			if (operation != "open") return;
			if (command == null || command.Count == 0 || system == null) return;

			var path = command.Dequeue();
			var fm = system.FileManager;
			if (fm == null) return;

			File file;
			bool opened = fm.TryGetFile(0, path, out file) && system.OpenFile(file);
			var sysId = system.SystemId;
			var msg = opened
				? string.Format("{0} opened successfully on computer {1:X8}!", path, sysId)
				: string.Format("Failed to open {0} on computer {1:X8}!", path, sysId);

			Write(msg);
			newline = true;
		}

		private string[] SplitCommand(string input)
		{
			if (regex == null) return new[] { input ?? "" };
			var parts = regex.Split(input ?? "");
			for (int i = 0; i < parts.Length; i++) parts[i] = (parts[i] ?? "").Replace("\"", "");
			return parts;
		}

		private void OnAudioRead(float[] data)
		{
			if (data == null) return;
			int len = data.Length;
			float sr = sampleRate;
			for (int i = 0; i < len; i++)
			{
				float phase = (timeIndex * 2f * Mathf.PI * frequency) / sr;
				float s = Mathf.Sin(phase) < 0f ? -1f : 1f;
				data[i] = volume * s;
				timeIndex++;
				if (timeIndex >= waveLengthInSeconds * sr) timeIndex = 0;
			}
		}

		private float CreateWave(int timeIndex, float frequency, float sampleRate)
		{
			float phase = (timeIndex * 2f * Mathf.PI * frequency) / sampleRate;
			float s = Mathf.Sin(phase) < 0f ? -1f : 1f;
			return s * volume;
		}

		public void Write(string value)
		{
			var t = output;
			if (t == null) return;
			t.enabled = true;
			if (newline)
			{
				t.text = t.text + "\n";
				newline = false;
			}
			t.text = t.text + value;
		}

		public void WriteLine(string value)
        {
			Write(value);
			newline = true;
        }

		public void ClearOutput()
		{
			var t = output;
			if (t == null) return;
			t.text = "";
			newline = false;
			t.enabled = false;
		}
	}
}
