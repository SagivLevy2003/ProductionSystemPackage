using System;
using UnityEngine;

[Serializable]
public class PrintMessageCompletionFactory : ICompletionFactory
{
    [field: SerializeField] public string Message { get; private set; }
    public ICompletionRuntime CreateRuntime() => new PrintMessageCompletionRuntime(Message);
}

public class PrintMessageCompletionRuntime : ICompletionRuntime
{
    private readonly string _message;

    public PrintMessageCompletionRuntime(string message)
    {
        _message = message;
    }

    public void OnCompletion(ProductionInstance instance)
    {
        Debug.Log(_message);
    }
}