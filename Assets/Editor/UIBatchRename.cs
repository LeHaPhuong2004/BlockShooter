using UnityEngine;
using UnityEditor;
using TMPro;
using UnityEngine.UI;
using UnityEditor.Events;
using System.Text.RegularExpressions;

using System.Collections.Generic;
using System.Text;
using System;
using System.Linq;
//using System.Diagnostics;
//using System.Numerics;


public class UIBatchRename
{
    [MenuItem("Tools/Reset user data - DELETE USER DATA")]
    static void ResetUserData()
    {
        PlayerPrefs.DeleteAll();
    }
}