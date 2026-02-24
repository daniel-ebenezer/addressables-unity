using UnityEngine;
using System;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "AddressablesConfig", menuName = "Addressables/Config")]
public class AddressablesConfig : ScriptableObject
{
    [Serializable]
    public class CategoryConfig
    {
        public string categoryName;          // "Vehicles", "Trees", "Props"
        public string addressSuffix = "_Remote"; // customizable per category
        public List<string> baseKeys;        // "Vehicles/Sedan", "Vehicles/Truck", ...
    }

    [Serializable]
    public class VariantConfig
    {
        public string baseKey;               // e.g. "Vehicles/Sedan"
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