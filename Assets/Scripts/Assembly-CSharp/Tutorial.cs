using System.Linq;
using PC.Component;
using PC.Component.Software;
using UnityEngine;
using UnityEngine.UI;

public class Tutorial : MonoBehaviour
{
    [SerializeField]
    private GameObject startButton;

    [SerializeField]
    private GameObject tutorial;

    [SerializeField]
    private Motherboard motherboard;

    [SerializeField]
    private Case targetCase;

    [SerializeField]
    private UnityEngine.Animator animator;

    [SerializeField]
    private Text lastText;

    [SerializeField]
    private Text currentText;

    private int currentStep;

    private bool tutorialStarted;

    private bool tutorialCompleted;

    public float updateInterval;

    private void Start()
    {
        InvokeRepeating(nameof(UpdateSteps), updateInterval, updateInterval);
    }

    private void UpdateSteps()
    {
        if (!tutorialStarted) return;
        if (tutorialCompleted)
        {
            ShowStep("You have completed the tutorial", 4);
            return;
        }
        bool caseOk_mobo = false;
        bool caseOk_drive = false;
        bool caseOk_psu = false;

        if (targetCase != null)
        {
            var mbSlot = targetCase.Motherboard;
            caseOk_mobo = mbSlot != null && mbSlot.Ready;

            var ext = targetCase.External;
            if (ext != null)
            {
                if (ext.drive != null)
                    caseOk_drive = ext.drive.Any(s => s != null && s.Ready);
                if (ext.supply != null)
                    caseOk_psu = ext.supply.Any(s => s != null && s.Ready);
            }
        }

        if (!caseOk_mobo || !caseOk_drive || !caseOk_psu)
        {
            string header = "<b>Step 1: Assembly</b>\nInstall the following components into the case (ATX)\n";
            string body =
                StepStatus(Localization.GetText("Motherboard") + " (Micro-ATX)", caseOk_mobo) +
                "" +
                StepStatus(Localization.GetText("Storage"), caseOk_drive) +
                "" +
                StepStatus(Localization.GetText("Power Supply"), caseOk_psu);

            ShowStep(header + body, 0);
            return;
        }

        bool mbOk_cpu = false, mbOk_ram = false, mbOk_cooler = false;
        var mbExt = motherboard != null ? motherboard.external : null;
        if (mbExt != null)
        {
            if (mbExt.CPU != null)
                mbOk_cpu = mbExt.CPU.Any(s => s != null && s.Ready);
            if (mbExt.RAM != null)
                mbOk_ram = mbExt.RAM.Any(s => s != null && s.Ready);
            if (mbExt.CPU_fan != null)
                mbOk_cooler = mbExt.CPU_fan.Any(s => s != null && s.Ready);
        }

        if (!mbOk_cpu || !mbOk_ram || !mbOk_cooler)
        {
            string header = "<b>Step 2: Assembly</b>\nInstall the following components onto the motherboard\n";
            string body =
                StepStatus(Localization.GetText("Processor"), mbOk_cpu) +
                "" +
                StepStatus(Localization.GetText("Memory"), mbOk_ram) +
                "" +
                StepStatus(Localization.GetText("Cooler"), mbOk_cooler);

            ShowStep(header + body, 1);
            return;
        }

        bool monitorConnected = false;
        bool usbInserted = false;

        if (motherboard != null)
        {
            monitorConnected = motherboard.monitor != null;

            if (mbExt != null && mbExt.usb != null)
                usbInserted = mbExt.usb.Any(s => s != null && s.Ready);
        }

        if (!monitorConnected || !usbInserted)
        {
            string header = "<b>Step 3: Boot Preparation</b>\n";
            string body =
                StepStatus("Connect the monitor to the motherboard", monitorConnected) +
                "" +
                StepStatus("Plug the flash drive with the installer into the motherboard", usbInserted);

            ShowStep(header + body, 2);
            return;
        }

        bool isRunning = motherboard != null && motherboard.Running;
        bool hasOsDisk = false;

        if (motherboard != null)
        {
            var storages = motherboard.GetHardwares(HardwareType.Drive);
            foreach (var s in storages.Cast<Storage>())
            {
                if (s == null) continue;

                bool hasOs = false;

                if (s.TryGetFile("System/boot.bin", out File file) && file != null)
                {
                    if (file.content == "pcos")
                    {
                        hasOs = true;
                    }
                }

                if (hasOs) { hasOsDisk = true; break; }
            }
        }

        if (isRunning && hasOsDisk)
        {
            tutorialCompleted = true;
            return;
        }

        string header4 = "<b>Step 4: Operating System Installation</b>\n";
        string body4 =
            StepStatus("Start the computer", isRunning) +
            "" +
            StepStatus("Install the operating system on an empty disk", hasOsDisk);

        ShowStep(header4 + body4, 3);
    }

    public void StartTutorial()
    {
        if (startButton != null)
            startButton.SetActive(false);

        if (tutorial != null)
            tutorial.SetActive(true);

        tutorialStarted = true;
    }

    public void ShowStep(string current, int step)
    {
        if (animator == null || currentText == null || lastText == null) return;

        var st = animator.GetCurrentAnimatorStateInfo(0);
        if (st.IsName("Wait"))
        {
            if (currentStep != step)
            {
                lastText.text = currentText.text;
                animator.SetTrigger("NextStep");
                currentStep = step;
            }

            currentText.text = current;
        }
    }

    private string StepStatus(string str, bool complete)
    {
        return complete
            ? string.Concat("<color=lime>✔️ ", str, "</color>\n")
            : string.Concat("● ", str, "\n");
    }
}
