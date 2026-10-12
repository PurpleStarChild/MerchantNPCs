using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using UnityEngine;

namespace MerchantNPCs.MerchantTrading
{
    /// <summary>
    /// Manages merchant inventories and trading
    /// </summary>
    public class MerchantManager
    {
        private static MerchantManager _instance;
        public static MerchantManager Instance => _instance ?? (_instance = new MerchantManager());

        // Dictionary of merchant inventories by merchant type
        private Dictionary<string, MerchantInventory> _merchantInventories = new Dictionary<string, MerchantInventory>();

        // Path to the merchant configuration files
        private string _configPath;

        // Constructor
        private MerchantManager()
        {
            _configPath = Path.Combine(Paths.ConfigPath, "MerchantNPCs");

            // Create config directory if it doesn't exist
            if (!Directory.Exists(_configPath))
            {
                Directory.CreateDirectory(_configPath);
                CreateDefaultConfigs();
            }
        }

        /// <summary>
        /// Initializes the merchant manager
        /// </summary>
        public void Initialize()
        {
            LoadAllMerchantConfigs();
            //MerchantNPCsPlugin.Logger.LogInfo($"Loaded {_merchantInventories.Count} merchant inventories");
        }

        /// <summary>
        /// Creates default configuration files for each merchant type
        /// </summary>
        private void CreateDefaultConfigs()
        {
            CreateDefaultConfig("Blacksmith", new List<MerchantItem>
            {
                new MerchantItem { ItemName = "AxeIron", BuyPrice = 120},
                new MerchantItem { ItemName = "Hammer", BuyPrice = 80},
                new MerchantItem { ItemName = "PickaxeIron", BuyPrice = 150},
                new MerchantItem { ItemName = "ArrowIron", BuyPrice = 8},
                new MerchantItem { ItemName = "SwordIron", BuyPrice = 200},
                new MerchantItem { ItemName = "SwordSilver", BuyPrice = 350},
                new MerchantItem { ItemName = "AtgeirIron", BuyPrice = 180},
                new MerchantItem { ItemName = "MaceSilver", BuyPrice = 300}
            });

            CreateDefaultConfig("Farmer", new List<MerchantItem>
            {
                new MerchantItem { ItemName = "Carrot", BuyPrice = 10 },
                new MerchantItem { ItemName = "CarrotSeeds", BuyPrice = 5 },
                new MerchantItem { ItemName = "Turnip", BuyPrice = 15 },
                new MerchantItem { ItemName = "TurnipSeeds", BuyPrice = 8 },
                new MerchantItem { ItemName = "Flax", BuyPrice = 25 },
                new MerchantItem { ItemName = "Barley", BuyPrice = 20 },
                new MerchantItem { ItemName = "Honey", BuyPrice = 12 },
                new MerchantItem { ItemName = "QueenBee", BuyPrice = 200 },
                new MerchantItem { ItemName = "Cultivator", BuyPrice = 60 }
            });

            CreateDefaultConfig("Innkeeper", new List<MerchantItem>
            {
                new MerchantItem { ItemName = "MeadBaseFrostResist", BuyPrice = 50 },
                new MerchantItem { ItemName = "MeadBaseHealthMedium", BuyPrice = 60 },
                new MerchantItem { ItemName = "MeadBaseStaminaMedium", BuyPrice = 60 },
                new MerchantItem { ItemName = "MeadBasePoisonResist", BuyPrice = 50 },
                new MerchantItem { ItemName = "MeadBaseTasty", BuyPrice = 40 },
                new MerchantItem { ItemName = "Bread", BuyPrice = 20 },
                new MerchantItem { ItemName = "FishCooked", BuyPrice = 15 },
                new MerchantItem { ItemName = "NeckTailGrilled", BuyPrice = 15 },
                new MerchantItem { ItemName = "SerpentMeatCooked", BuyPrice = 80 }
            });

            CreateDefaultConfig("Trader", new List<MerchantItem>
            {
                new MerchantItem { ItemName = "Ruby", BuyPrice = 200 },
                new MerchantItem { ItemName = "Amber", BuyPrice = 100 },
                new MerchantItem { ItemName = "FineWood", BuyPrice = 5 },
                new MerchantItem { ItemName = "Iron", BuyPrice = 20 },
                new MerchantItem { ItemName = "Silver", BuyPrice = 40 },
                new MerchantItem { ItemName = "BlackMetal", BuyPrice = 60 },
                new MerchantItem { ItemName = "Obsidian", BuyPrice = 25 },
                new MerchantItem { ItemName = "Chitin", BuyPrice = 30 }
            });
        }

        /// <summary>
        /// Creates a default configuration file for a merchant type
        /// </summary>
        private void CreateDefaultConfig(string merchantType, List<MerchantItem> items)
        {
            string configPath = Path.Combine(_configPath, $"{merchantType.ToLower()}.txt");

            // Create the compact format configuration file
            List<string> lines = new List<string>();
            lines.Add($"# {merchantType} Items");
            lines.Add("# Format: item:price");
            lines.Add("");

            foreach (var item in items)
            {
                lines.Add($"{item.ItemName}:{item.BuyPrice}");
            }

            File.WriteAllLines(configPath, lines);

            //MerchantNPCsPlugin.Logger.LogInfo($"Created default config for {merchantType}");
        }

        /// <summary>
        /// Loads all merchant configuration files
        /// </summary>
        public void LoadAllMerchantConfigs()
        {
            _merchantInventories.Clear();

            // First try to load the new compact format (.txt) files
            string[] txtConfigFiles = Directory.GetFiles(_configPath, "*.txt");
            if (txtConfigFiles.Length > 0)
            {
                foreach (string configFile in txtConfigFiles)
                {
                    try
                    {
                        LoadCompactFormatConfig(configFile);
                    }
                    catch (Exception ex)
                    {
                        MerchantNPCsPlugin.Logger.LogError($"Error loading merchant config {configFile}: {ex.Message}");
                    }
                }
            }
            else
            {
                // Fall back to YAML files if no TXT files are found
                string[] yamlConfigFiles = Directory.GetFiles(_configPath, "*.yaml");
                foreach (string configFile in yamlConfigFiles)
                {
                    try
                    {
                        // Convert YAML to compact format
                        ConvertYamlToCompactFormat(configFile);
                    }
                    catch (Exception ex)
                    {
                        MerchantNPCsPlugin.Logger.LogError($"Error converting YAML config {configFile}: {ex.Message}");
                    }
                }

                // Try loading the converted files
                txtConfigFiles = Directory.GetFiles(_configPath, "*.txt");
                foreach (string configFile in txtConfigFiles)
                {
                    try
                    {
                        LoadCompactFormatConfig(configFile);
                    }
                    catch (Exception ex)
                    {
                        MerchantNPCsPlugin.Logger.LogError($"Error loading merchant config {configFile}: {ex.Message}");
                    }
                }
            }

            // Populate item data for all items
            PopulateItemData();
        }

        /// <summary>
        /// Loads a merchant configuration file in the compact format
        /// </summary>
        private void LoadCompactFormatConfig(string configFile)
        {
            string merchantType = Path.GetFileNameWithoutExtension(configFile);
            merchantType = char.ToUpper(merchantType[0]) + merchantType.Substring(1); // Capitalize first letter

            List<MerchantItem> items = new List<MerchantItem>();
            string[] lines = File.ReadAllLines(configFile);

            foreach (string line in lines)
            {
                string trimmedLine = line.Trim();

                // Skip empty lines and comments
                if (string.IsNullOrWhiteSpace(trimmedLine) || trimmedLine.StartsWith("#"))
                    continue;

                // Parse item entries in format: item:price:requires_discovery
                string[] parts = trimmedLine.Split(':');
                if (parts.Length < 2)
                    continue; // Need at least item and price

                string itemName = parts[0].Trim();
                string originalItemName = itemName; // Keep the original name for prefab lookup

                // Ensure the item name has the $item_ prefix for discovery checks
                if (!itemName.StartsWith("$item_") && !itemName.StartsWith("$"))
                {
                    itemName = "$item_" + itemName.Replace(" ", "_");
                }

                // Parse price
                if (!int.TryParse(parts[1].Trim(), out int price))
                    continue; // Invalid price

                // Add the item to the inventory
                items.Add(new MerchantItem
                {
                    ItemName = itemName,
                    OriginalItemName = originalItemName, // Store the original name for prefab lookup
                    BuyPrice = price
                });
            }

            // Create the inventory and add it to the dictionary
            MerchantInventory inventory = new MerchantInventory
            {
                MerchantType = merchantType,
                Items = items
            };

            _merchantInventories[merchantType] = inventory;
            //MerchantNPCsPlugin.Logger.LogInfo($"Loaded merchant config for {merchantType} with {items.Count} items");
        }

        /// <summary>
        /// Converts a YAML configuration file to the compact format
        /// </summary>
        private void ConvertYamlToCompactFormat(string yamlFile)
        {
            try
            {
                // This is just a placeholder since we've already manually converted the files
                // In a real implementation, this would parse the YAML and convert it to the compact format
                string txtFile = Path.ChangeExtension(yamlFile, ".txt");
                if (!File.Exists(txtFile))
                {
                    MerchantNPCsPlugin.Logger.LogWarning($"Could not find converted file {txtFile} for {yamlFile}");
                }
            }
            catch (Exception ex)
            {
                MerchantNPCsPlugin.Logger.LogError($"Error converting YAML file {yamlFile}: {ex.Message}");
            }
        }

        /// <summary>
        /// Populates the ItemData property for all merchant items
        /// </summary>
        private void PopulateItemData()
        {
            foreach (var inventory in _merchantInventories.Values)
            {
                foreach (var item in inventory.Items)
                {
                    try
                    {
                        // Try multiple formats of the item name to find the prefab
                        GameObject prefab = null;
                        string prefabName = null;

                        // Format 1: Try with the prefixed item name (e.g., $item_Wood)
                        prefab = ZNetScene.instance?.GetPrefab(item.ItemName);
                        if (prefab != null)
                        {
                            prefabName = item.ItemName;
                            // MerchantNPCsPlugin.Logger.LogDebug($"Found prefab using prefixed item name: {item.ItemName}");
                        }

                        // Format 2: Try with the original item name without prefix (e.g., Wood)
                        if (prefab == null && !string.IsNullOrEmpty(item.OriginalItemName))
                        {
                            prefab = ZNetScene.instance?.GetPrefab(item.OriginalItemName);
                            if (prefab != null)
                            {
                                prefabName = item.OriginalItemName;
                                // MerchantNPCsPlugin.Logger.LogDebug($"Found prefab using original item name: {item.OriginalItemName}");
                            }
                        }

                        // Format 3: Try with lowercase name
                        if (prefab == null)
                        {
                            string lowercaseName = item.OriginalItemName.ToLower();
                            prefab = ZNetScene.instance?.GetPrefab(lowercaseName);
                            if (prefab != null)
                            {
                                prefabName = lowercaseName;
                                // MerchantNPCsPlugin.Logger.LogDebug($"Found prefab using lowercase name: {lowercaseName}");
                            }
                        }

                        // Format 4: Try without spaces and lowercase
                        if (prefab == null)
                        {
                            string noSpacesName = item.OriginalItemName.Replace(" ", "").ToLower();
                            prefab = ZNetScene.instance?.GetPrefab(noSpacesName);
                            if (prefab != null)
                            {
                                prefabName = noSpacesName;
                                // MerchantNPCsPlugin.Logger.LogDebug($"Found prefab using no spaces lowercase name: {noSpacesName}");
                            }
                        }

                        // Format 5: Try with first letter capitalized and no spaces
                        if (prefab == null)
                        {
                            string pascalCaseName = item.OriginalItemName.Replace(" ", "");
                            if (pascalCaseName.Length > 0)
                            {
                                pascalCaseName = char.ToUpper(pascalCaseName[0]) + pascalCaseName.Substring(1).ToLower();
                                prefab = ZNetScene.instance?.GetPrefab(pascalCaseName);
                                if (prefab != null)
                                {
                                    prefabName = pascalCaseName;
                                    // MerchantNPCsPlugin.Logger.LogDebug($"Found prefab using pascal case name: {pascalCaseName}");
                                }
                            }
                        }

                        // Format 6: Try with all words capitalized and no spaces (PascalCase)
                        if (prefab == null)
                        {
                            string[] words = item.OriginalItemName.Split(' ');
                            string pascalCaseName = "";
                            foreach (string word in words)
                            {
                                if (!string.IsNullOrEmpty(word))
                                {
                                    pascalCaseName += char.ToUpper(word[0]) + word.Substring(1).ToLower();
                                }
                            }
                            prefab = ZNetScene.instance?.GetPrefab(pascalCaseName);
                            if (prefab != null)
                            {
                                prefabName = pascalCaseName;
                                // MerchantNPCsPlugin.Logger.LogDebug($"Found prefab using multi-word pascal case name: {pascalCaseName}");
                            }
                        }

                        if (prefab != null)
                        {
                            ItemDrop itemDrop = prefab.GetComponent<ItemDrop>();
                            if (itemDrop != null)
                            {
                                item.ItemData = itemDrop.m_itemData;
                                // Store the actual prefab name that worked for future reference
                                item.PrefabName = prefab.name;
                                // MerchantNPCsPlugin.Logger.LogDebug($"Found item data for {item.ItemName}, using prefab name: {prefab.name}");

                                // If the item name in the game differs from our configured name, log it for debugging
                                if (itemDrop.m_itemData.m_shared.m_name != item.ItemName)
                                {
                                    // MerchantNPCsPlugin.Logger.LogDebug($"Item name mismatch: Config={item.ItemName}, Game={itemDrop.m_itemData.m_shared.m_name}");
                                }
                            }
                            else
                            {
                                MerchantNPCsPlugin.Logger.LogWarning($"Prefab {prefab.name} does not have an ItemDrop component");
                            }
                        }
                        else
                        {
                            MerchantNPCsPlugin.Logger.LogWarning($"Could not find prefab for item {item.ItemName} or {item.OriginalItemName}");
                        }
                    }
                    catch (Exception ex)
                    {
                        MerchantNPCsPlugin.Logger.LogError($"Error populating item data for {item.ItemName}: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Gets the merchant inventory for a specific merchant type
        /// </summary>
        public MerchantInventory GetMerchantInventory(string merchantType)
        {
            if (_merchantInventories.TryGetValue(merchantType, out MerchantInventory inventory))
            {
                return inventory;
            }

            // Handle special case for Mountain/Mountains naming inconsistency
            if (merchantType == "Mountain" && !_merchantInventories.ContainsKey("Mountain") && _merchantInventories.ContainsKey("Mountains"))
            {
                // MerchantNPCsPlugin.Logger.LogInfo($"Using Mountains inventory for Mountains Merchant");
                return _merchantInventories["Mountains"];
            }
            else if (merchantType == "Mountains" && !_merchantInventories.ContainsKey("Mountains") && _merchantInventories.ContainsKey("Mountain"))
            {
                // MerchantNPCsPlugin.Logger.LogInfo($"Using Mountain inventory for Mountains Merchant");
                return _merchantInventories["Mountain"];
            }

            return null;
        }


        /// <summary>
        /// Buys an item from a merchant
        /// </summary>
        public bool BuyItem(Player player, MerchantItem item, int quantity = 1)
        {
            try
            {
                if (player == null)
                {
                    MerchantNPCsPlugin.Logger.LogError("BuyItem: Player is null");
                    return false;
                }

                if (item == null)
                {
                    MerchantNPCsPlugin.Logger.LogError("BuyItem: Item is null");
                    player.Message(MessageHud.MessageType.Center, "Error: Invalid item");
                    return false;
                }

                if (quantity <= 0)
                {
                    MerchantNPCsPlugin.Logger.LogError($"BuyItem: Invalid quantity {quantity}");
                    return false;
                }

                // MerchantNPCsPlugin.Logger.LogInfo($"Attempting to buy {quantity}x {item.ItemName}");

                // Check if the item is available
                if (!item.IsAvailable)
                {
                    // MerchantNPCsPlugin.Logger.LogInfo($"Item {item.ItemName} is not available for purchase");
                    player.Message(MessageHud.MessageType.Center, "This item is not available for purchase");
                    return false;
                }

                // Check if the player has enough coins
                int totalCost = item.BuyPrice * quantity;
                // MerchantNPCsPlugin.Logger.LogInfo($"Total cost: {totalCost} coins");

                if (!HasEnoughCoins(player, totalCost))
                {
                    // MerchantNPCsPlugin.Logger.LogInfo("Player doesn't have enough coins");
                    player.Message(MessageHud.MessageType.Center, "You don't have enough coins for that");
                    return false;
                }

                // Check if item data is valid
                if (item.ItemData == null)
                {
                    MerchantNPCsPlugin.Logger.LogError($"BuyItem: ItemData is null for {item.ItemName}");
                    player.Message(MessageHud.MessageType.Center, "Error: Invalid item data");
                    return false;
                }

                // Check if the player has enough inventory space
                if (!HasEnoughInventorySpace(player, item.ItemData, quantity))
                {
                    // MerchantNPCsPlugin.Logger.LogInfo("Player doesn't have enough inventory space");
                    player.Message(MessageHud.MessageType.Center, "You don't have enough space for that");
                    return false;
                }

                // Remove coins from player
                // MerchantNPCsPlugin.Logger.LogInfo($"Removing {totalCost} coins from player");
                RemoveCoins(player, totalCost);

                // Find the prefab for the item
                GameObject prefab = null;

                // Safety check for ZNetScene
                if (ZNetScene.instance == null)
                {
                    MerchantNPCsPlugin.Logger.LogError("ZNetScene.instance is null");
                    player.Message(MessageHud.MessageType.Center, "Error: Game scene not fully loaded");
                    return false;
                }

                try
                {
                    // Try to get the prefab by name
                    prefab = ZNetScene.instance.GetPrefab(item.ItemName);
                    // MerchantNPCsPlugin.Logger.LogInfo($"Trying to get prefab by ItemName: {item.ItemName}, result: {(prefab != null ? "found" : "not found")}");
                }
                catch (Exception ex)
                {
                    MerchantNPCsPlugin.Logger.LogError($"Exception when getting prefab by ItemName: {ex.Message}");
                }

                // If that fails, try to get it by the actual item name
                if (prefab == null && item.ItemData != null && item.ItemData.m_shared != null)
                {
                    try
                    {
                        string itemSharedName = item.ItemData.m_shared.m_name;
                        // MerchantNPCsPlugin.Logger.LogInfo($"Trying to get prefab by shared name: {itemSharedName}");
                        prefab = ZNetScene.instance.GetPrefab(itemSharedName);
                    }
                    catch (Exception ex)
                    {
                        MerchantNPCsPlugin.Logger.LogError($"Exception when getting prefab by shared name: {ex.Message}");
                    }
                }

                // If that still fails, try to get it from the ObjectDB
                if (prefab == null && ObjectDB.instance != null)
                {
                    try
                    {
                        prefab = ObjectDB.instance.GetItemPrefab(item.ItemName);
                        // MerchantNPCsPlugin.Logger.LogInfo($"Trying to get prefab from ObjectDB: {(prefab != null ? "found" : "not found")}");
                    }
                    catch (Exception ex)
                    {
                        MerchantNPCsPlugin.Logger.LogError($"Exception when getting prefab from ObjectDB: {ex.Message}");
                    }

                    // Last resort - try to find by the actual item name in the ObjectDB
                    if (prefab == null && item.ItemData != null && item.ItemData.m_shared != null)
                    {
                        try
                        {
                            string itemSharedName = item.ItemData.m_shared.m_name;
                            // MerchantNPCsPlugin.Logger.LogInfo($"Searching ObjectDB items for: {itemSharedName}");

                            foreach (GameObject obj in ObjectDB.instance.m_items)
                            {
                                if (obj == null) continue;

                                ItemDrop component = obj.GetComponent<ItemDrop>();
                                if (component != null && component.m_itemData != null &&
                                    component.m_itemData.m_shared != null &&
                                    component.m_itemData.m_shared.m_name == itemSharedName)
                                {
                                    prefab = obj;
                                    // MerchantNPCsPlugin.Logger.LogInfo($"Found prefab in ObjectDB items: {obj.name}");
                                    break;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            MerchantNPCsPlugin.Logger.LogError($"Exception when searching ObjectDB items: {ex.Message}");
                        }
                    }
                }

                // Try direct prefab creation as a last resort
                if (prefab == null)
                {
                    try
                    {
                        // Try to create a simple prefab with the item name
                        // MerchantNPCsPlugin.Logger.LogInfo("Creating a simple prefab as last resort");
                        prefab = new GameObject(item.ItemName);
                        ItemDrop newItemDrop = prefab.AddComponent<ItemDrop>();
                        newItemDrop.m_itemData = item.ItemData.Clone();
                    }
                    catch (Exception ex)
                    {
                        MerchantNPCsPlugin.Logger.LogError($"Exception when creating simple prefab: {ex.Message}");
                    }
                }

                if (prefab == null)
                {
                    MerchantNPCsPlugin.Logger.LogError($"Could not find or create prefab for {item.ItemName}");
                    player.Message(MessageHud.MessageType.Center, "Error: Could not find item");
                    return false;
                }

                // Get the ItemDrop component
                ItemDrop itemDrop = prefab.GetComponent<ItemDrop>();
                if (itemDrop == null)
                {
                    MerchantNPCsPlugin.Logger.LogError($"Prefab {item.ItemName} does not have an ItemDrop component");
                    player.Message(MessageHud.MessageType.Center, "Error: Invalid item");
                    return false;
                }

                // Try to add the item to player inventory
                try
                {
                    // Create a clone of the item data
                    ItemDrop.ItemData newItem = itemDrop.m_itemData.Clone();
                    newItem.m_stack = quantity;

                    // Ensure the item is properly initialized
                    if (newItem.m_shared == null)
                    {
                        MerchantNPCsPlugin.Logger.LogWarning($"Item {item.ItemName} has null m_shared, attempting to fix");
                        newItem.m_shared = itemDrop.m_itemData.m_shared;
                    }

                    // Ensure the item has a valid ID
                    if (string.IsNullOrEmpty(newItem.m_dropPrefab?.name))
                    {
                        MerchantNPCsPlugin.Logger.LogWarning($"Item {item.ItemName} has null prefab name, setting from original");
                        newItem.m_dropPrefab = itemDrop.gameObject;
                    }

                    // Add to inventory
                    if (player.GetInventory().AddItem(newItem))
                    {
                        string itemName = item.ItemData.m_shared.m_name;
                        // MerchantNPCsPlugin.Logger.LogInfo($"Player purchased {quantity}x {itemName}");
                        player.Message(MessageHud.MessageType.Center, $"You purchased {quantity}x {itemName}");

                        // Force inventory update to ensure the item is properly registered
                        player.GetInventory().Changed();

                        // If this is equippable gear, make sure it's properly initialized
                        if (newItem.IsEquipable())
                        {
                            try
                            {
                                // MerchantNPCsPlugin.Logger.LogInfo("Updating equipment after purchase");

                                // Make sure the visual equipment component exists
                                if (player.m_visEquipment == null)
                                {
                                    MerchantNPCsPlugin.Logger.LogWarning("Player visual equipment is null, attempting to initialize");
                                    // Try to find or add the component
                                    player.m_visEquipment = player.GetComponent<VisEquipment>();
                                    if (player.m_visEquipment == null)
                                    {
                                        player.m_visEquipment = player.gameObject.AddComponent<VisEquipment>();
                                    }
                                }

                                // Force inventory to update first
                                player.GetInventory().Changed();

                                // Let the game process for a frame to ensure inventory changes are registered
                                // This is a safer approach than immediately trying to update equipment
                                player.StartCoroutine(UpdatePlayerEquipmentDelayed(player, newItem));

                                // MerchantNPCsPlugin.Logger.LogInfo("Equipment update initiated");
                            }
                            catch (Exception equipEx)
                            {
                                MerchantNPCsPlugin.Logger.LogError($"Error updating equipment: {equipEx.Message}");
                            }
                        }

                        return true;
                    }
                    else
                    {
                        MerchantNPCsPlugin.Logger.LogError($"Failed to add {item.ItemName} to player inventory");
                        player.Message(MessageHud.MessageType.Center, "Error: Could not add item to inventory");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    MerchantNPCsPlugin.Logger.LogError($"Exception when adding item to inventory: {ex.Message}");
                    player.Message(MessageHud.MessageType.Center, "Error: Failed to add item to inventory");
                    return false;
                }
            }
            catch (Exception ex)
            {
                MerchantNPCsPlugin.Logger.LogError($"Unhandled exception in BuyItem: {ex.Message}\n{ex.StackTrace}");
                if (player != null)
                {
                    player.Message(MessageHud.MessageType.Center, "Error: Failed to complete purchase");
                }
                return false;
            }
        }

        /// <summary>
        /// Updates player equipment after a short delay to ensure everything is properly initialized
        /// </summary>
        private IEnumerator UpdatePlayerEquipmentDelayed(Player player, ItemDrop.ItemData newItem)
        {
            // Wait for a frame to let inventory changes propagate
            yield return null;

            try
            {
                // Force equipment update
                player.SetupEquipment();

                // Force visual equipment update if component exists
                if (player.m_visEquipment != null)
                {
                    player.SetupVisEquipment(player.m_visEquipment, false);
                }
                else
                {
                    MerchantNPCsPlugin.Logger.LogWarning("Visual equipment component still null after initialization attempt");
                }

                // Ensure ZDO is updated for network sync
                if (player.m_nview && player.m_nview.IsValid())
                {
                    player.GetInventory().Changed();
                }

                // MerchantNPCsPlugin.Logger.LogInfo("Equipment update completed successfully");
            }
            catch (Exception ex)
            {
                MerchantNPCsPlugin.Logger.LogError($"Error in delayed equipment update: {ex.Message}");
            }
        }

        /// <summary>
        /// Sells an item to a merchant
        /// </summary>
        public bool SellItem(Player player, ItemDrop.ItemData item, int quantity = 1)
        {
            if (player == null || item == null || quantity <= 0 || quantity > item.m_stack)
            {
                return false;
            }

            // Find the merchant item that matches this item
            MerchantItem merchantItem = FindMerchantItem(item.m_shared.m_name);
            if (merchantItem == null)
            {
                player.Message(MessageHud.MessageType.Center, "This merchant doesn't buy that item");
                return false;
            }

            // Calculate sell price
            int totalValue = merchantItem.SellPrice * quantity;

            // Remove item from player inventory
            player.GetInventory().RemoveItem(item.m_shared.m_name, quantity);

            // Add coins to player inventory
            AddCoins(player, totalValue);
            player.Message(MessageHud.MessageType.Center, $"You sold {quantity}x {item.m_shared.m_name} for {totalValue} coins");

            return true;
        }

        /// <summary>
        /// Finds a merchant item by name across all merchant inventories
        /// </summary>
        private MerchantItem FindMerchantItem(string itemName)
        {
            foreach (var inventory in _merchantInventories.Values)
            {
                foreach (var item in inventory.Items)
                {
                    if (item.ItemName == itemName || (item.ItemData != null && item.ItemData.m_shared.m_name == itemName))
                    {
                        return item;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Checks if a player has enough coins
        /// </summary>
        private bool HasEnoughCoins(Player player, int amount)
        {
            // Find all coin stacks in the player's inventory
            int totalCoins = 0;
            var inventory = player.GetInventory();
            foreach (var item in inventory.GetAllItems())
            {
                if (item.m_shared.m_name.ToLower() == "coins" || item.m_shared.m_name == "$item_coins")
                {
                    totalCoins += item.m_stack;
                }
            }

            // MerchantNPCsPlugin.Logger.LogInfo($"Player has {totalCoins} coins, needs {amount}");
            return totalCoins >= amount;
        }

        /// <summary>
        /// Removes coins from a player's inventory
        /// </summary>
        private void RemoveCoins(Player player, int amount)
        {
            int remainingAmount = amount;
            var inventory = player.GetInventory();

            // Find coin items and remove them until we've removed enough
            List<ItemDrop.ItemData> coinItems = new List<ItemDrop.ItemData>();

            // First, collect all coin items
            foreach (var item in inventory.GetAllItems())
            {
                if (item.m_shared.m_name.ToLower() == "coins" || item.m_shared.m_name == "$item_coins")
                {
                    coinItems.Add(item);
                }
            }

            // Then remove coins from stacks
            foreach (var coinItem in coinItems)
            {
                if (remainingAmount <= 0) break;

                if (coinItem.m_stack <= remainingAmount)
                {
                    // Remove the entire stack
                    remainingAmount -= coinItem.m_stack;
                    inventory.RemoveItem(coinItem);
                }
                else
                {
                    // Remove part of the stack
                    coinItem.m_stack -= remainingAmount;
                    remainingAmount = 0;
                    break;
                }
            }

            // MerchantNPCsPlugin.Logger.LogInfo($"Removed {amount} coins from player's inventory");
        }

        /// <summary>
        /// Adds coins to a player's inventory.
        /// The Deep North Inventory no longer has AddItem(string name, ...). AddItem(GameObject, int) caps
        /// each call at the item's max stack size, so coins are added in chunks of that size.
        /// </summary>
        public void AddCoins(Player player, int amount)
        {
            if (player == null || amount <= 0)
            {
                return;
            }

            GameObject coinPrefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab("Coins") : null;
            if (coinPrefab == null && ObjectDB.instance != null)
            {
                coinPrefab = ObjectDB.instance.GetItemPrefab("Coins");
            }

            ItemDrop coinDrop = coinPrefab != null ? coinPrefab.GetComponent<ItemDrop>() : null;
            if (coinDrop == null)
            {
                MerchantNPCsPlugin.Logger.LogError("Could not find the Coins prefab, no coins were added");
                return;
            }

            int maxStack = Mathf.Max(1, coinDrop.m_itemData.m_shared.m_maxStackSize);
            Inventory inventory = player.GetInventory();
            int remaining = amount;

            while (remaining > 0)
            {
                int chunk = Mathf.Min(remaining, maxStack);
                if (!inventory.AddItem(coinPrefab, chunk))
                {
                    MerchantNPCsPlugin.Logger.LogWarning($"Inventory is full, could not add {remaining} coins");
                    return;
                }

                remaining -= chunk;
            }
        }

        /// <summary>
        /// Checks if a player has enough inventory space for an item
        /// </summary>
        private bool HasEnoughInventorySpace(Player player, ItemDrop.ItemData itemData, int quantity)
        {
            if (player == null || itemData == null || quantity <= 0)
            {
                MerchantNPCsPlugin.Logger.LogError("Invalid parameters in HasEnoughInventorySpace");
                return false;
            }

            if (ZNetScene.instance == null)
            {
                MerchantNPCsPlugin.Logger.LogError("ZNetScene.instance is null");
                return true; // Assume there's enough space if we can't check properly
            }

            if (itemData.m_shared == null)
            {
                MerchantNPCsPlugin.Logger.LogError("itemData.m_shared is null");
                return true; // Assume there's enough space if we can't check properly
            }

            string itemName = itemData.m_shared.m_name;
            if (string.IsNullOrEmpty(itemName))
            {
                MerchantNPCsPlugin.Logger.LogError("Item name is null or empty");
                return true; // Assume there's enough space if we can't check properly
            }

            // MerchantNPCsPlugin.Logger.LogInfo($"Checking inventory space for item: {itemName}");

            // Try to get the prefab by name
            GameObject prefab = null;
            try
            {
                prefab = ZNetScene.instance.GetPrefab(itemName);
            }
            catch (Exception ex)
            {
                MerchantNPCsPlugin.Logger.LogError($"Exception when getting prefab by name: {ex.Message}");
            }

            // If that fails, try to get it by the prefab name if available
            if (prefab == null && itemData.m_dropPrefab != null)
            {
                try
                {
                    prefab = ZNetScene.instance.GetPrefab(itemData.m_dropPrefab.name);
                }
                catch (Exception ex)
                {
                    MerchantNPCsPlugin.Logger.LogError($"Exception when getting prefab by dropPrefab name: {ex.Message}");
                }
            }

            // If that still fails, try to get it from ObjectDB
            if (prefab == null && ObjectDB.instance != null)
            {
                try
                {
                    prefab = ObjectDB.instance.GetItemPrefab(itemName);
                }
                catch (Exception ex)
                {
                    MerchantNPCsPlugin.Logger.LogError($"Exception when getting prefab from ObjectDB: {ex.Message}");
                }

                // Last resort - try to find by the actual item name in the ObjectDB
                if (prefab == null)
                {
                    try
                    {
                        foreach (GameObject obj in ObjectDB.instance.m_items)
                        {
                            if (obj == null) continue;

                            ItemDrop component = obj.GetComponent<ItemDrop>();
                            if (component != null && component.m_itemData != null &&
                                component.m_itemData.m_shared != null &&
                                component.m_itemData.m_shared.m_name == itemName)
                            {
                                prefab = obj;
                                break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MerchantNPCsPlugin.Logger.LogError($"Exception when searching ObjectDB items: {ex.Message}");
                    }
                }
            }

            if (prefab == null)
            {
                MerchantNPCsPlugin.Logger.LogError($"Could not find prefab for {itemName} - assuming inventory has space");
                return true; // Assume there's enough space if we can't find the prefab
            }

            // Check if the player can add the item
            try
            {
                bool canAdd = player.GetInventory().CanAddItem(prefab, quantity);
                // MerchantNPCsPlugin.Logger.LogInfo($"Can player add {quantity}x {itemName}? {canAdd}");
                return canAdd;
            }
            catch (Exception ex)
            {
                MerchantNPCsPlugin.Logger.LogError($"Exception when checking CanAddItem: {ex.Message}");
                return true; // Assume there's enough space if we can't check properly
            }
        }
    }
}