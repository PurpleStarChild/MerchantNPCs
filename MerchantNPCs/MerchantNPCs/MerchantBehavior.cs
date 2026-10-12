using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Jotunn.Managers;
using MerchantNPCs.MerchantTrading;

namespace MerchantNPCs
{
    /// <summary>
    /// Component that handles merchant NPC behavior
    /// This is attached to merchant prefabs to handle interactions
    /// </summary>
    public class MerchantBehavior : MonoBehaviour, Hoverable, Interactable
    {
        [Header("Merchant Settings")]
        [SerializeField] private string _merchantName = "Merchant";
        [SerializeField] private string _merchantType = "";
        [SerializeField] private string _merchantGreeting = "Hello, traveler!";
        [SerializeField] private float _interactionDistance = 5f;

        // Reference to the player who is currently interacting with the merchant
        private Player _interactingPlayer;

        // Hover text
        private string _hoverText;

        private void Awake()
        {
            // Set merchant name based on prefab name if not already set
            if (string.IsNullOrEmpty(_merchantName) || _merchantName == "Merchant")
            {
                // For biome-based merchants, extract the biome name from the prefab name
                // Example: MeadowsMerch_ru(Clone) -> Meadows
                if (gameObject.name.Contains("Merch_ru"))
                {
                    string prefabName = gameObject.name;
                    // Remove the (Clone) suffix if present
                    prefabName = prefabName.Replace("(Clone)", "");
                    string biomeName = prefabName.Replace("Merch_ru", "");

                    // Special case for BlackForest to add a space
                    if (biomeName == "Blackforest")
                    {
                        _merchantName = "Black Forest Merchant";
                        _merchantType = "BlackForest";
                    }
                    else if (biomeName == "DeepNorth")
                    {
                        _merchantName = "Deep North Merchant";
                        _merchantType = "DeepNorth";
                    }
                    else
                    {
                        _merchantName = $"{biomeName} Merchant";
                        _merchantType = biomeName;
                    }

                    MerchantNPCsPlugin.Logger.LogInfo($"Set merchant name to {_merchantName} based on prefab {prefabName}");
                }
                else
                {
                    // Default name if no specific pattern is matched
                    _merchantName = "Merchant";
                    _merchantType = "";
                    MerchantNPCsPlugin.Logger.LogWarning($"Could not determine merchant type from prefab name: {gameObject.name}");
                }
            }

            _hoverText = $"[<color=yellow><b>E</b></color>] Trade with {_merchantName}";

            // Make sure the merchant has a ZNetView component
            if (!GetComponent<ZNetView>())
            {
                MerchantNPCsPlugin.Logger.LogWarning($"Merchant {gameObject.name} is missing ZNetView component");
            }

            // We don't need an Animator component for now
            // if (!GetComponent<Animator>())
            // {
            //     MerchantNPCsPlugin.Logger.LogWarning($"Merchant {gameObject.name} is missing Animator component");
            // }
        }

        #region Hoverable Implementation

        public string GetHoverText()
        {
            return _hoverText;
        }

        public string GetHoverName()
        {
            return _merchantName;
        }

        // Added to the Hoverable interface by the Deep North update. No offset for the hover text.
        public float GetHoverOffset()
        {
            return 0f;
        }

        #endregion

        #region Interactable Implementation

        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            MerchantNPCsPlugin.Logger.LogInfo($"Merchant {gameObject.name} interaction started");

            if (hold)
            {
                MerchantNPCsPlugin.Logger.LogInfo("Hold interaction not supported");
                return false;
            }

            Player player = user as Player;
            if (player == null)
            {
                MerchantNPCsPlugin.Logger.LogError("User is not a Player");
                return false;
            }

            MerchantNPCsPlugin.Logger.LogInfo($"Player {player.GetPlayerName()} is interacting with merchant {_merchantName}");

            // Check if player is within interaction distance
            float distanceToPlayer = Vector3.Distance(player.transform.position, transform.position);
            MerchantNPCsPlugin.Logger.LogInfo($"Distance to player: {distanceToPlayer}, Interaction distance: {_interactionDistance}");

            if (distanceToPlayer > _interactionDistance)
            {
                // Player is too far away
                MerchantNPCsPlugin.Logger.LogInfo("Player is too far away");
                player.Message(MessageHud.MessageType.Center, "Get closer to interact with the merchant");
                return false;
            }

            // Set the interacting player
            _interactingPlayer = player;
            MerchantNPCsPlugin.Logger.LogInfo("Set interacting player");

            // Show greeting
            player.Message(MessageHud.MessageType.Center, $"{_merchantName}: {_merchantGreeting}");
            MerchantNPCsPlugin.Logger.LogInfo($"Showed greeting: {_merchantName}: {_merchantGreeting}");

            // Open merchant UI
            MerchantNPCsPlugin.Logger.LogInfo("Opening merchant UI...");
            OpenMerchantUI();

            return true;
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item)
        {
            return false;
        }

        #endregion

        /// <summary>
        /// Opens the merchant UI for the interacting player
        /// </summary>
        private void OpenMerchantUI()
        {
            try
            {
                MerchantNPCsPlugin.Logger.LogInfo("OpenMerchantUI method called");

                if (_interactingPlayer == null)
                {
                    MerchantNPCsPlugin.Logger.LogError("Cannot open merchant UI: No interacting player");
                    return;
                }

                MerchantNPCsPlugin.Logger.LogInfo($"Interacting player: {_interactingPlayer.GetPlayerName()}");
                MerchantNPCsPlugin.Logger.LogInfo($"Merchant type: {_merchantType}");

                // Make sure the merchant UI is initialized
                if (MerchantUI.Instance == null)
                {
                    MerchantNPCsPlugin.Logger.LogInfo("MerchantUI instance is null, initializing...");
                    MerchantUI.Initialize();

                    if (MerchantUI.Instance == null)
                    {
                        MerchantNPCsPlugin.Logger.LogError("Failed to initialize MerchantUI instance");
                        _interactingPlayer.Message(MessageHud.MessageType.Center, "Error initializing merchant UI");
                        return;
                    }

                    MerchantNPCsPlugin.Logger.LogInfo("MerchantUI instance initialized successfully");
                }
                else
                {
                    MerchantNPCsPlugin.Logger.LogInfo("MerchantUI instance already exists");
                }

                // Show the merchant UI for this merchant type
                // The UI will be created on demand if it doesn't exist yet
                MerchantNPCsPlugin.Logger.LogInfo("Calling MerchantUI.Show method...");
                MerchantUI.Instance.Show(_merchantType, _interactingPlayer);
                MerchantNPCsPlugin.Logger.LogInfo("MerchantUI.Show method completed");
            }
            catch (Exception ex)
            {
                MerchantNPCsPlugin.Logger.LogError($"Error opening merchant UI: {ex.Message}\n{ex.StackTrace}");
                if (_interactingPlayer != null)
                {
                    _interactingPlayer.Message(MessageHud.MessageType.Center, "Error opening merchant UI");
                }
            }
        }

        // OpenMerchantUIDelayed method removed - UI is now created on demand
    }
}