using UnityEngine;
using UnityEngine.UI;
using System;

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Web.WebView2.Core;
#endif

public class HybridWebView : MonoBehaviour
{
    private string currentUrl;

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR

    private Form webForm;
    private WebView2 webView;

#elif UNITY_ANDROID && !UNITY_EDITOR

    private AndroidJavaObject webView;
    private AndroidJavaObject activity;

#endif

    public void Open(string url)
    {
        currentUrl = url;

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        OpenWindowsWebView(url);
#elif UNITY_ANDROID && !UNITY_EDITOR
        OpenAndroidWebView(url);
#else
        Application.OpenURL(url);
#endif
    }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR

    private async void OpenWindowsWebView(string url)
    {
        if (webForm != null)
            webForm.Close();

        webForm = new Form();
        webForm.Width = 1200;
        webForm.Height = 800;
        webForm.Text = "Browser";

        webView = new WebView2();
        webView.Dock = DockStyle.Fill;
        webForm.Controls.Add(webView);

        await webView.EnsureCoreWebView2Async(null);
        webView.CoreWebView2.Navigate(url);

        webForm.Show();
    }

#endif

#if UNITY_ANDROID && !UNITY_EDITOR

    private void OpenAndroidWebView(string url)
    {
        using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        {
            activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

            activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
            {
                webView = new AndroidJavaObject("android.webkit.WebView", activity);
                webView.Call("getSettings").Call("setJavaScriptEnabled", true);
                webView.Call("loadUrl", url);

                activity.Call("setContentView", webView);
            }));
        }
    }

#endif

    public void Close()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        if (webForm != null)
            webForm.Close();
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
        if (activity != null)
            activity.Call("recreate");
#endif
    }
}