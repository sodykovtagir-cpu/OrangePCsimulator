#!/usr/bin/env python3
"""Localize the runtime strings of ThreeDMork.cs.

Every literal that ends up on screen is replaced with a Localization.GetText
lookup, the window size follows the shrunk prefab and the start screen is
refreshed when the player switches language. The patch is idempotent: a marker
in the file header records that it already ran.
"""
import os
import sys

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SOURCE = os.path.join(REPO, "Assets", "Scripts", "Assembly-CSharp",
                      "PC", "Component", "Software", "ThreeDMork.cs")
MARKER = "// 3DMork strings are localized, see tools/localize_3dmork_source.py"
WINDOW = "722.5f, 408.5f"

REPLACEMENTS = [
    # window size follows the shrunk prefab
    ("\t\t\tSetDefaultSize(new Vector2(850f, 520f));",
     "\t\t\tSetDefaultSize(new Vector2(%s));" % WINDOW),

    # hardware summary
    ('''				hardwareTitle.text = snapshot.hasBoard
					? "THIS PC - " + snapshot.boardName
					: "THIS PC - NO MOTHERBOARD";''',
     '''				hardwareTitle.text = snapshot.hasBoard
					? Format("3DMork hardware title", snapshot.boardName)
					: Tr("3DMork hardware no board");'''),

    ('''				hardwareCpu.text = snapshot.cpuCount > 0
					? "CPU: " + snapshot.cpuName
					: "CPU: not installed";''',
     '''				hardwareCpu.text = Format("3DMork hardware cpu", snapshot.cpuCount > 0
					? snapshot.cpuName
					: Tr("3DMork hardware missing"));'''),

    ('''				hardwareGpu.text = snapshot.gpuCount > 0
					? "GPU: " + snapshot.gpuName
					: "GPU: not installed";''',
     '''				hardwareGpu.text = Format("3DMork hardware gpu", snapshot.gpuCount > 0
					? snapshot.gpuName
					: Tr("3DMork hardware missing"));'''),

    ('''				hardwareRam.text = snapshot.ramCount > 0
					? "RAM: " + FormatRam(snapshot.ramCapacity)
					: "RAM: not installed";''',
     '''				hardwareRam.text = Format("3DMork hardware ram", snapshot.ramCount > 0
					? FormatRam(snapshot.ramCapacity)
					: Tr("3DMork hardware missing"));'''),

    ('''				hardwareBoard.text = snapshot.hasBoard
					? "BOARD: " + snapshot.boardName
					: "BOARD: build a PC to run the benchmark";''',
     '''				hardwareBoard.text = Format("3DMork hardware board", snapshot.hasBoard
					? snapshot.boardName
					: Tr("3DMork hardware build first"));'''),

    # leaderboard
    ('''				SetCell(leaderboardFps, i, hasRow ? entry.fps.ToString() + " fps" : "--", new Color(0.55f, 0.9f, 0.6f));''',
     '''				SetCell(leaderboardFps, i, hasRow ? Format("3DMork fps", entry.fps.ToString()) : "--", new Color(0.55f, 0.9f, 0.6f));'''),

    ('''				leaderboardSelfName.text = snapshot.hasBoard ? snapshot.boardName : "This PC";''',
     '''				leaderboardSelfName.text = snapshot.hasBoard ? snapshot.boardName : Tr("3DMork self pc");'''),

    ('''				string spec = (snapshot.cpuCount > 0 ? snapshot.cpuName : "no CPU") + "  /  " +
							  (snapshot.gpuCount > 0 ? snapshot.gpuName : "no GPU");''',
     '''				string spec = (snapshot.cpuCount > 0 ? snapshot.cpuName : Tr("3DMork no cpu")) + "  /  " +
							  (snapshot.gpuCount > 0 ? snapshot.gpuName : Tr("3DMork no gpu"));'''),

    ('''				leaderboardSelfFps.text = lastFps > 0 ? lastFps.ToString() + " fps" : "--";''',
     '''				leaderboardSelfFps.text = lastFps > 0 ? Format("3DMork fps", lastFps.ToString()) : "--";'''),

    ('''				leaderboardAverage.text = "Average of " + ReferenceBenchmarks.Length + " reference PCs: " + avg +
										  "   |   Your best: " + (best > 0 ? best.ToString() : "--");''',
     '''				leaderboardAverage.text = Format("3DMork average", ReferenceBenchmarks.Length, avg,
										  best > 0 ? best.ToString() : "--");'''),

    # history
    ('''				SetCell(historyFps, i, hasRow ? history[i].fps.ToString() + " fps" : "--", new Color(0.55f, 0.9f, 0.6f));''',
     '''				SetCell(historyFps, i, hasRow ? Format("3DMork fps", history[i].fps.ToString()) : "--", new Color(0.55f, 0.9f, 0.6f));'''),

    ('''				historyBest.text = best > 0
					? "Best score: " + best
					: "Best score: -- (no runs yet)";''',
     '''				historyBest.text = Format("3DMork best score", best > 0
					? best.ToString()
					: Tr("3DMork no runs yet"));'''),

    # flyby phase names
    ('''					string pName = wp != null && !string.IsNullOrEmpty(wp.phaseName)
						? wp.phaseName
						: string.Format("Scene {0}: Benchmark Flyby", i + 1);''',
     '''					string pName = wp != null && !string.IsNullOrEmpty(wp.phaseName)
						? Localization.GetText(wp.phaseName)
						: Format("3DMork scene generic", (i + 1).ToString());'''),

    ('''						name = string.IsNullOrEmpty(phase.phaseName) ? string.Format("Scene {0}", i + 1) : phase.phaseName,''',
     '''						name = string.IsNullOrEmpty(phase.phaseName)
							? Format("3DMork scene generic", (i + 1).ToString())
							: Localization.GetText(phase.phaseName),'''),

    ('\t\t\t\tname = "Scene 1: Tech Showcase Flyby",',
     '\t\t\t\tname = Tr("3DMork scene 1"),'),
    ('\t\t\t\tname = "Scene 2: Hardware & Geometry Test",',
     '\t\t\t\tname = Tr("3DMork scene 2"),'),
    ('\t\t\t\tname = "Scene 3: Dynamic Lighting Test",',
     '\t\t\t\tname = Tr("3DMork scene 3"),'),

    # test screen
    ('''			if (fpsText != null) fpsText.text = "FPS: --";
			if (sceneInfoText != null) sceneInfoText.text = "Initializing 3DMork...";''',
     '''			liveFps = -1;
			currentSceneName = "";
			RefreshTestTexts();'''),

    ('''					if (fpsText != null) fpsText.text = "FPS: " + Mathf.RoundToInt(liveFps).ToString();''',
     '''					liveFps = Mathf.RoundToInt(liveFps);
					RefreshTestTexts();'''),

    ('''				if (sceneInfoText != null) sceneInfoText.text = seg.name;''',
     '''				currentSceneName = seg.name;
				RefreshTestTexts();'''),

    ('''						if (i == 3)
							text_marks[i].text = (v / 100f).ToString("0.0") + " FPS";''',
     '''						if (i == 3)
							text_marks[i].text = Format("3DMork fps", (v / 100f).ToString("0.0"));'''),
]

# helpers and the language hook, inserted once
HELPERS = '''
		/// <summary>Перевод строки приложения, ключ берётся из Resources/Translate.txt.</summary>
		private static string Tr(string key)
		{
			return Localization.GetText(key);
		}

		/// <summary>Перевод строки с одним подставляемым значением.</summary>
		private static string Format(string key, object value)
		{
			string pattern = Localization.GetText(key);
			return pattern.Contains("{0}") ? string.Format(pattern, value) : pattern;
		}

		/// <summary>Перевод строки с тремя подставляемыми значениями.</summary>
		private static string Format(string key, object a, object b, object c)
		{
			string pattern = Localization.GetText(key);
			return pattern.Contains("{0}") ? string.Format(pattern, a, b, c) : pattern;
		}

		private void RefreshTestTexts()
		{
			if (fpsText != null)
			{
				fpsText.text = Format("3DMork fps", liveFps >= 0 ? liveFps.ToString() : "--");
			}

			if (sceneInfoText != null)
			{
				sceneInfoText.text = string.IsNullOrEmpty(currentSceneName)
					? Tr("3DMork initialising")
					: currentSceneName;
			}
		}

		/// <summary>Смена языка перерисовывает подписи, не прерывая тест.</summary>
		private void OnLanguageChanged()
		{
			if (startPanel != null && startPanel.activeSelf)
			{
				var snapshot = CollectHardware();
				RefreshHardwareSummary(snapshot);
				RefreshLeaderboard(snapshot);
				RefreshHistory();
			}

			RefreshTestTexts();
		}
'''

FIELD = '''		private bool isAdditiveSceneLoaded;
		private int liveFps = -1;
		private string currentSceneName = string.Empty;
'''
FIELD_OLD = '''		private bool isAdditiveSceneLoaded;
'''

HOOKS = [
    ("\t\t\tWireButtons();\n\t\t\tShowStartScreen();",
     "\t\t\tWireButtons();\n\t\t\tLocalization.LanguageChanged += OnLanguageChanged;\n\t\t\tShowStartScreen();"),
    ('''		private void OnDestroy()
		{
			if (benchmarkCoroutine != null)''',
     '''		private void OnDestroy()
		{
			Localization.LanguageChanged -= OnLanguageChanged;
			if (benchmarkCoroutine != null)'''),
]


def main():
    text = open(SOURCE, encoding="utf-8").read()
    if MARKER in text:
        print("ThreeDMork.cs is already localized")
        return
    missing = [old for old, _ in REPLACEMENTS + HOOKS if old not in text]
    if missing:
        raise SystemExit("source drift, %d snippets not found:\n%s" % (len(missing), "\n---\n".join(missing[:3])))
    for old, new in REPLACEMENTS + HOOKS:
        text = text.replace(old, new, 1)
    text = text.replace(FIELD_OLD, FIELD, 1)
    marker = "\t\tprivate GameObject startPanel;"
    text = text.replace(marker, MARKER + "\n" + marker, 1)
    text = text.replace("\t\tprivate void WireButtons()", HELPERS + "\n\t\tprivate void WireButtons()", 1)
    open(SOURCE, "w", encoding="utf-8").write(text)
    print("ThreeDMork.cs localized: %d strings, window %s" % (len(REPLACEMENTS), WINDOW))


if __name__ == "__main__":
    main()
