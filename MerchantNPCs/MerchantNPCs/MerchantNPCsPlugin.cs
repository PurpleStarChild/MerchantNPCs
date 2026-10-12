using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Jotunn;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using MerchantNPCs.MerchantTrading;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace MerchantNPCs
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    // Jotunn 2.30.2 is the first version that supports the Valheim 1.0 / Deep North build menu
    // (usage tags and tag-based custom categories), so refuse to load on anything older.
    [BepInDependency(Jotunn.Main.ModGuid, MinJotunnVersion)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    internal partial class MerchantNPCsPlugin : BaseUnityPlugin
    {
        public const string PluginGUID = "ruijven.merchantnpcs";
        public const string PluginName = "MerchantNPCs";
        public const string PluginVersion = "1.0.3";

        // Minimum Jotunn version this mod is built and tested against (also set in Package/manifest.json)
        public const string MinJotunnVersion = "2.30.2";

        // Use this instance of the plugin for accessing the config and logger
        internal static MerchantNPCsPlugin Instance { get; private set; }

        // Use this for logging
        internal static new ManualLogSource Logger;

        // Asset bundle name
        private const string AssetBundleName = "merchant1_ru";

        // Asset bundle reference
        private AssetBundle _merchantAssetBundle;

        // Merchant pieces handler
        private MerchantPieces _merchantPieces;

        // Configuration entries
        private ConfigEntry<bool> _isModEnabled;
        
        // Sell percentage configuration (1-100%)
        public static ConfigEntry<int> SellPercentage;
        
        // Merchant cost configurations
        public Dictionary<string, MerchantCostConfig> MerchantCosts { get; private set; } = new Dictionary<string, MerchantCostConfig>();

        private void Awake()
        {
            // Set instance and logger
            Instance = this;
            Logger = base.Logger;

            // Initialize configuration
            _isModEnabled = Config.Bind("General", "IsModEnabled", true, "Enables or disables the mod");
            SellPercentage = Config.Bind("Trading", "SellPercentage", 50, new ConfigDescription("Percentage of buy price received when selling items (1-100)", new AcceptableValueRange<int>(1, 100)));

            if (!_isModEnabled.Value)
            {
                // Logger.LogInfo("Mod is disabled via configuration");
                return;
            }
            
            // Initialize merchant cost configurations
            InitializeMerchantCostConfigs();
            
            // Extract embedded configuration files
            ExtractEmbeddedConfigs();

            // Register events
            // Fires before every vanilla prefab copy (every menu start). AddMerchantPieces unsubscribes
            // itself because Jotunn caches custom pieces for the whole game session.
            PrefabManager.OnVanillaPrefabsAvailable += AddMerchantPieces;

            // Initialize the merchant trading system when the game world is loaded.
            // Unlike the prefab event above, this one is meant to stay subscribed: Jotunn raises it every time
            // a new ZoneSystem becomes available (each world load), and the trading data (item prefabs from the
            // new ZNetScene) must be rebuilt for every world.
            ZoneManager.OnVanillaLocationsAvailable += InitializeMerchantTrading;

            // Apply Harmony patches
            Harmony harmony = new Harmony(PluginGUID);
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            // Logger.LogInfo($"{PluginName} v{PluginVersion} loaded successfully!");
        }

        /// <summary>
        /// Initializes configuration entries for merchant build costs
        /// </summary>
        private void InitializeMerchantCostConfigs()
        {
            // Logger.LogInfo("Initializing merchant cost configurations");
            
            // Meadows Merchant
            var meadowsConfig = new MerchantCostConfig();
            meadowsConfig.AddResourceRequirement(Config, "Meadows Merchant", 1, "Wood", 50);
            meadowsConfig.AddResourceRequirement(Config, "Meadows Merchant", 2, "Stone", 50);
            meadowsConfig.AddResourceRequirement(Config, "Meadows Merchant", 3, "Flint", 25);
            MerchantCosts["Meadows"] = meadowsConfig;
            
            // Black Forest Merchant
            var blackForestConfig = new MerchantCostConfig();
            blackForestConfig.AddResourceRequirement(Config, "Black Forest Merchant", 1, "RoundLog", 20);
            blackForestConfig.AddResourceRequirement(Config, "Black Forest Merchant", 2, "TrollHide", 10);
            blackForestConfig.AddResourceRequirement(Config, "Black Forest Merchant", 3, "Copper", 10);
            blackForestConfig.AddResourceRequirement(Config, "Black Forest Merchant", 4, "TrophyEikthyr", 1);
            MerchantCosts["BlackForest"] = blackForestConfig;
            
            // Swamp Merchant
            var swampConfig = new MerchantCostConfig();
            swampConfig.AddResourceRequirement(Config, "Swamp Merchant", 1, "ElderBark", 20);
            swampConfig.AddResourceRequirement(Config, "Swamp Merchant", 2, "Guck", 10);
            swampConfig.AddResourceRequirement(Config, "Swamp Merchant", 3, "Iron", 10);
            swampConfig.AddResourceRequirement(Config, "Swamp Merchant", 4, "TrophyTheElder", 1);
            MerchantCosts["Swamp"] = swampConfig;
            
            // Mountains Merchant
            var mountainConfig = new MerchantCostConfig();
            mountainConfig.AddResourceRequirement(Config, "Mountains Merchant", 1, "FreezeGland", 10);
            mountainConfig.AddResourceRequirement(Config, "Mountains Merchant", 2, "WolfPelt", 10);
            mountainConfig.AddResourceRequirement(Config, "Mountains Merchant", 3, "Silver", 10);
            mountainConfig.AddResourceRequirement(Config, "Mountains Merchant", 4, "TrophyBonemass", 1);
            MerchantCosts["Mountain"] = mountainConfig;
            
            // Plains Merchant
            var plainsConfig = new MerchantCostConfig();
            plainsConfig.AddResourceRequirement(Config, "Plains Merchant", 1, "Barley", 10);
            plainsConfig.AddResourceRequirement(Config, "Plains Merchant", 2, "BlackMetalScrap", 10);
            plainsConfig.AddResourceRequirement(Config, "Plains Merchant", 3, "Needle", 5);
            plainsConfig.AddResourceRequirement(Config, "Plains Merchant", 4, "TrophyDragonQueen", 1);
            MerchantCosts["Plains"] = plainsConfig;
            
            // Mistlands Merchant
            var mistlandsConfig = new MerchantCostConfig();
            mistlandsConfig.AddResourceRequirement(Config, "Mistlands Merchant", 1, "RoyalJelly", 10);
            mistlandsConfig.AddResourceRequirement(Config, "Mistlands Merchant", 2, "YggdrasilWood", 20);
            mistlandsConfig.AddResourceRequirement(Config, "Mistlands Merchant", 3, "Carapace", 5);
            mistlandsConfig.AddResourceRequirement(Config, "Mistlands Merchant", 4, "TrophyGoblinKing", 1);
            MerchantCosts["Mistlands"] = mistlandsConfig;
            
            // Ashlands Merchant
            var ashlandsConfig = new MerchantCostConfig();
            ashlandsConfig.AddResourceRequirement(Config, "Ashlands Merchant", 1, "Blackwood", 20);
            ashlandsConfig.AddResourceRequirement(Config, "Ashlands Merchant", 2, "FlametalOreNew", 5);
            ashlandsConfig.AddResourceRequirement(Config, "Ashlands Merchant", 3, "Fiddleheadfern", 10);
            ashlandsConfig.AddResourceRequirement(Config, "Ashlands Merchant", 4, "TrophySeekerQueen", 1);
            MerchantCosts["Ashlands"] = ashlandsConfig;
            
            // Deep North Merchant
            var deepNorthConfig = new MerchantCostConfig();
            deepNorthConfig.AddResourceRequirement(Config, "Deep North Merchant", 1, "Frostwood", 20);
            deepNorthConfig.AddResourceRequirement(Config, "Deep North Merchant", 2, "MooseHide", 10);
            deepNorthConfig.AddResourceRequirement(Config, "Deep North Merchant", 3, "FrostCore", 10);
            deepNorthConfig.AddResourceRequirement(Config, "Deep North Merchant", 4, "TrophyFader", 1);
            MerchantCosts["DeepNorth"] = deepNorthConfig;
            
            // Logger.LogInfo($"Initialized {MerchantCosts.Count} merchant cost configurations");
        }

        private void AddMerchantPieces()
        {
            // Jotunn raises OnVanillaPrefabsAvailable before every vanilla prefab copy,
            // so unsubscribe to make sure the pieces are only registered once.
            PrefabManager.OnVanillaPrefabsAvailable -= AddMerchantPieces;

            try
            {
                // Load asset bundle
                LoadAssetBundle();

                if (_merchantAssetBundle == null)
                {
                    Logger.LogError("Failed to load asset bundle");
                    return;
                }

                // Register all materials, prefabs, and icons
                RegisterAllAssets();

                // Note: no custom piece table is needed. Merchants are added to the vanilla Hammer
                // with the "Merchants" category (see MerchantPieces), which Jotunn shows as its own
                // tag in the build menu.

                // Initialize the merchant pieces handler
                _merchantPieces = new MerchantPieces(_merchantAssetBundle, this);
                
                // Add all merchant pieces to the game
                _merchantPieces.AddAllPieces();
                
                // Logger.LogInfo("Merchant pieces added successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error adding merchant pieces: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void LoadAssetBundle()
        {
            try
            {
                // Load the embedded asset bundle
                _merchantAssetBundle = AssetUtils.LoadAssetBundleFromResources(AssetBundleName, Assembly.GetExecutingAssembly());
                
                if (_merchantAssetBundle != null)
                {
                    // Log all assets in the bundle for debugging
                    string[] assetNames = _merchantAssetBundle.GetAllAssetNames();
                    // Logger.LogInfo($"Asset bundle contains {assetNames.Length} assets:");
                    foreach (string assetName in assetNames)
                    {
                        // Logger.LogInfo($"- {assetName}");
                    }
                    
                    // Log all asset types for more detailed information
                    // Logger.LogInfo("Asset bundle contains the following asset types:");
                    
                    // Log GameObjects
                    string[] prefabNames = _merchantAssetBundle.GetAllAssetNames()
                        .Where(name => _merchantAssetBundle.LoadAsset<GameObject>(name) != null)
                        .ToArray();
                    // Logger.LogInfo($"GameObjects ({prefabNames.Length}): {string.Join(", ", prefabNames)}");
                    
                    // Log Sprites
                    string[] spriteNames = _merchantAssetBundle.GetAllAssetNames()
                        .Where(name => _merchantAssetBundle.LoadAsset<Sprite>(name) != null)
                        .ToArray();
                    // Logger.LogInfo($"Sprites ({spriteNames.Length}): {string.Join(", ", spriteNames)}");
                    
                    // Log Materials
                    string[] materialNames = _merchantAssetBundle.GetAllAssetNames()
                        .Where(name => _merchantAssetBundle.LoadAsset<Material>(name) != null)
                        .ToArray();
                    // Logger.LogInfo($"Materials ({materialNames.Length}): {string.Join(", ", materialNames)}");
                    
                    // Log Textures
                    string[] textureNames = _merchantAssetBundle.GetAllAssetNames()
                        .Where(name => _merchantAssetBundle.LoadAsset<Texture>(name) != null)
                        .ToArray();
                    // Logger.LogInfo($"Textures ({textureNames.Length}): {string.Join(", ", textureNames)}");
                    
                    // Log Animations
                    string[] animationNames = _merchantAssetBundle.GetAllAssetNames()
                        .Where(name => _merchantAssetBundle.LoadAsset<AnimationClip>(name) != null)
                        .ToArray();
                    // Logger.LogInfo($"Animations ({animationNames.Length}): {string.Join(", ", animationNames)}");
                    
                    // Logger.LogInfo("Asset bundle loaded successfully");
                }
                else
                {
                    Logger.LogError("Asset bundle is null after loading");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error loading asset bundle: {ex.Message}\n{ex.StackTrace}");
            }
        }

/// <summary>
/// Registers all materials, prefabs, and icons with the game
/// </summary>
private void RegisterAllAssets()
{
    try
    {
        if (_merchantAssetBundle == null)
        {
            Logger.LogError("Cannot register assets: Asset bundle is null");
            return;
        }

        // Get all asset names from the bundle
        string[] allAssetNames = _merchantAssetBundle.GetAllAssetNames();
        // Logger.LogInfo($"Preparing to register {allAssetNames.Length} assets from bundle");

        // Register all GameObjects/Prefabs
        int prefabCount = 0;
        foreach (string assetName in allAssetNames)
        {
            // Extract the asset name without path
            string fileName = Path.GetFileNameWithoutExtension(assetName);
            
            // Try to load as GameObject
            GameObject prefab = _merchantAssetBundle.LoadAsset<GameObject>(fileName);
            if (prefab != null)
            {
                try
                {
                    PrefabManager.Instance.AddPrefab(prefab);
                    // Logger.LogInfo($"Registered prefab: {fileName}");
                    prefabCount++;
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"Failed to register prefab {fileName}: {ex.Message}");
                }
            }
        }
        // Logger.LogInfo($"Registered {prefabCount} prefabs");

        // Load all Sprites (icons)
        int spriteCount = 0;
        foreach (string assetName in allAssetNames)
        {
            // Extract the asset name without path
            string fileName = Path.GetFileNameWithoutExtension(assetName);
            
            // Try to load as Sprite
            Sprite sprite = _merchantAssetBundle.LoadAsset<Sprite>(fileName);
            if (sprite != null)
            {
                // Sprites are automatically available through the asset bundle
                // We just need to ensure they're loaded
                // Logger.LogInfo($"Loaded sprite: {fileName}");
                spriteCount++;
            }
        }
        // Logger.LogInfo($"Loaded {spriteCount} sprites");

        // Load all Materials
        int materialCount = 0;
        foreach (string assetName in allAssetNames)
        {
            // Extract the asset name without path
            string fileName = Path.GetFileNameWithoutExtension(assetName);
            
            // Try to load as Material
            Material material = _merchantAssetBundle.LoadAsset<Material>(fileName);
            if (material != null)
            {
                // Materials are automatically available through the asset bundle
                // We just need to ensure they're loaded
                // Logger.LogInfo($"Loaded material: {fileName}");
                materialCount++;
            }
        }
        // Logger.LogInfo($"Loaded {materialCount} materials");

        // Load all Textures
        int textureCount = 0;
        foreach (string assetName in allAssetNames)
        {
            // Extract the asset name without path
            string fileName = Path.GetFileNameWithoutExtension(assetName);
            
            // Try to load as Texture
            Texture texture = _merchantAssetBundle.LoadAsset<Texture>(fileName);
            if (texture != null)
            {
                // Textures are automatically available through the asset bundle
                // We just need to ensure they're loaded
                // Logger.LogInfo($"Loaded texture: {fileName}");
                textureCount++;
            }
        }
        // Logger.LogInfo($"Loaded {textureCount} textures");

        // Load all Animation Clips
        int animationCount = 0;
        foreach (string assetName in allAssetNames)
        {
            // Extract the asset name without path
            string fileName = Path.GetFileNameWithoutExtension(assetName);
            
            // Try to load as AnimationClip
            AnimationClip animation = _merchantAssetBundle.LoadAsset<AnimationClip>(fileName);
            if (animation != null)
            {
                // Animation clips are automatically available through the asset bundle
                // We just need to ensure they're loaded
                // Logger.LogInfo($"Loaded animation: {fileName}");
                animationCount++;
            }
        }
        // Logger.LogInfo($"Loaded {animationCount} animations");

        // Logger.LogInfo("All assets registered successfully");
    }
    catch (Exception ex)
    {
        Logger.LogError($"Error registering assets: {ex.Message}\n{ex.StackTrace}");
    }
}

private void InitializeMerchantTrading()
{
    try
    {
        // Initialize the merchant manager
        if (MerchantManager.Instance != null)
        {
            MerchantManager.Instance.Initialize();
            // Logger.LogInfo("Merchant manager initialized successfully");
        }
        else
        {
            Logger.LogError("MerchantManager instance is null");
        }
        
        // Initialize the merchant UI instance (but don't create the UI elements yet)
        try
        {
            MerchantUI.Initialize();
            // Logger.LogInfo("MerchantUI instance initialized - UI will be created on demand");
        }
        catch (Exception uiEx)
        {
            Logger.LogError($"Error initializing merchant UI instance: {uiEx.Message}\n{uiEx.StackTrace}");
            // Logger.LogInfo("MerchantUI instance initialization failed, but the mod will continue to function");
        }
        
        // Logger.LogInfo("Merchant trading system initialized successfully");
    }
    catch (Exception ex)
    {
        Logger.LogError($"Error initializing merchant trading system: {ex.Message}\n{ex.StackTrace}");
    }
}
    }
}
