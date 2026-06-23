using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class TextAnimation : MonoBehaviour
{
    private Text text;
    private string oldText;

    private void Awake()
    {
        text = GetComponent<Text>();
        oldText = text.text;
    }

    private void OnEnable()
    {
        StartCoroutine(Animation());
    }

    public void ResetText()
    {
        if (text != null)
        {
            oldText = text.text;
        }
        else
        {
            text = GetComponent<Text>();
            oldText = text.text;
        }
    }

    private IEnumerator Animation(){
        string src=oldText;text.text="";float delay=(src!=null&&src.Length<6)?Random.Range(0.03f,0.06f):Random.Range(0.02f,0.04f);
        while(text.text.Length<(src?.Length??0)){
            text.text=src.Substring(0,text.text.Length+1);
            yield return new WaitForSeconds(delay);
        }
    }
}
