#if UNITY_EDITOR
using UnityEditor;
using FishNet.Object;
using UnityEngine;
using Jabbas.ProductionSystem;

[CustomEditor(typeof(ProductionHandler), true)]
public class ProductionHandlerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var handler = (ProductionHandler)target;
        var go = handler.gameObject;

        bool isTopMost = IsTopMostNetworkObject(go);
        if (isTopMost)
        {
            EditorGUILayout.HelpBox(
                "⚠️ This component should be on a NESTED NetworkObject.\n" +
                "Place it under a parent that also has a NetworkObject.",
                MessageType.Warning);
        }

        // Draw the rest of the inspector
        base.OnInspectorGUI();
    }

    private static bool IsTopMostNetworkObject(GameObject go)
    {
        // If this object doesn't have a NetworkObject (shouldn't happen due to RequireComponent),
        // still treat as top-most (show warning) to be safe.
        if (go.GetComponent<NetworkObject>() == null)
            return true;

        Transform p = go.transform.parent;
        while (p != null)
        {
            if (p.GetComponent<NetworkObject>() != null)
                return false; // ancestor has NetworkObject -> nested correctly
            p = p.parent;
        }
        return true; // no ancestor NetworkObject -> top-most
    }
}
#endif