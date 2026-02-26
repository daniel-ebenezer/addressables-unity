using UnityEngine;
using System;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "AddressablesConfig", menuName = "Addressables/Config")]
public class AddressablesConfig : ScriptableObject
{
    [Serializable]
    public class CategoryConfig
    {
        public string categoryName;          
        public string addressSuffix; 
        public List<string> baseKeys;        
    }

    [Serializable]
    public class VariantConfig
    {
        public string baseKey;               
        public string labelA;
        public string labelB;
    }

    [Header("Category Settings")]
    public List<CategoryConfig> categories = new List<CategoryConfig>();

    [Header("A/B Variant Settings")]
    public List<VariantConfig> abVariants = new List<VariantConfig>();

    [Header("Global Defaults")]
    public string defaultAddressSuffix;
    public string defaultLabelA;
    public string defaultLabelB;
}