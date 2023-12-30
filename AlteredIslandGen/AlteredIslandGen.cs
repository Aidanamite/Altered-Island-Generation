using UnityEngine;
using HarmonyLib;
using System.Collections.Generic;
using UnityEngine.UI;
using HMLLibrary;
using RaftModLoader;

public class AlteredIslandGen : Mod
{
    Harmony harmony;
    static bool started;
    public override bool CanUnload(ref string message)
    {
        if (!Raft_Network.InMenuScene)
        {
            message = "Mod must be unloaded on the main menu";
            return false;
        }
        return base.CanUnload(ref message);
    }
    public void Start()
    {
        started = false;
        if (!Raft_Network.InMenuScene)
        {
            modlistEntry.modinfo.unloadBtn.GetComponent<Button>().onClick.Invoke();
            throw new ModLoadException("Mod must be loaded on the main menu");
        }
        started = true;
        harmony = new Harmony("com.aidanamite.AlteredIslandGen");
        harmony.PatchAll();
        Log("Mod has been loaded!");
    }

    public void OnModUnload()
    {
        if (!started)
            return;
        harmony.UnpatchAll(harmony.Id);
        Log("Mod has been unloaded!");
    }
}

class ModLoadException : System.Exception
{
    public ModLoadException(string message) : base(message) { }
}

[HarmonyPatch(typeof(Landmark), "Initialize")]
public class Patch_IslandSpawn
{
    static GameObject titaniumModel = null;
    static GameObject TitaniumModel // Gets a titanium model either from cache or resource search
    {
        get
        {
            if (!titaniumModel)
                foreach (var p in Resources.FindObjectsOfTypeAll<MeshRenderer>())
                    if (p.name == "Raw_Titanium")
                    {
                        titaniumModel = p.gameObject;
                        break;
                    }
            return titaniumModel;
        }
    }
    static List<GameObject> plasticModels = new List<GameObject>();
    static List<GameObject> PlasticModels // Gets a set of plastic models either from cache or resource search
    {
        get
        {
            if (plasticModels.Count > 0 && !plasticModels[0]) // Checks if the cache contains items and the first item is null or destroyed
                plasticModels.Clear();
            if (plasticModels.Count == 0)
                foreach (var p in Resources.FindObjectsOfTypeAll<MeshRenderer>())
                    if (p.name.StartsWith("Pickup_Floating_Plastic") && !p.name.EndsWith("(Clone)"))
                        plasticModels.Add(p.gameObject);
            return plasticModels;
        }
    }
    static SO_ItemYield titaniumYield;
    static SO_ItemYield plasticYield;
    // Replaces a selection of copper and iron with titanium and scrap with plastic. This uses the island's unique index as the seed for the random so every player has the same set of items replaced
    static void Prefix(ref Landmark __instance, bool ___initialized) 
    {
        if (!___initialized)
        {
            bool flagT = TitaniumModel;
            if (!flagT)
                Debug.LogError("Could not find titanium ore model");
            bool flagP = PlasticModels.Count > 0;
            if (!flagP)
                Debug.LogError("Could not find plastic model");
            if (!flagT && !flagP)
                return;
            var rand = new System.Random(__instance.uniqueLandmarkIndex > int.MaxValue ? (int)(__instance.uniqueLandmarkIndex - (long)uint.MaxValue - 1) : (int)__instance.uniqueLandmarkIndex);
            var l = new List<LandmarkItem_PickupItem>();
            __instance.transform.GetComponentsInChildrenRecursively(ref l);
            var titanium = ItemManager.GetItemByName("TitaniumOre");
            var plastic = ItemManager.GetItemByName("Plastic");
            foreach (var item in l)
                if (flagT && (item.name.StartsWith("Pickup_Landmark_Iron") || item.name.StartsWith("Pickup_Landmark_Copper")) && rand.NextDouble() < 0.05)
                    ModifyPickup(__instance, item, "Titanium", TitaniumModel, ref titaniumYield, titanium);
                else if (flagP && item.name.StartsWith("Pickup_Landmark_Scrap") && item.name.Contains("OceanBottom") && rand.NextDouble() < 0.2)
                    ModifyPickup(__instance, item, "Plastic", PlasticModels[(int)(PlasticModels.Count * rand.NextDouble())], ref plasticYield, plastic);
        }
    }
    public static void ModifyPickup(Landmark island, LandmarkItem_PickupItem item, string name, GameObject newModel, ref SO_ItemYield yieldAsset, Item_Base loot)
    {
        var pickup = item.GetComponent<PickupItem>();
        if (yieldAsset == null)
        {
            yieldAsset = new SO_ItemYield();
            yieldAsset.name = "ItemYield_Pickup_Landmark_" + name;
            yieldAsset.yieldAssets = new List<Cost>()
            {
                new Cost(loot, 1)
            };
        }
        pickup.yieldHandler.yieldAsset = yieldAsset;
        pickup.yieldHandler.ResetYield();
        pickup.specificPickups = new Pickup_Specific[0];
        pickup.itemInstance = new ItemInstance();
        pickup.dropper = null;
        item.name = "Pickup_Landmark_" + name;
        var model = item.transform.Find("Model");
        model.GetComponent<MeshFilter>().mesh = newModel.GetComponent<MeshFilter>().mesh;
        model.GetComponent<MeshRenderer>().material = newModel.GetComponent<MeshRenderer>().material;
        var collider = item.GetComponent<BoxCollider>();
        var modelCollider = newModel.GetComponent<BoxCollider>();
        if (!modelCollider)
            modelCollider = newModel.GetComponentInChildren<BoxCollider>();
        if (modelCollider)
        {
            collider.size = modelCollider.size;
            collider.center = modelCollider.center;
        }
        var parent = island.transform.Find(name);
        if (parent == null)
        {
            parent = new GameObject(name).transform;
            parent.SetParent(island.transform, false);
        }
        item.transform.SetParent(parent, true);
    }
}