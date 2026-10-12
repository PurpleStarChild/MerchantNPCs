using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MerchantNPCs
{
    internal class MerchantPieces
    {
        // Reference to the asset bundle
        private readonly AssetBundle _assetBundle;

        // Reference to the plugin for configuration access
        private readonly MerchantNPCsPlugin _plugin;

        // List to keep track of added pieces
        private readonly List<CustomPiece> _addedPieces = new List<CustomPiece>();

        // Prefabs that already had their piece components set up. EnsurePieceComponents adds
        // components (e.g. a collider), so it must only run once per prefab instance.
        private static readonly HashSet<int> _preparedPrefabs = new HashSet<int>();

        // Constructor
        public MerchantPieces(AssetBundle assetBundle, MerchantNPCsPlugin plugin)
        {
            _assetBundle = assetBundle;
            _plugin = plugin;
        }

        /// <summary>
        /// Adds all merchant pieces to the game
        /// </summary>
        public void AddAllPieces()
        {
            try
            {
                if (_assetBundle == null)
                {
                    MerchantNPCsPlugin.Logger.LogError("Asset bundle is null, cannot add pieces");
                    return;
                }

                // Add each biome merchant
                AddMeadowsMerchant();
                AddBlackForestMerchant();
                AddSwampMerchant();
                AddMountainMerchant();
                AddPlainsMerchant();
                AddMistlandsMerchant();
                AddAshlandsMerchant();
                AddDeepNorthMerchant();

                MerchantNPCsPlugin.Logger.LogInfo($"Added {_addedPieces.Count} merchant pieces to the game");
            }
            catch (Exception ex)
            {
                MerchantNPCsPlugin.Logger.LogError($"Error adding merchant pieces: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Helper method to add a merchant piece to the game
        /// </summary>
        private void AddMerchantPiece(string prefabName, string pieceName, string description, List<RequirementConfig> requirements)
        {
            try
            {
                // Load the prefab from the asset bundle
                GameObject prefab = _assetBundle.LoadAsset<GameObject>(prefabName);
                
                if (prefab == null)
                {
                    MerchantNPCsPlugin.Logger.LogError($"Failed to load prefab {prefabName} from asset bundle");
                    return;
                }

                // Ensure the prefab has the necessary components for a piece
                EnsurePieceComponents(prefab);
                
                // Load the icon for this merchant
                // Get biome name from prefab name
                string biomeName = prefabName.Replace("Merch_ru", "");
        
                // Use the exact icon names provided
                Sprite icon = null;
                string iconName;
        
                // Special case for Mountains merchant - icon has an 's' at the end (MountainsIcon_ru)
                if (biomeName == "Mountain")
                {
                    iconName = "MountainsIcon_ru";
                }
                else
                {
                    iconName = biomeName + "Icon_ru";
                }
        
                icon = _assetBundle.LoadAsset<Sprite>(iconName);
                
                MerchantNPCsPlugin.Logger.LogInfo($"Trying to load icon {iconName} for {prefabName}");
                
                // If icon loading fails, try the texts_icon1 as fallback
                if (icon == null)
                {
                    icon = _assetBundle.LoadAsset<Sprite>("texts_icon1");
                    MerchantNPCsPlugin.Logger.LogWarning($"Using fallback icon for {prefabName}");
                }
                
                if (icon == null)
                {
                    MerchantNPCsPlugin.Logger.LogWarning($"Failed to load any icon for {prefabName}");
                }

                // Create piece config
                PieceConfig pieceConfig = new PieceConfig
                {
                    Name = pieceName,
                    Description = description,
                    PieceTable = PieceTables.Hammer, // Use the vanilla hammer
                    // Pieces of a custom category get their own "Merchants" tag button in the build menu
                    Category = "Merchants",
                    // Set the usage tags explicitly instead of letting Jotunn guess them
                    Usage = new[] { PieceUsages.Crafting },
                    Requirements = requirements.ToArray(), // Convert List to array
                    Icon = icon // Set the icon for the piece
                };

                // Create custom piece
                CustomPiece customPiece = new CustomPiece(prefab, false, pieceConfig);
                
                // Add the piece to the game
                PieceManager.Instance.AddPiece(customPiece);
                
                // Keep track of added pieces
                _addedPieces.Add(customPiece);
                
                MerchantNPCsPlugin.Logger.LogInfo($"Added merchant piece: {pieceName}");
            }
            catch (Exception ex)
            {
                MerchantNPCsPlugin.Logger.LogError($"Error adding merchant piece {prefabName}: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Ensures the prefab has all the necessary components to function as a piece and be immune to damage
        /// </summary>
        private void EnsurePieceComponents(GameObject prefab)
        {
            if (!_preparedPrefabs.Add(prefab.GetInstanceID()))
            {
                return;
            }

            try
            {
                // Make sure the prefab has a Piece component to handle building requirements
                Piece pieceComponent = prefab.GetComponent<Piece>();
                if (pieceComponent == null)
                {
                    pieceComponent = prefab.AddComponent<Piece>();
                    MerchantNPCsPlugin.Logger.LogInfo($"Added Piece component to {prefab.name}");
                }
                
                // Ensure the piece is configured to check for resources
                pieceComponent.m_resources = new Piece.Requirement[0]; // Will be overridden by PieceConfig
                pieceComponent.m_craftingStation = null; // No crafting station required
                pieceComponent.m_enabled = true;
                pieceComponent.m_category = Piece.PieceCategory.Misc;
                pieceComponent.m_canBeRemoved = true;
                pieceComponent.m_allowedInDungeons = false;
                
                // Make sure the prefab has a WearNTear component to handle damage
                WearNTear wearNTear = prefab.GetComponent<WearNTear>();
                if (wearNTear == null)
                {
                    wearNTear = prefab.AddComponent<WearNTear>();
                    MerchantNPCsPlugin.Logger.LogInfo($"Added WearNTear component to {prefab.name}");
                }

                // Make the piece immune to all damage
                wearNTear.m_noRoofWear = false;
                wearNTear.m_noSupportWear = false;
                wearNTear.m_supports = true;
                wearNTear.m_materialType = WearNTear.MaterialType.Stone; // Stone is more durable
                wearNTear.m_health = 10000000f; // High health value
                wearNTear.m_damages.m_blunt = 0f;
                wearNTear.m_damages.m_slash = 0f;
                wearNTear.m_damages.m_pierce = 0f;
                wearNTear.m_damages.m_chop = 0f;
                wearNTear.m_damages.m_pickaxe = 0f;
                wearNTear.m_damages.m_fire = 0f;
                wearNTear.m_damages.m_frost = 0f;
                wearNTear.m_damages.m_lightning = 0f;
                wearNTear.m_damages.m_poison = 0f;
                wearNTear.m_damages.m_spirit = 0f;

                // Update additional piece properties
                pieceComponent.m_groundPiece = false; // Can be placed on other structures
                pieceComponent.m_groundOnly = false; // Can be placed on other structures
                pieceComponent.m_cultivatedGroundOnly = false; // Can be placed on any ground
                pieceComponent.m_waterPiece = false; // Not a water piece
                pieceComponent.m_noInWater = true; // Cannot be placed in water
                pieceComponent.m_notOnWood = false; // Can be placed on wood
                pieceComponent.m_notOnTiltingSurface = false; // Can be placed on tilting surface
                // Note: m_destroyedEffect is not available in this version of the Piece class
                pieceComponent.m_primaryTarget = false; // Not a primary target
                
                // Ensure piece requirements are properly initialized to prevent IndexOutOfRangeException
                if (pieceComponent.m_resources == null || pieceComponent.m_resources.Length == 0)
                {
                    // Initialize with a minimal empty requirement array to prevent null reference
                    pieceComponent.m_resources = new Piece.Requirement[0];
                }
                
                // Set place effect properly
                if (ZNetScene.instance != null)
                {
                    GameObject effectPrefab = ZNetScene.instance.GetPrefab("sfx_build_hammer_stone");
                    if (effectPrefab != null)
                    {
                        pieceComponent.m_placeEffect = new EffectList { m_effectPrefabs = new EffectList.EffectData[] 
                        { 
                            new EffectList.EffectData { m_prefab = effectPrefab } 
                        }};
                    }
                }
                
                pieceComponent.m_clipEverything = true; // Prevents clipping with other objects

                // Make sure the prefab has a ZNetView component properly configured
                ZNetView zNetView = prefab.GetComponent<ZNetView>();
                if (zNetView == null)
                {
                    zNetView = prefab.AddComponent<ZNetView>();
                    MerchantNPCsPlugin.Logger.LogInfo($"Added ZNetView component to {prefab.name}");
                }
        
                // Configure the ZNetView to ensure proper network synchronization and persistence
                zNetView.m_persistent = true; // Make merchants persist between game sessions
                zNetView.m_type = ZDO.ObjectType.Default; // Default object type
                zNetView.m_syncInitialScale = true; // Sync the initial scale
        
                // Ensure the ZNetView has the WearNTear component registered
                if (wearNTear != null && !zNetView.m_syncInitialScale)
                {
                    // This helps with proper destruction
                    zNetView.m_syncInitialScale = true;
                }

                // Add a capsule collider to prevent walking through the merchant
                // Remove any existing trigger colliders first
                CapsuleCollider[] existingColliders = prefab.GetComponents<CapsuleCollider>();
                foreach (CapsuleCollider col in existingColliders)
                {
                    if (col.isTrigger)
                    {
                        MerchantNPCsPlugin.Logger.LogInfo($"Found trigger collider on {prefab.name}, not removing it");
                        // Keep trigger colliders for interaction
                    }
                }
                
                // Add a solid capsule collider for physical collision
                CapsuleCollider solidCollider = prefab.AddComponent<CapsuleCollider>();
                solidCollider.center = new Vector3(0, 1f, 0); // Position at character's center
                solidCollider.radius = 0.4f;  // Slightly narrower than default
                solidCollider.height = 1.8f;  // Slightly shorter than default
                solidCollider.direction = 1;  // Y-axis (up/down)
                solidCollider.isTrigger = false;  // Make it a solid collider
                
                MerchantNPCsPlugin.Logger.LogInfo($"Added solid capsule collider to {prefab.name}");

                // Add MerchantBehavior component for interaction
                MerchantBehavior merchantBehavior = prefab.GetComponent<MerchantBehavior>();
                if (merchantBehavior == null)
                {
                    merchantBehavior = prefab.AddComponent<MerchantBehavior>();
                    MerchantNPCsPlugin.Logger.LogInfo($"Added MerchantBehavior component to {prefab.name}");
                    
                    // Set merchant type based on prefab name
                    string merchantType = "";
                    if (prefab.name.Contains("MeadowsMerch"))
                    {
                        merchantType = "Meadows";
                    }
                    else if (prefab.name.Contains("BlackforestMerch"))
                    {
                        merchantType = "BlackForest";
                    }
                    else if (prefab.name.Contains("SwampMerch"))
                    {
                        merchantType = "Swamp";
                    }
                    else if (prefab.name.Contains("MountainMerch"))
                    {
                        merchantType = "Mountain";
                    }
                    else if (prefab.name.Contains("PlainsMerch"))
                    {
                        merchantType = "Plains";
                    }
                    else if (prefab.name.Contains("MistlandsMerch"))
                    {
                        merchantType = "Mistlands";
                    }
                    else if (prefab.name.Contains("AshlandsMerch"))
                    {
                        merchantType = "Ashlands";
                    }
                    else if (prefab.name.Contains("DeepNorthMerch"))
                    {
                        merchantType = "DeepNorth";
                    }
                    
                    // Use reflection to set the private _merchantType field
                    var field = typeof(MerchantBehavior).GetField("_merchantType", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (field != null)
                    {
                        field.SetValue(merchantBehavior, merchantType);
                    }
                }
            }
            catch (Exception ex)
            {
                MerchantNPCsPlugin.Logger.LogError($"Error ensuring piece components for {prefab.name}: {ex.Message}\n{ex.StackTrace}");
            }
        }

        // Meadows Merchant - Available from the start
        private void AddMeadowsMerchant()
        {
            List<RequirementConfig> requirements = GetRequirementsFromConfig("Meadows",
                ("$item_wood", 50),
                ("$item_stone", 50),
                ("$item_flint", 25)
            );

            AddMerchantPiece(
                "MeadowsMerch_ru", // Using new biome-specific prefab
                "Meadows Merchant",
                "A merchant selling basic meadows resources and tools",
                requirements
            );
        }

        // Black Forest Merchant - Requires Eikthyr Trophy
        private void AddBlackForestMerchant()
        {
            List<RequirementConfig> requirements = GetRequirementsFromConfig("BlackForest",
                ("RoundLog", 20),
                ("TrollHide", 10),
                ("Copper", 10),
                ("TrophyEikthyr", 1)
            );

            AddMerchantPiece(
                "BlackforestMerch_ru", // Using new biome-specific prefab
                "Black Forest Merchant",
                "A merchant selling Black Forest resources and bronze equipment",
                requirements
            );
        }

        /// <summary>
        /// Helper method to create requirements from configuration
        /// </summary>
        private List<RequirementConfig> GetRequirementsFromConfig(string merchantType, params (string itemType, int defaultAmount)[] defaultRequirements)
        {
            // Get the configuration for this merchant
            var config = _plugin.MerchantCosts.TryGetValue(merchantType, out var merchantConfig) 
                ? merchantConfig 
                : null;
                
            List<RequirementConfig> requirements = new List<RequirementConfig>();
            
            // If we have a valid config, use it
            if (config != null)
            {
                // Get all configured requirements
                var configuredRequirements = config.GetRequirements();
                
                // Add each configured requirement
                foreach (var (itemType, amount) in configuredRequirements)
                {
                    requirements.Add(new RequirementConfig
                    {
                        Item = itemType,
                        Amount = amount,
                        Recover = true,
                        AmountPerLevel = 0
                    });
                }
            }
            
            // If no requirements were configured, use defaults
            if (requirements.Count == 0)
            {
                foreach (var (itemType, defaultAmount) in defaultRequirements)
                {
                    requirements.Add(new RequirementConfig
                    {
                        Item = itemType,
                        Amount = defaultAmount,
                        Recover = true,
                        AmountPerLevel = 0
                    });
                }
            }
            
            return requirements;
        }
        
        // Swamp Merchant - Requires Elder Trophy
        private void AddSwampMerchant()
        {
            List<RequirementConfig> requirements = GetRequirementsFromConfig("Swamp",
                ("$item_elderbark", 20),
                ("$item_guck", 10),
                ("$item_iron", 10),
                ("$item_trophy_elder", 1)
            );

            AddMerchantPiece(
                "SwampMerch_ru", // Using new biome-specific prefab
                "Swamp Merchant",
                "A merchant selling swamp resources and iron equipment",
                requirements
            );
        }

        // Plains Merchant - Requires Moder Trophy
        private void AddPlainsMerchant()
        {
            List<RequirementConfig> requirements = GetRequirementsFromConfig("Plains",
                ("Barley", 10),
                ("BlackMetalScrap", 10),
                ("Needle", 5),
                ("TrophyDragonQueen", 1)
            );

            AddMerchantPiece(
                "PlainsMerch_ru", // Using new biome-specific prefab
                "Plains Merchant",
                "A merchant selling plains resources and black metal equipment",
                requirements
            );
        }

        // Mountains Merchant - Requires Bonemass Trophy
        private void AddMountainMerchant()
        {
            List<RequirementConfig> requirements = GetRequirementsFromConfig("Mountains",
                ("FreezeGland", 10),
                ("WolfPelt", 10),
                ("Silver", 10),
                ("TrophyBonemass", 1)
            );

            AddMerchantPiece(
                "MountainMerch_ru", // Using new biome-specific prefab
                "Mountains Merchant",
                "A merchant selling mountains resources and silver equipment",
                requirements
            );
        }

        // Mistlands Merchant - Requires Yagluth Trophy
        private void AddMistlandsMerchant()
        {
            List<RequirementConfig> requirements = GetRequirementsFromConfig("Mistlands",
                ("RoyalJelly", 10),
                ("YggdrasilWood", 20),
                ("Carapace", 5),
                ("TrophyGoblinKing", 1)
            );

            AddMerchantPiece(
                "MistlandsMerch_ru", // Using new biome-specific prefab
                "Mistlands Merchant",
                "A merchant selling mistlands resources and carapace equipment",
                requirements
            );
        }

        // Ashlands Merchant - Requires Queen Trophy 
        private void AddAshlandsMerchant()
        {
            List<RequirementConfig> requirements = GetRequirementsFromConfig("Ashlands",
                ("Blackwood", 20),
                ("FlametalOreNew", 5),
                ("Fiddleheadfern", 10),
                ("TrophySeekerQueen", 1)
            );

            AddMerchantPiece(
                "AshlandsMerch_ru", // Using new biome-specific prefab
                "Ashlands Merchant",
                "A merchant selling ashlands resources and flametal equipment",
                requirements
            );
        }

        // Deep North Merchant - Future content
        private void AddDeepNorthMerchant()
        {
            List<RequirementConfig> requirements = GetRequirementsFromConfig("DeepNorth",
                ("Frostwood", 20),
                ("MooseHide", 10),
                ("FrostCore", 10),
                ("TrophyFader", 1)
            );

            AddMerchantPiece(
                "DeepNorthMerch_ru", // Using new biome-specific prefab
                "Deep North Merchant",
                "A merchant selling Deep North resources and frost equipment",
                requirements
            );
        }
    }
}
