using UnityEngine;
using System;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "LocalContentRegistry", menuName = "ContentManagement/Local Content Registry", order = 1)]
public class LocalContentRegistry : ScriptableObject
{       
    [Serializable]
    public class Entry
    {
        public string RemoteKey = "";
        public string Category = ""; 
    }

    public List<Entry> entries = new List<Entry>();

}