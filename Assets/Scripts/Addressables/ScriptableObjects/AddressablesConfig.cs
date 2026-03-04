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
        public string addressSuffix = "_Remote";
        public List<string> baseKeys = new List<string>();
    }

    [Serializable]
    public class VariantConfig
    {
        public string baseKey;
        public string remoteAddressA = "";
        public string remoteAddressB = "";
        public string labelA = "Variant_A";
        public string labelB = "Variant_B";
    }

    [Header("Category Settings")]
    public List<CategoryConfig> categories = new List<CategoryConfig>();

    [Header("A/B Variant Settings")]
    public List<VariantConfig> abVariants = new List<VariantConfig>();

    [Header("Global Defaults")]
    public string defaultAddressSuffix = "_Remote";
    public string defaultLabelA = "Variant_A";
    public string defaultLabelB = "Variant_B";
}