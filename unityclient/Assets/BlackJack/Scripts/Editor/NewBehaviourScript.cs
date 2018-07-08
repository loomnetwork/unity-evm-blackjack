using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class NewBehaviourScript : MonoBehaviour {
    [MenuItem("Tools/teststs")]
    public static void gg()
    {
        Debug.Log(Selection.activeObject);
    }
}
