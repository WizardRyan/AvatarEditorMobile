using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 1. Define the safety methods
public enum RemovalMethod 
{ 
    Destroy, 
    Disable, 
    ScaleToZero 
}

// 2. Configuration class for the Inspector
[System.Serializable]
public class UIRemovalTarget
{
    [Tooltip("The name of the parent object holding the elements.")]
    public string parentContainerName;
    
    [Tooltip("The exact name of the clone/element you want to remove.")]
    public string elementToRemove;
    
    [Tooltip("How this specific element should be removed.")]
    public RemovalMethod removalMethod = RemovalMethod.ScaleToZero;
    
    [HideInInspector] 
    public Transform cachedParent = null;
}

// 3. The Manager Class
public class MultiUIElementRemover : MonoBehaviour
{
    [Header("Settings")]
    public float checkInterval = 1.0f;

    [Header("Elements to Remove")]
    public List<UIRemovalTarget> targets = new List<UIRemovalTarget>();

    void Start()
    {
        StartCoroutine(PollAndRemoveRoutine());
    }

    private IEnumerator PollAndRemoveRoutine()
    {
        while (true)
        {
            foreach (var target in targets)
            {
                // 1. Locate the parent container if we haven't yet
                if (target.cachedParent == null)
                {
                    GameObject parentObj = GameObject.Find(target.parentContainerName);
                    if (parentObj != null) 
                    {
                        target.cachedParent = parentObj.transform;
                    }
                }

                // 2. Search its children for the target element
                if (target.cachedParent != null)
                {
                    // Passing 'true' includes inactive children in the search
                    Transform[] children = target.cachedParent.GetComponentsInChildren<Transform>(true);

                    foreach (Transform child in children)
                    {
                        if (child != null && child.name == target.elementToRemove)
                        {
                            RemoveElement(child.gameObject, target.removalMethod);
                        }
                    }
                }
            }

            yield return new WaitForSeconds(checkInterval);
        }
    }

    private void RemoveElement(GameObject targetObj, RemovalMethod method)
    {
        Debug.Log($"[UI Remover] Removing {targetObj.name} using {method}");

        switch (method)
        {
            case RemovalMethod.Destroy:
                // Nukes the object. Can cause errors in the original game's code.
                Destroy(targetObj);
                break;

            case RemovalMethod.Disable:
                // Turns the object off and renames it so we don't process it again.
                targetObj.SetActive(false);
                targetObj.name = targetObj.name + "_Removed";
                break;

            case RemovalMethod.ScaleToZero:
                // The safest method. It stays active but becomes invisible and unclickable.
                targetObj.transform.localScale = Vector3.zero;
                targetObj.name = targetObj.name + "_Removed";
                break;
        }
    }
}