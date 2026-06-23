using System;
using System.Collections;

public class CatchableEnumerator : IEnumerator
{
	private IEnumerator enumerator;

	private Action exceptionCallback;

	private bool subExceptionCatched;

	public CatchableEnumerator(IEnumerator enumerator, Action exceptionCallback = null)
	{
		this.enumerator = enumerator;
		this.exceptionCallback = exceptionCallback;
	}

	public void BindExceptionCallback(Action exceptionCallback)
	{
		this.exceptionCallback = exceptionCallback;
	}

	public object Current => enumerator.Current;

	public bool MoveNext()
	{
		if (subExceptionCatched) return false;
		try
		{
			return enumerator.MoveNext();
		}
		catch
		{
			subExceptionCatched = true;
			exceptionCallback?.Invoke();
			return false;
		}
	}

	private void SubExceptionCall()
	{
		subExceptionCatched = true;
	}

	public void Reset()
	{
		enumerator.Reset();
	}

}
