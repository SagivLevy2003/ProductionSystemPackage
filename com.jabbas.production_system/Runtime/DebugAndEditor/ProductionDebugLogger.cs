using System;
using UnityEngine;

public static class ProductionDebugLogger
{
    public static void LogMessage(object source, GameObject instance, string message, bool isWarning = false) //For non-monobehaviours
    {
        string typeName = source is Type t ? t.Name : source.GetType().Name;
        string instanceName = instance ? instance.name : "<color=red>Null Instance</color>";

        PrintLog(typeName, instanceName, message, isWarning);
    }

    public static void LogMessage(object source, string message, bool isWarning = false) //For non-monobehaviours
    {
        string typeName = source is Type t ? t.Name : source.GetType().Name;

        PrintLog(typeName, "", message, isWarning);
    }

    public static void LogMessage(MonoBehaviour source, string message, bool isWarning = false) //For monobehaviours
    {
        string sourceTypeName = source ? source.GetType().Name : "<color=red>Null Source</color>";
        string sourceInstanceName = source ? source.gameObject.name : "<color=red>Null Instance</color>";

        PrintLog(sourceTypeName, sourceInstanceName, message, isWarning);
    }

    private static void PrintLog(string sourceStr, string instanceStr, string message, bool isWarning)
    {
        sourceStr = $"<color=cyan>{sourceStr}</color>";
        instanceStr = !string.IsNullOrEmpty(instanceStr) ? $"<color=white> : </color><color=cyan>{instanceStr}</color>" : "";

        if (isWarning) Debug.LogWarning($"{sourceStr}{instanceStr} {message}");
        else Debug.Log($"{sourceStr}{instanceStr} {message}");
    }
}