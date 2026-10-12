using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Jotunn.Managers;
using BepInEx;
using UnityEngine.EventSystems;
using System.Text.RegularExpressions;
using System.Globalization;
using UnityEngine.SceneManagement;
#if UNITY_2019_1_OR_NEWER
using UnityEngine.ImageConversion;
#endif

namespace MerchantNPCs.MerchantTrading
{
    /// <summary>
    /// UI for merchant trading
    /// </summary>
    public class MerchantUI : MonoBehaviour
    {
        // Custom color palette for merchant UI
        private static readonly Color MerchantBlue = new Color(0.1f, 0.2f, 0.4f, 1f);
        private static readonly Color MerchantDarkBlue = new Color(0.05f, 0.1f, 0.2f, 1f);
        private static readonly Color MerchantGold = new Color(0.9f, 0.7f, 0.2f, 1f);
        private static readonly Color PanelTint = new Color(0.05f, 0.1f, 0.2f, 0.95f); // Using dark blue for window background

        // Button colors for merchant UI styling - lighter shade of dark blue
        private static readonly Color MerchantButtonNormal = new Color(0.25f, 0.35f, 0.55f, 1f);
        private static readonly Color MerchantButtonHighlighted = new Color(0.35f, 0.45f, 0.65f, 1f);
        private static readonly Color MerchantButtonPressed = new Color(0.2f, 0.3f, 0.5f, 1f);
        private static readonly Color MerchantTextColor = new Color(0.9f, 0.8f, 0.5f, 1f); // Gold text

        // Config path for custom background
        private static readonly string ConfigFolderPath = Path.Combine(Paths.ConfigPath, "MerchantNPCs");
        private static readonly string BackgroundImagePath = Path.Combine(ConfigFolderPath, "merchantnpcscanvas.png"); // Light gold
        private static readonly Color MerchantTitleColor = new Color(1f, 0.8f, 0.4f, 1f); // Gold

        // Singleton instance
        private static MerchantUI _instance;
        public static MerchantUI Instance => _instance;

        // Flag to track if UI has been created
        private bool _uiCreated = false;

        // Flag to track if we need to recreate the UI after a session change
        private bool _needsRecreation = false;

        // References to UI elements
        private GameObject _uiPanel;
        private Text _merchantNameText;
        // Player coins display removed
        private InputField _buySearchInput;
        private InputField _sellSearchInput;
        private RectTransform _buyItemsContainer;
        private RectTransform _sellItemsContainer;
        private Button _buyButton;
        private Button _sellButton;
        private Button _closeButton;
        private Button _quantity1Button;
        private Button _quantity5Button;
        private Button _quantity10Button;
        private Text _quantityText;

        // We use Valheim's native chat system for merchant messages

        // Current state
        private Player _player;
        private string _merchantType;
        private MerchantInventory _merchantInventory;
        private MerchantItem _selectedBuyItem;
        private ItemDrop.ItemData _selectedSellItem;
        private int _currentQuantity = 1;
        private string _buySearchFilter = "";
        private string _sellSearchFilter = "";

        // UI prefabs
        private GameObject _itemButtonPrefab;

        // Initialization
        public static void Initialize()
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("MerchantUI");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<MerchantUI>();
                //MerchantNPCsPlugin.Logger.LogInfo("MerchantUI instance created - UI will be created on demand");

                // Subscribe to scene change events to handle session changes
                SceneManager.sceneLoaded += OnSceneLoaded;
            }
        }

        // Handle scene changes to detect session changes
        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (_instance != null)
            {
                //MerchantNPCsPlugin.Logger.LogInfo($"Scene loaded: {scene.name}, marking UI for recreation");
                _instance._needsRecreation = true;
                _instance._uiCreated = false;
            }
        }

        private void OnDestroy()
        {
            // Clean up any resources when the UI is destroyed
            //MerchantNPCsPlugin.Logger.LogInfo("MerchantUI OnDestroy called");

            // Unsubscribe from scene change events
            SceneManager.sceneLoaded -= OnSceneLoaded;

            // Clean up UI elements
            CleanupUIElements();
        }

        /// <summary>
        /// Cleans up UI elements to prepare for recreation
        /// </summary>
        private void CleanupUIElements()
        {
            //MerchantNPCsPlugin.Logger.LogInfo("Cleaning up UI elements");

            // Destroy UI panel if it exists
            if (_uiPanel != null)
            {
                Destroy(_uiPanel);
                _uiPanel = null;
            }

            // Reset UI element references
            _merchantNameText = null;
            _buySearchInput = null;
            _sellSearchInput = null;
            _buyItemsContainer = null;
            _sellItemsContainer = null;
            _buyButton = null;
            _sellButton = null;
            _closeButton = null;
            _quantity1Button = null;
            _quantity5Button = null;
            _quantity10Button = null;
            _quantityText = null;

            // Recreate item button prefab if needed
            if (_itemButtonPrefab != null)
            {
                Destroy(_itemButtonPrefab);
                _itemButtonPrefab = null;
            }

            // Reset state
            _uiCreated = false;
        }

        private void CreateUI()
        {
            try
            {
                //MerchantNPCsPlugin.Logger.LogInfo("CreateUI method called");

                // Check if GUIManager is available and wait if necessary
                int attempts = 0;
                while (GUIManager.Instance == null && attempts < 5)
                {
                    MerchantNPCsPlugin.Logger.LogWarning($"GUIManager.Instance is null. Waiting for it to be available (attempt {attempts + 1}/5)...");
                    System.Threading.Thread.Sleep(500); // Wait for 500ms
                    attempts++;
                }

                if (GUIManager.Instance == null)
                {
                    MerchantNPCsPlugin.Logger.LogError("GUIManager.Instance is still null after waiting. UI creation failed.");
                    return;
                }
                //MerchantNPCsPlugin.Logger.LogInfo("GUIManager.Instance is available");

                // Check if CustomGUIFront is available and wait if necessary
                attempts = 0;
                while (GUIManager.CustomGUIFront == null && attempts < 5)
                {
                    MerchantNPCsPlugin.Logger.LogWarning($"GUIManager.CustomGUIFront is null. Waiting for it to be available (attempt {attempts + 1}/5)...");
                    System.Threading.Thread.Sleep(500); // Wait for 500ms
                    attempts++;
                }

                if (GUIManager.CustomGUIFront == null)
                {
                    MerchantNPCsPlugin.Logger.LogError("GUIManager.CustomGUIFront is still null after waiting. UI creation failed.");
                    return;
                }
                //MerchantNPCsPlugin.Logger.LogInfo("GUIManager.CustomGUIFront is available");

                //MerchantNPCsPlugin.Logger.LogInfo("Creating main UI panel...");

                // Create the main UI panel
                //MerchantNPCsPlugin.Logger.LogInfo("Creating UI panel with GUIManager.Instance.CreateWoodpanel...");
                try
                {
                    _uiPanel = GUIManager.Instance.CreateWoodpanel(
                        parent: GUIManager.CustomGUIFront.transform,
                        anchorMin: new Vector2(1f, 0.5f),  // Right-aligned anchor
                        anchorMax: new Vector2(1f, 0.5f),  // Right-aligned anchor
                        position: new Vector2(-400, 0),    // Offset to the left from the right edge
                        width: 800,
                        height: 600,
                        draggable: true);

                    // Apply a custom style to the panel background
                    try
                    {
                        // Find the background image component
                        Image panelImage = _uiPanel.GetComponent<Image>();
                        if (panelImage != null)
                        {
                            // Instead of loading a custom sprite, we'll modify the existing one's appearance
                            // Set a custom color tint that matches our theme
                            // Custom background temporarily disabled per user request
                            // Sprite customBackground = LoadCustomBackground();
                            // if (customBackground != null)
                            // {
                            //     // Apply custom background sprite
                            //     panelImage.sprite = customBackground;
                            //     panelImage.color = Color.white; // Reset color to show sprite properly
                            //     MerchantNPCsPlugin.Logger.LogInfo("Applied custom background image");
                            // }
                            // else
                            // {
                            //     // Fall back to blue tint if custom background can't be loaded
                            //     panelImage.color = new Color(0.15f, 0.25f, 0.4f, 1f); // Dark blue tint
                            //     MerchantNPCsPlugin.Logger.LogInfo("Using blue tint background (no custom image found)");
                            // }

                            // Always use the blue tint background
                            panelImage.color = new Color(0.15f, 0.25f, 0.4f, 1f); // Dark blue tint
                            //MerchantNPCsPlugin.Logger.LogInfo("Using blue tint background (custom background disabled)");
                        }
                        else
                        {
                            MerchantNPCsPlugin.Logger.LogError("Could not find Image component on UI panel");
                        }
                    }
                    catch (Exception ex)
                    {
                        MerchantNPCsPlugin.Logger.LogError($"Error applying custom background: {ex.Message}\n{ex.StackTrace}");
                    }

                    if (_uiPanel == null)
                    {
                        MerchantNPCsPlugin.Logger.LogError("CreateWoodpanel returned null");
                        return;
                    }

                    //MerchantNPCsPlugin.Logger.LogInfo("UI panel created successfully");
                    _uiPanel.name = "MerchantUI_Panel";

                    // Create a dedicated message panel that will always appear in front
                    // Display a welcome message that doesn't rely on string interpolation
                    if (string.IsNullOrEmpty(_merchantType))
                    {
                        ShowMerchantMessage("Welcome to my shop!");
                    }
                    else
                    {
                        ShowMerchantMessage($"Welcome to {_merchantType}'s shop!");
                    }

                    // Set the UI panel's sorting order
                    Canvas panelCanvas = _uiPanel.GetComponent<Canvas>();
                    if (panelCanvas != null)
                    {
                        // Set a medium sorting order for the main UI
                        panelCanvas.sortingOrder = 100;
                        //MerchantNPCsPlugin.Logger.LogInfo($"Set UI panel canvas sorting order to {panelCanvas.sortingOrder}");
                    }
                    else
                    {
                        MerchantNPCsPlugin.Logger.LogWarning("Could not find Canvas component on UI panel");
                    }

                    //MerchantNPCsPlugin.Logger.LogInfo("UI panel named 'MerchantUI_Panel'");
                }
                catch (Exception ex)
                {
                    MerchantNPCsPlugin.Logger.LogError($"Error creating UI panel: {ex.Message}\n{ex.StackTrace}");
                    return;
                }

                _uiPanel.name = "MerchantUIPanel";
                _uiPanel.SetActive(false);

                // Create merchant name text
                //MerchantNPCsPlugin.Logger.LogInfo("Creating merchant name text...");
                try
                {
                    GameObject merchantNameObj = GUIManager.Instance.CreateText(
                        text: "Merchant",
                        parent: _uiPanel.transform,
                        anchorMin: new Vector2(0.5f, 1f),
                        anchorMax: new Vector2(0.5f, 1f),
                        position: new Vector2(0, -40),
                        width: 300,
                        height: 40,
                        color: GUIManager.Instance.ValheimOrange,
                        outline: true,
                        outlineColor: Color.black,
                        addContentSizeFitter: false,
                        font: GUIManager.Instance.AveriaSerifBold,
                        fontSize: 25);

                    if (merchantNameObj == null)
                    {
                        MerchantNPCsPlugin.Logger.LogError("CreateText returned null for merchant name text");
                        return;
                    }

                    _merchantNameText = merchantNameObj.GetComponent<Text>();
                    if (_merchantNameText == null)
                    {
                        MerchantNPCsPlugin.Logger.LogError("Failed to get Text component from merchant name object");
                        return;
                    }

                    _merchantNameText.alignment = TextAnchor.MiddleCenter;

                    _merchantNameText.name = "MerchantUI_MerchantNameText";
                    //MerchantNPCsPlugin.Logger.LogInfo("Merchant name text created successfully");
                }
                catch (Exception ex)
                {
                    MerchantNPCsPlugin.Logger.LogError($"Error creating merchant name text: {ex.Message}\n{ex.StackTrace}");
                    return;
                }

                // Add "All prices in coins" text
                try
                {
                    //MerchantNPCsPlugin.Logger.LogInfo("Creating prices info text...");
                    GameObject pricesInfoObj = GUIManager.Instance.CreateText(
                        text: "All prices in coins",
                        parent: _uiPanel.transform,
                        anchorMin: new Vector2(0.5f, 1f),
                        anchorMax: new Vector2(0.5f, 1f),
                        position: new Vector2(0, -70),
                        width: 300,
                        height: 30,
                        color: Color.yellow,
                        outline: true,
                        outlineColor: Color.black,
                        addContentSizeFitter: false,
                        font: GUIManager.Instance.AveriaSerif,
                        fontSize: 16);

                    if (pricesInfoObj != null)
                    {
                        Text pricesInfoText = pricesInfoObj.GetComponent<Text>();
                        if (pricesInfoText != null)
                        {
                            pricesInfoText.alignment = TextAnchor.MiddleCenter;
                            pricesInfoText.name = "MerchantUI_PricesInfoText";
                            //MerchantNPCsPlugin.Logger.LogInfo("Prices info text created successfully");
                        }
                    }
                }
                catch (Exception ex)
                {
                    MerchantNPCsPlugin.Logger.LogError($"Error creating prices info text: {ex.Message}\n{ex.StackTrace}");
                    // Continue with other UI elements
                }

                // Player coins display removed

                // Create buy section
                try
                {
                    //MerchantNPCsPlugin.Logger.LogInfo("Creating buy section title...");
                    GameObject buyTitleObj = GUIManager.Instance.CreateText(
                        text: "Buy",
                        parent: _uiPanel.transform,
                        anchorMin: new Vector2(0, 1),
                        anchorMax: new Vector2(0.5f, 1),
                        position: new Vector2(0, -90),
                        font: GUIManager.Instance.AveriaSerif,
                        fontSize: 20,
                        color: GUIManager.Instance.ValheimYellow,
                        outline: true,
                        outlineColor: Color.black,
                        width: 380,
                        height: 30,
                        addContentSizeFitter: false);

                    if (buyTitleObj == null)
                    {
                        MerchantNPCsPlugin.Logger.LogError("Failed to create buy title object");
                    }
                    else
                    {
                        Text buyTitleText = buyTitleObj.GetComponent<Text>();
                        if (buyTitleText == null)
                        {
                            MerchantNPCsPlugin.Logger.LogError("Failed to get Text component from buy title object");
                        }
                        else
                        {
                            buyTitleText.alignment = TextAnchor.MiddleCenter;
                            //MerchantNPCsPlugin.Logger.LogInfo("Buy section title created successfully");
                        }
                    }
                }
                catch (Exception ex)
                {
                    MerchantNPCsPlugin.Logger.LogError($"Error creating buy section title: {ex.Message}\n{ex.StackTrace}");
                    // Continue with other UI elements instead of returning
                }

                // Create buy search input
                try
                {
                    //MerchantNPCsPlugin.Logger.LogInfo("Creating buy search input...");
                    GameObject buySearchObj = GUIManager.Instance.CreateInputField(
                        parent: _uiPanel.transform,
                        anchorMin: new Vector2(0, 1),
                        anchorMax: new Vector2(0.5f, 1),
                        position: new Vector2(0, -120),
                        width: 380,
                        height: 30);

                    if (buySearchObj == null)
                    {
                        MerchantNPCsPlugin.Logger.LogError("Failed to create buy search input object");
                    }
                    else
                    {
                        _buySearchInput = buySearchObj.GetComponent<InputField>();
                        if (_buySearchInput == null)
                        {
                            MerchantNPCsPlugin.Logger.LogError("Failed to get InputField component from buy search object");
                        }
                        else
                        {
                            if (_buySearchInput.placeholder == null)
                            {
                                MerchantNPCsPlugin.Logger.LogError("Buy search input placeholder is null");
                            }
                            else
                            {
                                Text placeholderText = _buySearchInput.placeholder.GetComponent<Text>();
                                if (placeholderText == null)
                                {
                                    MerchantNPCsPlugin.Logger.LogError("Failed to get Text component from buy search placeholder");
                                }
                                else
                                {
                                    placeholderText.text = "Search...";
                                }
                            }

                            _buySearchInput.onValueChanged.AddListener(OnBuySearchChanged);
                            //MerchantNPCsPlugin.Logger.LogInfo("Buy search input created successfully");
                        }
                    }
                }
                catch (Exception ex)
                {
                    MerchantNPCsPlugin.Logger.LogError($"Error creating buy search input: {ex.Message}\n{ex.StackTrace}");
                    // Continue with other UI elements instead of returning
                }

                // Create buy items container with scroll view
                try
                {
                    //MerchantNPCsPlugin.Logger.LogInfo("Creating buy items container with scroll view...");

                    // Create a container for the buy section
                    GameObject buyContainer = new GameObject("BuyContainer", typeof(RectTransform));
                    buyContainer.transform.SetParent(_uiPanel.transform, false);
                    RectTransform buyContainerRect = buyContainer.GetComponent<RectTransform>();

                    // Position the buy container in the left half of the panel, leaving space for title and search
                    buyContainerRect.anchorMin = new Vector2(0, 0.25f); // Start much lower to leave room for title/search
                    buyContainerRect.anchorMax = new Vector2(0.5f, 0.75f); // End lower to make container shorter
                    buyContainerRect.anchoredPosition = Vector2.zero;
                    buyContainerRect.sizeDelta = Vector2.zero; // Use anchors for sizing

                    //MerchantNPCsPlugin.Logger.LogInfo($"Buy container rect: anchorMin={buyContainerRect.anchorMin}, anchorMax={buyContainerRect.anchorMax}");

                    // Create a simple panel for items
                    GameObject buyItemsPanel = new GameObject("BuyItemsPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    buyItemsPanel.transform.SetParent(buyContainer.transform, false);
                    RectTransform buyItemsPanelRect = buyItemsPanel.GetComponent<RectTransform>();
                    buyItemsPanelRect.anchorMin = new Vector2(0, 0);
                    buyItemsPanelRect.anchorMax = new Vector2(1, 1);
                    buyItemsPanelRect.offsetMin = new Vector2(10, 10); // Left, bottom padding
                    buyItemsPanelRect.offsetMax = new Vector2(-10, -10); // Right, top padding

                    // Set the panel background
                    Image buyPanelImage = buyItemsPanel.GetComponent<Image>();
                    buyPanelImage.color = new Color(0.1f, 0.1f, 0.1f, 0.3f);

                    // Create a scroll rect for the items
                    GameObject buyScrollObj = new GameObject("BuyScroll", typeof(RectTransform), typeof(ScrollRect));
                    buyScrollObj.transform.SetParent(buyItemsPanel.transform, false);
                    RectTransform buyScrollRect = buyScrollObj.GetComponent<RectTransform>();
                    buyScrollRect.anchorMin = new Vector2(0, 0);
                    buyScrollRect.anchorMax = new Vector2(1, 1);
                    buyScrollRect.offsetMin = Vector2.zero;
                    buyScrollRect.offsetMax = Vector2.zero;

                    // Configure the scroll rect
                    ScrollRect buyScrollRect1 = buyScrollObj.GetComponent<ScrollRect>();
                    buyScrollRect1.horizontal = false;
                    buyScrollRect1.vertical = true;

                    // Create a scrollbar for the scroll rect
                    GameObject buyScrollbarObj = new GameObject("BuyScrollbar", typeof(RectTransform), typeof(Scrollbar), typeof(Image));
                    buyScrollbarObj.transform.SetParent(buyScrollObj.transform, false);
                    RectTransform buyScrollbarRect = buyScrollbarObj.GetComponent<RectTransform>();
                    buyScrollbarRect.anchorMin = new Vector2(1, 0);
                    buyScrollbarRect.anchorMax = new Vector2(1, 1);
                    buyScrollbarRect.pivot = new Vector2(1, 0.5f);
                    buyScrollbarRect.anchoredPosition = new Vector2(0, 0);
                    buyScrollbarRect.sizeDelta = new Vector2(20, 0); // Width of scrollbar

                    // Configure the scrollbar
                    Scrollbar buyScrollbar = buyScrollbarObj.GetComponent<Scrollbar>();
                    buyScrollbar.direction = Scrollbar.Direction.BottomToTop;

                    // Set scrollbar colors
                    Image buyScrollbarImage = buyScrollbarObj.GetComponent<Image>();
                    buyScrollbarImage.color = new Color(0.3f, 0.3f, 0.3f, 0.8f);

                    // Create scrollbar handle
                    GameObject buyScrollbarHandle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
                    buyScrollbarHandle.transform.SetParent(buyScrollbarObj.transform, false);
                    RectTransform buyScrollbarHandleRect = buyScrollbarHandle.GetComponent<RectTransform>();
                    buyScrollbarHandleRect.anchorMin = new Vector2(0, 0);
                    buyScrollbarHandleRect.anchorMax = new Vector2(1, 0.2f); // Initial size of handle
                    buyScrollbarHandleRect.sizeDelta = Vector2.zero;
                    buyScrollbarHandleRect.pivot = new Vector2(0.5f, 0.5f);

                    // Set handle color
                    Image buyScrollbarHandleImage = buyScrollbarHandle.GetComponent<Image>();
                    buyScrollbarHandleImage.color = new Color(0.7f, 0.7f, 0.7f, 0.8f);

                    // Connect scrollbar to the scroll rect
                    buyScrollbar.handleRect = buyScrollbarHandleRect;
                    buyScrollRect1.verticalScrollbar = buyScrollbar;
                    buyScrollRect1.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
                    buyScrollRect1.verticalScrollbarSpacing = 5;

                    // Create a viewport
                    GameObject buyViewport = new GameObject("Viewport", typeof(RectTransform), typeof(Mask), typeof(Image));
                    buyViewport.transform.SetParent(buyScrollObj.transform, false);
                    RectTransform buyViewportRect = buyViewport.GetComponent<RectTransform>();
                    buyViewportRect.anchorMin = new Vector2(0, 0);
                    buyViewportRect.anchorMax = new Vector2(1, 1);
                    buyViewportRect.offsetMin = Vector2.zero;
                    buyViewportRect.offsetMax = Vector2.zero;

                    // Configure the viewport mask
                    Mask buyViewportMask = buyViewport.GetComponent<Mask>();
                    buyViewportMask.showMaskGraphic = false;

                    // Set the viewport image (needed for the mask)
                    Image buyViewportImage = buyViewport.GetComponent<Image>();
                    buyViewportImage.color = Color.white;

                    // Create the content container
                    GameObject buyContent = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
                    buyContent.transform.SetParent(buyViewport.transform, false);
                    _buyItemsContainer = buyContent.GetComponent<RectTransform>();
                    _buyItemsContainer.anchorMin = new Vector2(0, 1);
                    _buyItemsContainer.anchorMax = new Vector2(1, 1);
                    _buyItemsContainer.pivot = new Vector2(0.5f, 1);
                    _buyItemsContainer.anchoredPosition = new Vector2(0, -50); // Much larger offset to position well below search bar
                    _buyItemsContainer.sizeDelta = new Vector2(0, 0); // Height will be determined by content

                    // Configure the layout group
                    VerticalLayoutGroup buyLayout = buyContent.GetComponent<VerticalLayoutGroup>();
                    buyLayout.spacing = 10f; // Increased spacing between items
                    buyLayout.padding = new RectOffset(10, 10, 10, 10);
                    buyLayout.childAlignment = TextAnchor.UpperCenter;
                    buyLayout.childControlWidth = true;
                    buyLayout.childControlHeight = false; // Don't let layout control height so our custom heights work
                    buyLayout.childForceExpandWidth = true;
                    buyLayout.childForceExpandHeight = false;
                    buyLayout.reverseArrangement = false; // Ensure items start from the top

                    // Configure the content size fitter
                    ContentSizeFitter buySizeFitter = buyContent.GetComponent<ContentSizeFitter>();
                    buySizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                    buySizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                    // Connect the scroll rect to the viewport and content
                    buyScrollRect1.viewport = buyViewportRect;
                    buyScrollRect1.content = _buyItemsContainer;

                    // Add debug logging for container positioning
                    //MerchantNPCsPlugin.Logger.LogInfo($"Buy container position: anchorMin={_buyItemsContainer.anchorMin}, anchorMax={_buyItemsContainer.anchorMax}, pivot={_buyItemsContainer.pivot}, position={_buyItemsContainer.anchoredPosition}");
                    //MerchantNPCsPlugin.Logger.LogInfo($"Buy viewport position: anchorMin={buyViewportRect.anchorMin}, anchorMax={buyViewportRect.anchorMax}");

                    //MerchantNPCsPlugin.Logger.LogInfo("Buy items container created successfully");
                }
                catch (Exception ex)
                {
                    MerchantNPCsPlugin.Logger.LogError($"Error creating buy items container: {ex.Message}\n{ex.StackTrace}");
                    // Create a fallback container if needed
                    if (_buyItemsContainer == null)
                    {
                        //MerchantNPCsPlugin.Logger.LogInfo("Creating fallback buy items container");
                        GameObject containerObj = new GameObject("BuyItemsContainer", typeof(RectTransform));
                        containerObj.transform.SetParent(_uiPanel.transform, false);
                        _buyItemsContainer = containerObj.GetComponent<RectTransform>();
                    }
                }

                // Create sell section
                try
                {
                    //MerchantNPCsPlugin.Logger.LogInfo("Creating sell section title...");
                    GameObject sellTitleObj = GUIManager.Instance.CreateText(
                        text: "Sell",
                        parent: _uiPanel.transform,
                        anchorMin: new Vector2(0.5f, 1),
                        anchorMax: new Vector2(1, 1),
                        position: new Vector2(0, -90),
                        font: GUIManager.Instance.AveriaSerif,
                        fontSize: 20,
                        color: GUIManager.Instance.ValheimYellow,
                        outline: true,
                        outlineColor: Color.black,
                        width: 380,
                        height: 30,
                        addContentSizeFitter: false);

                    if (sellTitleObj == null)
                    {
                        MerchantNPCsPlugin.Logger.LogError("Failed to create sell title object");
                    }
                    else
                    {
                        Text sellTitleText = sellTitleObj.GetComponent<Text>();
                        if (sellTitleText == null)
                        {
                            MerchantNPCsPlugin.Logger.LogError("Failed to get Text component from sell title object");
                        }
                        else
                        {
                            sellTitleText.alignment = TextAnchor.MiddleCenter;
                            //MerchantNPCsPlugin.Logger.LogInfo("Sell section title created successfully");
                        }
                    }
                }
                catch (Exception ex)
                {
                    MerchantNPCsPlugin.Logger.LogError($"Error creating sell section title: {ex.Message}\n{ex.StackTrace}");
                    // Continue with other UI elements instead of returning
                }

                // Create sell search input
                try
                {
                    //MerchantNPCsPlugin.Logger.LogInfo("Creating sell search input...");
                    GameObject sellSearchObj = GUIManager.Instance.CreateInputField(
                        parent: _uiPanel.transform,
                        anchorMin: new Vector2(0.5f, 1),
                        anchorMax: new Vector2(1, 1),
                        position: new Vector2(0, -120),
                        width: 380,
                        height: 30);

                    if (sellSearchObj == null)
                    {
                        MerchantNPCsPlugin.Logger.LogError("Failed to create sell search input object");
                    }
                    else
                    {
                        _sellSearchInput = sellSearchObj.GetComponent<InputField>();
                        if (_sellSearchInput == null)
                        {
                            MerchantNPCsPlugin.Logger.LogError("Failed to get InputField component from sell search object");
                        }
                        else
                        {
                            if (_sellSearchInput.placeholder == null)
                            {
                                MerchantNPCsPlugin.Logger.LogError("Sell search input placeholder is null");
                            }
                            else
                            {
                                Text placeholderText = _sellSearchInput.placeholder.GetComponent<Text>();
                                if (placeholderText == null)
                                {
                                    MerchantNPCsPlugin.Logger.LogError("Failed to get Text component from sell search placeholder");
                                }
                                else
                                {
                                    placeholderText.text = "Search...";
                                }
                            }

                            _sellSearchInput.onValueChanged.AddListener(OnSellSearchChanged);
                            //MerchantNPCsPlugin.Logger.LogInfo("Sell search input created successfully");
                        }
                    }
                }
                catch (Exception ex)
                {
                    MerchantNPCsPlugin.Logger.LogError($"Error creating sell search input: {ex.Message}\n{ex.StackTrace}");
                    // Continue with other UI elements instead of returning
                }

                // Create sell items container with scroll view
                try
                {
                    //MerchantNPCsPlugin.Logger.LogInfo("Creating sell items container with scroll view...");

                    // Create a container for the sell section
                    GameObject sellContainer = new GameObject("SellContainer", typeof(RectTransform));
                    sellContainer.transform.SetParent(_uiPanel.transform, false);
                    RectTransform sellContainerRect = sellContainer.GetComponent<RectTransform>();

                    // Position the sell container in the right half of the panel, leaving space for title and search
                    sellContainerRect.anchorMin = new Vector2(0.5f, 0.25f); // Start much lower to leave room for title/search
                    sellContainerRect.anchorMax = new Vector2(1, 0.75f); // End lower to make container shorter
                    sellContainerRect.anchoredPosition = Vector2.zero;
                    sellContainerRect.sizeDelta = Vector2.zero; // Use anchors for sizing

                    //MerchantNPCsPlugin.Logger.LogInfo($"Sell container rect: anchorMin={sellContainerRect.anchorMin}, anchorMax={sellContainerRect.anchorMax}");

                    // Create a simple panel for items
                    GameObject sellItemsPanel = new GameObject("SellItemsPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    sellItemsPanel.transform.SetParent(sellContainer.transform, false);
                    RectTransform sellItemsPanelRect = sellItemsPanel.GetComponent<RectTransform>();
                    sellItemsPanelRect.anchorMin = new Vector2(0, 0);
                    sellItemsPanelRect.anchorMax = new Vector2(1, 1);
                    sellItemsPanelRect.offsetMin = new Vector2(10, 10); // Left, bottom padding
                    sellItemsPanelRect.offsetMax = new Vector2(-10, -10); // Right, top padding

                    // Set the panel background
                    Image sellPanelImage = sellItemsPanel.GetComponent<Image>();
                    sellPanelImage.color = new Color(0.1f, 0.1f, 0.1f, 0.3f);

                    // Create a scroll rect for the items
                    GameObject sellScrollObj = new GameObject("SellScroll", typeof(RectTransform), typeof(ScrollRect));
                    sellScrollObj.transform.SetParent(sellItemsPanel.transform, false);
                    RectTransform sellScrollRect = sellScrollObj.GetComponent<RectTransform>();
                    sellScrollRect.anchorMin = new Vector2(0, 0);
                    sellScrollRect.anchorMax = new Vector2(1, 1);
                    sellScrollRect.offsetMin = Vector2.zero;
                    sellScrollRect.offsetMax = Vector2.zero;

                    // Configure the scroll rect
                    ScrollRect sellScrollRect1 = sellScrollObj.GetComponent<ScrollRect>();
                    sellScrollRect1.horizontal = false;
                    sellScrollRect1.vertical = true;

                    // Create a scrollbar for the scroll rect
                    GameObject sellScrollbarObj = new GameObject("SellScrollbar", typeof(RectTransform), typeof(Scrollbar), typeof(Image));
                    sellScrollbarObj.transform.SetParent(sellScrollObj.transform, false);
                    RectTransform sellScrollbarRect = sellScrollbarObj.GetComponent<RectTransform>();
                    sellScrollbarRect.anchorMin = new Vector2(1, 0);
                    sellScrollbarRect.anchorMax = new Vector2(1, 1);
                    sellScrollbarRect.pivot = new Vector2(1, 0.5f);
                    sellScrollbarRect.anchoredPosition = new Vector2(0, 0);
                    sellScrollbarRect.sizeDelta = new Vector2(20, 0); // Width of scrollbar

                    // Configure the scrollbar
                    Scrollbar sellScrollbar = sellScrollbarObj.GetComponent<Scrollbar>();
                    sellScrollbar.direction = Scrollbar.Direction.BottomToTop;

                    // Set scrollbar colors
                    Image sellScrollbarImage = sellScrollbarObj.GetComponent<Image>();
                    sellScrollbarImage.color = new Color(0.3f, 0.3f, 0.3f, 0.8f);

                    // Create scrollbar handle
                    GameObject sellScrollbarHandle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
                    sellScrollbarHandle.transform.SetParent(sellScrollbarObj.transform, false);
                    RectTransform sellScrollbarHandleRect = sellScrollbarHandle.GetComponent<RectTransform>();
                    sellScrollbarHandleRect.anchorMin = new Vector2(0, 0);
                    sellScrollbarHandleRect.anchorMax = new Vector2(1, 0.2f); // Initial size of handle
                    sellScrollbarHandleRect.sizeDelta = Vector2.zero;
                    sellScrollbarHandleRect.pivot = new Vector2(0.5f, 0.5f);

                    // Set handle color
                    Image sellScrollbarHandleImage = sellScrollbarHandle.GetComponent<Image>();
                    sellScrollbarHandleImage.color = new Color(0.7f, 0.7f, 0.7f, 0.8f);

                    // Connect scrollbar to the scroll rect
                    sellScrollbar.handleRect = sellScrollbarHandleRect;
                    sellScrollRect1.verticalScrollbar = sellScrollbar;
                    sellScrollRect1.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
                    sellScrollRect1.verticalScrollbarSpacing = 5;

                    // Create a viewport
                    GameObject sellViewport = new GameObject("Viewport", typeof(RectTransform), typeof(Mask), typeof(Image));
                    sellViewport.transform.SetParent(sellScrollObj.transform, false);
                    RectTransform sellViewportRect = sellViewport.GetComponent<RectTransform>();
                    sellViewportRect.anchorMin = new Vector2(0, 0);
                    sellViewportRect.anchorMax = new Vector2(1, 1);
                    sellViewportRect.offsetMin = Vector2.zero;
                    sellViewportRect.offsetMax = Vector2.zero;

                    // Configure the viewport mask
                    Mask sellViewportMask = sellViewport.GetComponent<Mask>();
                    sellViewportMask.showMaskGraphic = false;

                    // Set the viewport image (needed for the mask)
                    Image sellViewportImage = sellViewport.GetComponent<Image>();
                    sellViewportImage.color = Color.white;

                    // Create the content container
                    GameObject sellContent = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
                    sellContent.transform.SetParent(sellViewport.transform, false);
                    _sellItemsContainer = sellContent.GetComponent<RectTransform>();
                    _sellItemsContainer.anchorMin = new Vector2(0, 1);
                    _sellItemsContainer.anchorMax = new Vector2(1, 1);
                    _sellItemsContainer.pivot = new Vector2(0.5f, 1);
                    _sellItemsContainer.anchoredPosition = new Vector2(0, -50); // Much larger offset to position well below search bar
                    _sellItemsContainer.sizeDelta = new Vector2(0, 0); // Height will be determined by content

                    // Configure the layout group
                    VerticalLayoutGroup sellLayout = sellContent.GetComponent<VerticalLayoutGroup>();
                    sellLayout.spacing = 10f; // Increased spacing between items
                    sellLayout.padding = new RectOffset(10, 10, 10, 10);
                    sellLayout.childAlignment = TextAnchor.UpperCenter;
                    sellLayout.childControlWidth = true;
                    sellLayout.childControlHeight = false; // Don't let layout control height so our custom heights work
                    sellLayout.childForceExpandWidth = true;
                    sellLayout.childForceExpandHeight = false;
                    sellLayout.reverseArrangement = false; // Ensure items start from the top

                    // Configure the content size fitter
                    ContentSizeFitter sellSizeFitter = sellContent.GetComponent<ContentSizeFitter>();
                    sellSizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                    sellSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                    // Connect the scroll rect to the viewport and content
                    sellScrollRect1.viewport = sellViewportRect;
                    sellScrollRect1.content = _sellItemsContainer;

                    // Add debug logging for container positioning
                    //MerchantNPCsPlugin.Logger.LogInfo($"Sell container position: anchorMin={_sellItemsContainer.anchorMin}, anchorMax={_sellItemsContainer.anchorMax}, pivot={_sellItemsContainer.pivot}, position={_sellItemsContainer.anchoredPosition}");
                    //MerchantNPCsPlugin.Logger.LogInfo($"Sell viewport position: anchorMin={sellViewportRect.anchorMin}, anchorMax={sellViewportRect.anchorMax}");

                    //MerchantNPCsPlugin.Logger.LogInfo("Sell items container created successfully");
                }
                catch (Exception ex)
                {
                    MerchantNPCsPlugin.Logger.LogError($"Error creating sell items container: {ex.Message}\n{ex.StackTrace}");
                    // Create a fallback container if needed
                    if (_sellItemsContainer == null)
                    {
                        //MerchantNPCsPlugin.Logger.LogInfo("Creating fallback sell items container");
                        GameObject containerObj = new GameObject("SellItemsContainer", typeof(RectTransform));
                        containerObj.transform.SetParent(_uiPanel.transform, false);
                        _sellItemsContainer = containerObj.GetComponent<RectTransform>();
                    }
                }

                // Create quantity selector
                GameObject quantityObj = GUIManager.Instance.CreateText(
                    text: "Quantity: 1",
                    parent: _uiPanel.transform,
                    anchorMin: new Vector2(0, 0),
                    anchorMax: new Vector2(1, 0),
                    position: new Vector2(0, 120), // Moved up for more space from bottom
                    font: GUIManager.Instance.AveriaSerif,
                    fontSize: 18,
                    color: Color.white,
                    outline: true,
                    outlineColor: Color.black,
                    width: 760,
                    height: 30,
                    addContentSizeFitter: false);
                _quantityText = quantityObj.GetComponent<Text>();
                _quantityText.alignment = TextAnchor.MiddleCenter;

                // Create quantity buttons - rearranged horizontally with 10px spacing
                // Calculate button size (reduced by 20% from previous size)
                float quantityButtonWidth = 115; // 144 * 0.8 = 115.2
                float quantityButtonHeight = 58; // 72 * 0.8 = 57.6

                // Position buttons with x10 in the center and exactly 10px spacing between buttons
                float buttonSpacing = 10f; // Exact spacing of 10px between buttons

                // Calculate positions based on x10 button being in the center (position 0)
                // x10 at center (0), x5 and x1 to the left, x50 and Sell All to the right
                float x10Position = 0f; // Center of the UI
                float x5Position = x10Position - quantityButtonWidth - buttonSpacing;
                float x1Position = x5Position - quantityButtonWidth - buttonSpacing;
                float x50Position = x10Position + quantityButtonWidth + buttonSpacing;
                float sellAllPosition = x50Position + quantityButtonWidth + buttonSpacing;

                // Create trade buttons with updated sizes and positions
                // Calculate button sizes (reduced by 20% from previous size)
                float tradeButtonWidth = 192; // 240 * 0.8 = 192
                float tradeButtonHeight = 58; // 72 * 0.8 = 57.6

                // Buy button - centered under buy window
                GameObject buyButtonObj = GUIManager.Instance.CreateButton(
                    text: "Buy",
                    parent: _uiPanel.transform,
                    anchorMin: new Vector2(0.25f, 0),
                    anchorMax: new Vector2(0.25f, 0),
                    position: new Vector2(0, 100), // Centered under buy window
                    width: tradeButtonWidth,
                    height: tradeButtonHeight);
                _buyButton = buyButtonObj.GetComponent<Button>();
                _buyButton.onClick.AddListener(OnBuyClicked);
                ApplyMerchantButtonStyle(_buyButton); // Apply the bluish theme with gold outline

                // Sell button - centered under sell window
                GameObject sellButtonObj = GUIManager.Instance.CreateButton(
                    text: "Sell",
                    parent: _uiPanel.transform,
                    anchorMin: new Vector2(0.75f, 0),
                    anchorMax: new Vector2(0.75f, 0),
                    position: new Vector2(0, 100), // Centered under sell window
                    width: tradeButtonWidth,
                    height: tradeButtonHeight);
                _sellButton = sellButtonObj.GetComponent<Button>();
                _sellButton.onClick.AddListener(OnSellClicked);
                ApplyMerchantButtonStyle(_sellButton); // Apply the bluish theme with gold outline

                // Create quantity buttons at the bottom row
                // x1 button - leftmost position
                GameObject quantity1Obj = GUIManager.Instance.CreateButton(
                    text: "x1",
                    parent: _uiPanel.transform,
                    anchorMin: new Vector2(0.5f, 0),
                    anchorMax: new Vector2(0.5f, 0),
                    position: new Vector2(x1Position, 30), // Bottom row, leftmost position
                    width: quantityButtonWidth,
                    height: quantityButtonHeight);
                _quantity1Button = quantity1Obj.GetComponent<Button>();
                _quantity1Button.onClick.AddListener(() => SetQuantity(1));
                ApplyMerchantButtonStyle(_quantity1Button);

                // x5 button - second from left
                GameObject quantity5Obj = GUIManager.Instance.CreateButton(
                    text: "x5",
                    parent: _uiPanel.transform,
                    anchorMin: new Vector2(0.5f, 0),
                    anchorMax: new Vector2(0.5f, 0),
                    position: new Vector2(x5Position, 30), // Bottom row, second from left
                    width: quantityButtonWidth,
                    height: quantityButtonHeight);
                _quantity5Button = quantity5Obj.GetComponent<Button>();
                _quantity5Button.onClick.AddListener(() => SetQuantity(5));
                ApplyMerchantButtonStyle(_quantity5Button);

                // x10 button - center position
                GameObject quantity10Obj = GUIManager.Instance.CreateButton(
                    text: "x10",
                    parent: _uiPanel.transform,
                    anchorMin: new Vector2(0.5f, 0),
                    anchorMax: new Vector2(0.5f, 0),
                    position: new Vector2(x10Position, 30), // Bottom row, center position
                    width: quantityButtonWidth,
                    height: quantityButtonHeight);
                _quantity10Button = quantity10Obj.GetComponent<Button>();
                _quantity10Button.onClick.AddListener(() => SetQuantity(10));
                ApplyMerchantButtonStyle(_quantity10Button);

                // x50 button - second from right
                GameObject quantity50Obj = GUIManager.Instance.CreateButton(
                    text: "x50",
                    parent: _uiPanel.transform,
                    anchorMin: new Vector2(0.5f, 0),
                    anchorMax: new Vector2(0.5f, 0),
                    position: new Vector2(x50Position, 30), // Bottom row, second from right
                    width: quantityButtonWidth,
                    height: quantityButtonHeight);
                Button quantity50Button = quantity50Obj.GetComponent<Button>();
                quantity50Button.onClick.AddListener(() => SetQuantity(50));
                ApplyMerchantButtonStyle(quantity50Button);

                // Sell All button - rightmost position
                GameObject sellAllButtonObj = GUIManager.Instance.CreateButton(
                    text: "Sell All",
                    parent: _uiPanel.transform,
                    anchorMin: new Vector2(0.5f, 0),
                    anchorMax: new Vector2(0.5f, 0),
                    position: new Vector2(sellAllPosition, 30), // Bottom row, rightmost position
                    width: quantityButtonWidth,
                    height: quantityButtonHeight);
                Button sellAllButton = sellAllButtonObj.GetComponent<Button>();
                sellAllButton.onClick.AddListener(OnSellAllClicked);
                ApplyMerchantButtonStyle(sellAllButton);

                // Close button - moved below the two rows, centered, size unchanged
                GameObject closeButtonObj = GUIManager.Instance.CreateButton(
                    text: "Close",
                    parent: _uiPanel.transform,
                    anchorMin: new Vector2(0.5f, 0),
                    anchorMax: new Vector2(0.5f, 0),
                    position: new Vector2(0, -40), // Moved below the Sell/Sell All buttons
                    width: 200, // Size unchanged as per requirements
                    height: 60);
                _closeButton = closeButtonObj.GetComponent<Button>();
                _closeButton.onClick.AddListener(Hide);
                ApplyMerchantButtonStyle(_closeButton);

                // Create item button prefab
                _itemButtonPrefab = CreateItemButtonPrefab();

                //MerchantNPCsPlugin.Logger.LogInfo("Created merchant UI successfully");
            }
            catch (Exception ex)
            {
                MerchantNPCsPlugin.Logger.LogError($"Error creating merchant UI: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private GameObject CreateItemButtonPrefab()
        {
            try
            {
                //MerchantNPCsPlugin.Logger.LogInfo("Creating item button prefab...");

                GameObject buttonObj = new GameObject("ItemButtonPrefab", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                if (buttonObj == null)
                {
                    MerchantNPCsPlugin.Logger.LogError("Failed to create button GameObject");
                    return null;
                }
                //MerchantNPCsPlugin.Logger.LogInfo("Created button GameObject");

                RectTransform rectTransform = buttonObj.GetComponent<RectTransform>();
                if (rectTransform == null)
                {
                    MerchantNPCsPlugin.Logger.LogError("Failed to get RectTransform component");
                    return null;
                }
                rectTransform.sizeDelta = new Vector2(360, 60); // Moderate height for good visibility without taking too much space
                //MerchantNPCsPlugin.Logger.LogInfo("Set RectTransform size");

                Image image = buttonObj.GetComponent<Image>();
                if (image == null)
                {
                    MerchantNPCsPlugin.Logger.LogError("Failed to get Image component");
                    return null;
                }
                image.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

                if (GUIManager.Instance == null)
                {
                    MerchantNPCsPlugin.Logger.LogError("GUIManager.Instance is null when getting sprite");
                    return null;
                }

                Sprite buttonSprite = GUIManager.Instance.GetSprite("button");
                if (buttonSprite == null)
                {
                    MerchantNPCsPlugin.Logger.LogError("Failed to get 'button' sprite from GUIManager");
                    return null;
                }

                image.sprite = buttonSprite;
                image.type = Image.Type.Sliced;
                //MerchantNPCsPlugin.Logger.LogInfo("Set Image properties");

                // Item icon
                //MerchantNPCsPlugin.Logger.LogInfo("Creating item icon...");
                GameObject iconObj = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                if (iconObj == null)
                {
                    MerchantNPCsPlugin.Logger.LogError("Failed to create icon GameObject");
                    return null;
                }

                iconObj.transform.SetParent(buttonObj.transform, false);
                RectTransform iconRect = iconObj.GetComponent<RectTransform>();
                if (iconRect == null)
                {
                    MerchantNPCsPlugin.Logger.LogError("Failed to get icon RectTransform component");
                    return null;
                }

                iconRect.anchorMin = new Vector2(0, 0.5f);
                iconRect.anchorMax = new Vector2(0, 0.5f);
                iconRect.pivot = new Vector2(0, 0.5f);
                iconRect.anchoredPosition = new Vector2(10, 0);
                iconRect.sizeDelta = new Vector2(45f, 45f); // Final icon size (adjusted for optimal display)
                //MerchantNPCsPlugin.Logger.LogInfo("Item icon created successfully");

                // Item name
                //MerchantNPCsPlugin.Logger.LogInfo("Creating item name text...");
                GameObject nameObj = new GameObject("Name", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                if (nameObj == null)
                {
                    MerchantNPCsPlugin.Logger.LogError("Failed to create name GameObject");
                    return null;
                }

                nameObj.transform.SetParent(buttonObj.transform, false);
                RectTransform nameRect = nameObj.GetComponent<RectTransform>();
                if (nameRect == null)
                {
                    MerchantNPCsPlugin.Logger.LogError("Failed to get name RectTransform component");
                    return null;
                }

                nameRect.anchorMin = new Vector2(0, 0.5f);
                nameRect.anchorMax = new Vector2(0.55f, 0.5f);
                nameRect.pivot = new Vector2(0, 0.5f);
                nameRect.anchoredPosition = new Vector2(60, 0);
                nameRect.sizeDelta = new Vector2(140, 40); // Wider area for text

                Text nameText = nameObj.GetComponent<Text>();
                if (nameText == null)
                {
                    MerchantNPCsPlugin.Logger.LogError("Failed to get name Text component");
                    return null;
                }

                nameText.text = "Item Name";
                nameText.horizontalOverflow = HorizontalWrapMode.Wrap;
                nameText.verticalOverflow = VerticalWrapMode.Truncate;
                nameText.resizeTextForBestFit = false;
                nameText.alignment = TextAnchor.MiddleLeft;
                // We'll let the text component handle wrapping naturally
                // The horizontalOverflow = Wrap setting will ensure text wraps when it reaches the edge of the rect

                if (GUIManager.Instance == null)
                {
                    MerchantNPCsPlugin.Logger.LogError("GUIManager.Instance is null when setting name font");
                    return null;
                }

                // Use a fallback font if AveriaSerif is null
                if (GUIManager.Instance.AveriaSerif != null)
                {
                    nameText.font = GUIManager.Instance.AveriaSerif;
                }
                else
                {
                    MerchantNPCsPlugin.Logger.LogWarning("AveriaSerif font is null, using default font");
                    // Use the default font
                    nameText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                }

                nameText.fontSize = 16; // Slightly smaller font size to fit more text
                nameText.color = Color.white;
                MerchantNPCsPlugin.Logger.LogInfo("Item name text created successfully");

                // Item price
                //MerchantNPCsPlugin.Logger.LogInfo("Creating item price text...");
                GameObject priceObj = new GameObject("Price", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                if (priceObj == null)
                {
                    MerchantNPCsPlugin.Logger.LogError("Failed to create price GameObject");
                    return null;
                }

                priceObj.transform.SetParent(buttonObj.transform, false);
                RectTransform priceRect = priceObj.GetComponent<RectTransform>();
                if (priceRect == null)
                {
                    MerchantNPCsPlugin.Logger.LogError("Failed to get price RectTransform component");
                    return null;
                }

                priceRect.anchorMin = new Vector2(0.65f, 0.5f);
                priceRect.anchorMax = new Vector2(1, 0.5f);
                priceRect.pivot = new Vector2(0, 0.5f);
                priceRect.anchoredPosition = new Vector2(10, 0);
                priceRect.sizeDelta = new Vector2(90, 40); // Slightly smaller width, positioned further right

                Text priceText = priceObj.GetComponent<Text>();
                if (priceText == null)
                {
                    MerchantNPCsPlugin.Logger.LogError("Failed to get price Text component");
                    return null;
                }

                priceText.text = "100";
                priceText.horizontalOverflow = HorizontalWrapMode.Overflow;
                priceText.verticalOverflow = VerticalWrapMode.Truncate;
                priceText.alignment = TextAnchor.MiddleRight; // Right-align the price text

                // Use a fallback font if AveriaSerif is null
                if (GUIManager.Instance.AveriaSerif != null)
                {
                    priceText.font = GUIManager.Instance.AveriaSerif;
                }
                else
                {
                    MerchantNPCsPlugin.Logger.LogWarning("AveriaSerif font is null for price text, using default font");
                    // Use the default font
                    priceText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                }
                priceText.fontSize = 18; // Increased font size for better readability

                if (GUIManager.Instance.ValheimYellow == null)
                {
                    MerchantNPCsPlugin.Logger.LogWarning("ValheimYellow color is null, using default yellow");
                    // Use a fallback color instead of returning null
                    priceText.color = Color.yellow;
                }
                else
                {
                    priceText.color = GUIManager.Instance.ValheimYellow;
                }

                priceText.alignment = TextAnchor.MiddleRight;
                //MerchantNPCsPlugin.Logger.LogInfo("Item price text created successfully");

                // Add button component
                //MerchantNPCsPlugin.Logger.LogInfo("Configuring button component...");
                Button button = buttonObj.GetComponent<Button>();
                if (button == null)
                {
                    MerchantNPCsPlugin.Logger.LogError("Failed to get Button component");
                    return null;
                }

                ColorBlock colors = button.colors;
                colors.normalColor = new Color(1, 1, 1, 0.8f);
                colors.highlightedColor = new Color(1, 1, 1, 1);
                colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1);
                colors.selectedColor = new Color(1, 1, 1, 1);
                colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
                button.colors = colors;
                //MerchantNPCsPlugin.Logger.LogInfo("Button component configured successfully");

                // Don't destroy the prefab when loading a new scene
                DontDestroyOnLoad(buttonObj);

                // Hide the prefab
                buttonObj.SetActive(false);
                //MerchantNPCsPlugin.Logger.LogInfo("Item button prefab created successfully");

                return buttonObj;
            }
            catch (Exception ex)
            {
                MerchantNPCsPlugin.Logger.LogError($"Error in CreateItemButtonPrefab: {ex.Message}\n{ex.StackTrace}");
                return null;
            }
        }

        /// <summary>
        /// Creates the UI on demand when a merchant is interacted with
        /// </summary>
        private bool CreateUIOnDemand()
        {
            try
            {
                //MerchantNPCsPlugin.Logger.LogInfo("CreateUIOnDemand called");

                // Check if UI is already created and we don't need to recreate it
                if (_uiCreated && IsUIReady() && !_needsRecreation)
                {
                    //MerchantNPCsPlugin.Logger.LogInfo("UI is already created and ready");
                    return true;
                }

                // Reset UI created flag
                _uiCreated = false;

                // Clear any existing UI elements if we're recreating
                if (_needsRecreation)
                {
                    //MerchantNPCsPlugin.Logger.LogInfo("Recreating UI after session change");
                    CleanupUIElements();
                    _needsRecreation = false;
                }

                // Check if GUIManager is available
                if (GUIManager.Instance == null)
                {
                    MerchantNPCsPlugin.Logger.LogError("GUIManager.Instance is null. Cannot create UI on demand.");
                    return false;
                }
                //MerchantNPCsPlugin.Logger.LogInfo("GUIManager.Instance is available");

                if (GUIManager.CustomGUIFront == null)
                {
                    MerchantNPCsPlugin.Logger.LogError("GUIManager.CustomGUIFront is null. Cannot create UI on demand.");
                    return false;
                }
                //MerchantNPCsPlugin.Logger.LogInfo("GUIManager.CustomGUIFront is available");

                // Create the UI
                //MerchantNPCsPlugin.Logger.LogInfo("Calling CreateUI method...");
                try
                {
                    CreateUI();
                    //MerchantNPCsPlugin.Logger.LogInfo("CreateUI method completed");
                }
                catch (Exception ex)
                {
                    MerchantNPCsPlugin.Logger.LogError($"Exception in CreateUI: {ex.Message}\n{ex.StackTrace}");
                    return false;
                }

                // Check if UI was created successfully
                //MerchantNPCsPlugin.Logger.LogInfo("Checking if UI is ready after creation");
                bool uiReady = IsUIReady();
                if (!uiReady)
                {
                    MerchantNPCsPlugin.Logger.LogError("UI creation failed - UI elements are not ready.");
                    return false;
                }

                // Create message panel to ensure it's available and in front of the UI
                // We'll use MessageHud for merchant messages instead of a custom panel

                // Mark UI as created
                _uiCreated = true;

                //MerchantNPCsPlugin.Logger.LogInfo("Successfully created merchant UI on demand");
                return true;
            }
            catch (Exception ex)
            {
                MerchantNPCsPlugin.Logger.LogError($"Error creating UI on demand: {ex.Message}\n{ex.StackTrace}");
                _uiCreated = false;
                return false;
            }
        }

        /// <summary>
        /// Checks if the UI elements are properly initialized and ready to use
        /// </summary>
        private bool IsUIReady()
        {
            bool isReady = true;

            if (_uiPanel == null)
            {
                MerchantNPCsPlugin.Logger.LogError("UI panel is null");
                isReady = false;
            }

            if (_merchantNameText == null)
            {
                MerchantNPCsPlugin.Logger.LogError("Merchant name text is null");
                isReady = false;
            }

            if (_buyItemsContainer == null)
            {
                MerchantNPCsPlugin.Logger.LogError("Buy items container is null");
                isReady = false;
            }

            if (_sellItemsContainer == null)
            {
                MerchantNPCsPlugin.Logger.LogError("Sell items container is null");
                isReady = false;
            }

            if (_quantityText == null)
            {
                MerchantNPCsPlugin.Logger.LogError("Quantity text is null");
                isReady = false;
            }

            if (_buyButton == null)
            {
                MerchantNPCsPlugin.Logger.LogError("Buy button is null");
                isReady = false;
            }

            if (_sellButton == null)
            {
                MerchantNPCsPlugin.Logger.LogError("Sell button is null");
                isReady = false;
            }

            if (_closeButton == null)
            {
                MerchantNPCsPlugin.Logger.LogError("Close button is null");
                isReady = false;
            }

            if (_itemButtonPrefab == null)
            {
                MerchantNPCsPlugin.Logger.LogError("Item button prefab is null");
                isReady = false;
            }

            return isReady;
        }

        /// <summary>
        /// Shows the merchant UI for a specific merchant type
        /// </summary>
        public void Show(string merchantType, Player player)
        {
            try
            {
                //MerchantNPCsPlugin.Logger.LogInfo("MerchantUI.Show method called");

                // Basic parameter validation
                if (string.IsNullOrEmpty(merchantType))
                {
                    MerchantNPCsPlugin.Logger.LogWarning("Cannot show merchant UI: merchantType is null or empty");
                    return;
                }

                if (player == null)
                {
                    MerchantNPCsPlugin.Logger.LogWarning("Cannot show merchant UI: player is null");
                    return;
                }

                //MerchantNPCsPlugin.Logger.LogInfo($"Show parameters: merchantType={merchantType}, player={player.GetPlayerName()}");

                // Always check if we need to recreate the UI after a session change
                if (_needsRecreation || !_uiCreated || !IsUIReady())
                {
                    //MerchantNPCsPlugin.Logger.LogInfo("UI needs creation or recreation");
                    if (!CreateUIOnDemand())
                    {
                        MerchantNPCsPlugin.Logger.LogError("Failed to create UI on demand");
                        player.Message(MessageHud.MessageType.Center, "Error creating merchant UI. Please try again later.");
                        return;
                    }
                    //MerchantNPCsPlugin.Logger.LogInfo("CreateUIOnDemand completed");
                }
                else
                {
                    //MerchantNPCsPlugin.Logger.LogInfo("UI already created and ready");
                }

                // Check if UI is ready after creation
                //MerchantNPCsPlugin.Logger.LogInfo("Checking if UI is ready");
                if (!IsUIReady())
                {
                    MerchantNPCsPlugin.Logger.LogError("Cannot show merchant UI: UI elements not initialized properly");
                    //MerchantNPCsPlugin.Logger.LogInfo("UI elements status:");
                    //MerchantNPCsPlugin.Logger.LogInfo($"_uiPanel: {(_uiPanel == null ? "null" : "not null")}");
                    //MerchantNPCsPlugin.Logger.LogInfo($"_merchantNameText: {(_merchantNameText == null ? "null" : "not null")}");
                    // Player coins display removed
                    //MerchantNPCsPlugin.Logger.LogInfo($"_buyItemsContainer: {(_buyItemsContainer == null ? "null" : "not null")}");
                    //MerchantNPCsPlugin.Logger.LogInfo($"_sellItemsContainer: {(_sellItemsContainer == null ? "null" : "not null")}");
                    //MerchantNPCsPlugin.Logger.LogInfo($"_quantityText: {(_quantityText == null ? "null" : "not null")}");
                    //MerchantNPCsPlugin.Logger.LogInfo($"_buyButton: {(_buyButton == null ? "null" : "not null")}");
                    //MerchantNPCsPlugin.Logger.LogInfo($"_sellButton: {(_sellButton == null ? "null" : "not null")}");
                    //MerchantNPCsPlugin.Logger.LogInfo($"_closeButton: {(_closeButton == null ? "null" : "not null")}");
                    //MerchantNPCsPlugin.Logger.LogInfo($"_itemButtonPrefab: {(_itemButtonPrefab == null ? "null" : "not null")}");
                    player.Message(MessageHud.MessageType.Center, "Merchant UI not ready yet. Please try again later.");
                    return;
                }

                //MerchantNPCsPlugin.Logger.LogInfo("UI is ready, continuing with Show method");

                _player = player;
                _merchantType = merchantType;

                // Get merchant inventory
                if (MerchantManager.Instance == null)
                {
                    MerchantNPCsPlugin.Logger.LogError("MerchantManager instance is null");
                    player.Message(MessageHud.MessageType.Center, "Error: Merchant system not initialized");
                    return;
                }

                _merchantInventory = MerchantManager.Instance.GetMerchantInventory(merchantType);

                if (_merchantInventory == null)
                {
                    MerchantNPCsPlugin.Logger.LogWarning($"No inventory found for merchant type {merchantType}");
                    player.Message(MessageHud.MessageType.Center, $"No inventory found for merchant type {merchantType}");
                    return;
                }

                // Update merchant name
                if (_merchantNameText != null)
                {
                    _merchantNameText.text = merchantType;
                }

                // Update player coins
                // Player coins display removed

                // No discovery status to update anymore

                // Reset selection and quantity
                _selectedBuyItem = null;
                _selectedSellItem = null;
                _currentQuantity = 1;

                if (_quantityText != null)
                {
                    _quantityText.text = $"Quantity: {_currentQuantity}";
                }

                // Reset search filters
                _buySearchFilter = "";
                _sellSearchFilter = "";

                if (_buySearchInput != null)
                {
                    _buySearchInput.text = "";
                }

                if (_sellSearchInput != null)
                {
                    _sellSearchInput.text = "";
                }

                // Populate buy items
                PopulateBuyItems();

                // Populate sell items
                PopulateSellItems();

                // Update button states
                UpdateButtonStates();

                // Create message panel to ensure it's available and in front of the UI
                // We'll use MessageHud for merchant messages instead of a custom panel

                // Show the UI
                if (_uiPanel != null)
                {
                    _uiPanel.SetActive(true);

                    // Block input for the UI
                    GUIManager.BlockInput(true);

                    // We don't need to set player in place mode
                    // This was causing RPC warnings

                    //MerchantNPCsPlugin.Logger.LogInfo($"Successfully opened merchant UI for {merchantType}");
                }
                else
                {
                    MerchantNPCsPlugin.Logger.LogError("Cannot show merchant UI: UI panel is null");
                    player.Message(MessageHud.MessageType.Center, "Error opening merchant UI");
                }
            }
            catch (Exception ex)
            {
                MerchantNPCsPlugin.Logger.LogError($"Error showing merchant UI: {ex.Message}\n{ex.StackTrace}");
                if (player != null)
                {
                    player.Message(MessageHud.MessageType.Center, "Error opening merchant UI");
                }
            }
        }

        /// <summary>
        /// Hides the merchant UI
        /// </summary>
        public void Hide()
        {
            _uiPanel.SetActive(false);

            // Release input control
            GUIManager.BlockInput(false);

            // We don't need to set player out of place mode
            // This was causing RPC warnings
        }

        /// <summary>
        /// Updates the player's coin display
        /// </summary>
        // UpdatePlayerCoins method removed - no longer needed

        /// <summary>
        /// Populates the buy items list
        /// </summary>
        private void PopulateBuyItems()
        {
            try
            {
                //MerchantNPCsPlugin.Logger.LogInfo("PopulateBuyItems called");

                // Check if container exists
                if (_buyItemsContainer == null)
                {
                    MerchantNPCsPlugin.Logger.LogError("Buy items container is null");
                    return;
                }

                // Clear existing items
                foreach (Transform child in _buyItemsContainer)
                {
                    Destroy(child.gameObject);
                }

                if (_merchantInventory == null || _merchantInventory.Items == null)
                {
                    MerchantNPCsPlugin.Logger.LogWarning("Merchant inventory is null or empty");
                    return;
                }

                // Filter items based on search only, not on availability
                var filteredItems = _merchantInventory.Items
                    .Where(item => item.ItemData != null) // Only include items with valid ItemData
                    .Where(item => string.IsNullOrEmpty(_buySearchFilter) ||
                           (GetItemDisplayName(item.ItemName).ToLower().Contains(_buySearchFilter.ToLower()) ||
                            item.ItemData.m_shared.m_name.ToLower().Contains(_buySearchFilter.ToLower())))
                    .ToList();

                //MerchantNPCsPlugin.Logger.LogInfo($"Found {filteredItems.Count} items to display in buy container");

                // Create buttons for each item
                foreach (var item in filteredItems)
                {
                    // Log detailed information about each item for debugging
                    //MerchantNPCsPlugin.Logger.LogInfo($"Buy item: {item.ItemName}, PrefabName: {item.PrefabName}");

                    if (item.ItemData == null)
                    {
                        MerchantNPCsPlugin.Logger.LogWarning($"ItemData is null for item {item.ItemName}");
                        continue;
                    }

                    try
                    {
                        GameObject buttonObj = Instantiate(_itemButtonPrefab, _buyItemsContainer);
                        buttonObj.SetActive(true);

                        // Make sure the RectTransform is properly set up - changed to top anchoring
                        RectTransform rectTransform = buttonObj.GetComponent<RectTransform>();
                        rectTransform.anchorMin = new Vector2(0, 1);
                        rectTransform.anchorMax = new Vector2(1, 1);
                        rectTransform.pivot = new Vector2(0.5f, 1);
                        rectTransform.sizeDelta = new Vector2(0, 60); // Height only, width will be controlled by layout

                        // Log button position for debugging
                        //MerchantNPCsPlugin.Logger.LogInfo($"Buy button position: {buttonObj.name}, anchorMin={rectTransform.anchorMin}, anchorMax={rectTransform.anchorMax}");

                        // Set icon
                        Image iconImage = buttonObj.transform.Find("Icon").GetComponent<Image>();
                        iconImage.sprite = item.ItemData.GetIcon();
                        iconImage.enabled = true;

                        // Set name using our helper method to get a user-friendly display name
                        Text nameText = buttonObj.transform.Find("Name").GetComponent<Text>();
                        string displayName = GetItemDisplayName(item.ItemName);

                        nameText.color = Color.white; // Normal color for all items
                        nameText.text = displayName;
                        nameText.fontSize += 2; // Increase font size

                        // Set price with more visible formatting - just the number
                        Text priceText = buttonObj.transform.Find("Price").GetComponent<Text>();
                        priceText.text = item.BuyPrice.ToString();
                        priceText.fontSize += 2; // Increase font size
                        priceText.alignment = TextAnchor.MiddleRight; // Right-align the price
                        priceText.color = Color.yellow; // Make the price yellow for better visibility

                        // Ensure the price is fully visible by adjusting the RectTransform
                        RectTransform priceRect = priceText.GetComponent<RectTransform>();
                        if (priceRect != null)
                        {
                            priceRect.offsetMin = new Vector2(0, priceRect.offsetMin.y); // Left padding
                            priceRect.offsetMax = new Vector2(-10, priceRect.offsetMax.y); // Right padding (10px from edge)
                        }

                        // Add click handler
                        Button button = buttonObj.GetComponent<Button>();
                        MerchantItem capturedItem = item; // Capture the item for the lambda
                        button.onClick.AddListener(() => SelectBuyItem(capturedItem));

                        // All items are always available in the biome-based system
                        button.interactable = true;

                        // Log that this button is enabled
                        //MerchantNPCsPlugin.Logger.LogInfo($"Button for {item.ItemName} enabled");

                        //MerchantNPCsPlugin.Logger.LogInfo($"Added buy item button for {item.ItemName}");
                    }
                    catch (Exception ex)
                    {
                        MerchantNPCsPlugin.Logger.LogError($"Error creating button for buy item {item.ItemName}: {ex.Message}");
                    }
                }

                // Force layout update
                if (_buyItemsContainer.GetComponent<VerticalLayoutGroup>() != null)
                {
                    LayoutRebuilder.ForceRebuildLayoutImmediate(_buyItemsContainer);
                    //MerchantNPCsPlugin.Logger.LogInfo("Forced layout rebuild for buy items container");

                    // Add detailed debug info about the container
                    //MerchantNPCsPlugin.Logger.LogInfo($"After populating - Buy container rect: pos={_buyItemsContainer.anchoredPosition}, size={_buyItemsContainer.sizeDelta}, childCount={_buyItemsContainer.childCount}");
                    //MerchantNPCsPlugin.Logger.LogInfo($"Buy container layout: spacing={_buyItemsContainer.GetComponent<VerticalLayoutGroup>().spacing}, padding={_buyItemsContainer.GetComponent<VerticalLayoutGroup>().padding}");

                    // Try to make items visible by forcing them to be at the top
                    _buyItemsContainer.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.UpperCenter;

                    // Adjust the height of all item buttons
                    AdjustItemButtonHeights(_buyItemsContainer, 60f); // Moderate height for good visibility without taking too much space
                }

                //MerchantNPCsPlugin.Logger.LogInfo("Buy items populated successfully");
            }
            catch (Exception ex)
            {
                MerchantNPCsPlugin.Logger.LogError($"Error in PopulateBuyItems: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Populates the sell items list
        /// </summary>
        private void PopulateSellItems()
        {
            try
            {
                //MerchantNPCsPlugin.Logger.LogInfo("PopulateSellItems called");

                // Check if container exists
                if (_sellItemsContainer == null)
                {
                    MerchantNPCsPlugin.Logger.LogError("Sell items container is null");
                    return;
                }

                // Clear existing items
                foreach (Transform child in _sellItemsContainer)
                {
                    Destroy(child.gameObject);
                }

                if (_player == null || _player.GetInventory() == null)
                {
                    MerchantNPCsPlugin.Logger.LogWarning("Player or inventory is null");
                    return;
                }

                // Get player inventory items
                List<ItemDrop.ItemData> inventoryItems = _player.GetInventory().GetAllItems();

                // Filter items based on search
                var filteredItems = inventoryItems
                    .Where(item => item.m_shared.m_name != "Coins") // Exclude coins
                    .Where(item => string.IsNullOrEmpty(_sellSearchFilter) ||
                                  GetItemDisplayName(item.m_dropPrefab?.name ?? item.m_shared.m_name).ToLower().Contains(_sellSearchFilter.ToLower()) ||
                                  item.m_shared.m_name.ToLower().Contains(_sellSearchFilter.ToLower()))
                    .ToList();

                //MerchantNPCsPlugin.Logger.LogInfo($"Found {filteredItems.Count} items to display in sell container");

                // Create buttons for each item
                foreach (var item in filteredItems)
                {
                    try
                    {
                        GameObject buttonObj = Instantiate(_itemButtonPrefab, _sellItemsContainer);
                        buttonObj.SetActive(true);

                        // Make sure the RectTransform is properly set up - changed to top anchoring
                        RectTransform rectTransform = buttonObj.GetComponent<RectTransform>();
                        rectTransform.anchorMin = new Vector2(0, 1);
                        rectTransform.anchorMax = new Vector2(1, 1);
                        rectTransform.pivot = new Vector2(0.5f, 1);
                        rectTransform.sizeDelta = new Vector2(0, 60); // Height only, width will be controlled by layout

                        // Log button position for debugging
                        //MerchantNPCsPlugin.Logger.LogInfo($"Sell button position: {buttonObj.name}, anchorMin={rectTransform.anchorMin}, anchorMax={rectTransform.anchorMax}");

                        // Set icon
                        Image iconImage = buttonObj.transform.Find("Icon").GetComponent<Image>();
                        iconImage.sprite = item.GetIcon();
                        iconImage.enabled = true;

                        // Set name using our helper method to get a user-friendly display name
                        Text nameText = buttonObj.transform.Find("Name").GetComponent<Text>();
                        string displayName = GetItemDisplayName(item.m_dropPrefab?.name ?? item.m_shared.m_name);
                        nameText.text = $"{displayName} (x{item.m_stack})";
                        nameText.fontSize += 2; // Increase font size

                        // Find merchant item to get sell price
                        MerchantItem merchantItem = FindMerchantItem(item.m_shared.m_name);
                        int sellPrice = merchantItem != null ? merchantItem.SellPrice : 0;

                        // Skip items that can't be sold (we'll continue the loop)
                        if (sellPrice <= 0)
                        {
                            // Destroy the button since we don't want to show unsellable items
                            Destroy(buttonObj);
                            continue;
                        }

                        // Set price with more visible formatting
                        Text priceText = buttonObj.transform.Find("Price").GetComponent<Text>();
                        priceText.text = sellPrice.ToString();
                        priceText.fontSize += 2; // Increase font size
                        priceText.alignment = TextAnchor.MiddleRight; // Right-align the price
                        priceText.color = Color.yellow; // Make the price yellow for better visibility

                        // Ensure the price is fully visible by adjusting the RectTransform
                        RectTransform priceRect = priceText.GetComponent<RectTransform>();
                        if (priceRect != null)
                        {
                            priceRect.offsetMin = new Vector2(0, priceRect.offsetMin.y); // Left padding
                            priceRect.offsetMax = new Vector2(-10, priceRect.offsetMax.y); // Right padding (10px from edge)
                        }

                        // Add click handler
                        Button button = buttonObj.GetComponent<Button>();
                        ItemDrop.ItemData capturedItem = item; // Capture the item for the lambda
                        button.onClick.AddListener(() => SelectSellItem(capturedItem));

                        //MerchantNPCsPlugin.Logger.LogInfo($"Added sell item button for {item.m_shared.m_name}");
                    }
                    catch (Exception ex)
                    {
                        MerchantNPCsPlugin.Logger.LogError($"Error creating button for sell item {item.m_shared.m_name}: {ex.Message}");
                    }
                }

                // Force layout update
                if (_sellItemsContainer.GetComponent<VerticalLayoutGroup>() != null)
                {
                    LayoutRebuilder.ForceRebuildLayoutImmediate(_sellItemsContainer);
                    //MerchantNPCsPlugin.Logger.LogInfo("Forced layout rebuild for sell items container");

                    // Add detailed debug info about the container
                    //MerchantNPCsPlugin.Logger.LogInfo($"After populating - Sell container rect: pos={_sellItemsContainer.anchoredPosition}, size={_sellItemsContainer.sizeDelta}, childCount={_sellItemsContainer.childCount}");
                    //MerchantNPCsPlugin.Logger.LogInfo($"Sell container layout: spacing={_sellItemsContainer.GetComponent<VerticalLayoutGroup>().spacing}, padding={_sellItemsContainer.GetComponent<VerticalLayoutGroup>().padding}");

                    // Try to make items visible by forcing them to be at the top
                    _sellItemsContainer.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.UpperCenter;

                    // Adjust the height of all item buttons
                    AdjustItemButtonHeights(_sellItemsContainer, 60f); // Moderate height for good visibility without taking too much space
                }

                //MerchantNPCsPlugin.Logger.LogInfo("Sell items populated successfully");
            }
            catch (Exception ex)
            {
                MerchantNPCsPlugin.Logger.LogError($"Error in PopulateSellItems: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Finds a merchant item by name
        /// </summary>
        private MerchantItem FindMerchantItem(string itemName)
        {
            return MerchantManager.Instance.GetMerchantInventory(_merchantType)?.Items
                .FirstOrDefault(item => item.ItemName == itemName ||
                                       (item.ItemData != null && item.ItemData.m_shared.m_name == itemName));
        }

        /// <summary>
        /// Selects a buy item
        /// </summary>
        private void SelectBuyItem(MerchantItem item)
        {
            if (item == null) return;

            //MerchantNPCsPlugin.Logger.LogInfo($"Attempting to select buy item: {item.ItemName}, PrefabName: {item.PrefabName}");

            _selectedBuyItem = item;
            _selectedSellItem = null;
            UpdateButtonStates();
            //MerchantNPCsPlugin.Logger.LogInfo($"Selected buy item: {item.ItemName}");
        }

        /// <summary>
        /// Selects a sell item
        /// </summary>
        private void SelectSellItem(ItemDrop.ItemData item)
        {
            _selectedSellItem = item;
            _selectedBuyItem = null;
            UpdateButtonStates();
        }

        /// <summary>
        /// Sets the quantity for buying/selling
        /// </summary>
        private void SetQuantity(int quantity)
        {
            _currentQuantity = quantity;
            _quantityText.text = $"Quantity: {_currentQuantity}";
        }

        /// <summary>
        /// Loads a sprite from the Resources folder
        /// </summary>
        /// <param name="resourceName">Name of the resource without extension</param>
        /// <returns>Sprite from the Resources folder, or null if not found</returns>
        private Sprite LoadSpriteFromResources(string resourceName)
        {
            try
            {
                // Try to load from Resources folder
                Sprite sprite = Resources.Load<Sprite>(resourceName);
                if (sprite != null)
                {
                    //MerchantNPCsPlugin.Logger.LogInfo($"Loaded sprite from Resources: {resourceName}");
                    return sprite;
                }

                // If we can't find it, log the error
                MerchantNPCsPlugin.Logger.LogWarning($"Could not load sprite from Resources folder: {resourceName}");

                // Return null and let the calling code handle the fallback
                return null;
            }
            catch (System.Exception ex)
            {
                MerchantNPCsPlugin.Logger.LogError($"Error loading sprite from resources: {ex.Message}\n{ex.StackTrace}");
                return null;
            }
        }

        /// <summary>
        /// Applies custom styling to a button to match our merchant theme
        /// </summary>
        /// <param name="button">The button to style</param>
        private void ApplyMerchantButtonStyle(Button button)
        {
            if (button == null) return;

            // Get the colors block and modify it
            ColorBlock colors = button.colors;
            colors.normalColor = MerchantButtonNormal;
            colors.highlightedColor = MerchantButtonHighlighted;
            colors.pressedColor = MerchantButtonPressed;
            colors.selectedColor = MerchantButtonHighlighted;
            button.colors = colors;

            // Update text color if present
            Text buttonText = button.GetComponentInChildren<Text>();
            if (buttonText != null)
            {
                buttonText.color = MerchantTextColor;
                buttonText.fontStyle = FontStyle.Bold;
                // Increase font size by 20%
                buttonText.fontSize = Mathf.RoundToInt(buttonText.fontSize * 1.2f);
            }
        }

        /// <summary>
        /// Shows a message from the merchant in the chat window
        /// </summary>
        /// <param name="text">The message text to display</param>
        public void ShowMerchantMessage(string text)
        {
            try
            {
                //MerchantNPCsPlugin.Logger.LogInfo($"Showing merchant message: {text}");

                // Format the message with the merchant name if needed
                string formattedMessage = text;

                // Check if _merchantType is null or empty
                if (string.IsNullOrEmpty(_merchantType))
                {
                    // Use a default merchant type if none is set
                    formattedMessage = $"Merchant: {text}";
                }
                else if (text != null && !text.Contains(_merchantType))
                {
                    // Add the merchant type prefix if not already present
                    formattedMessage = $"{_merchantType}: {text}";
                }
                else if (text == null)
                {
                    // Handle null text
                    formattedMessage = $"{_merchantType}: Welcome!";
                }

                // Simply display the message in the chat window
                // This avoids RPC calls that cause warnings
                if (Chat.instance != null)
                {
                    // Add it to the chat log
                    Chat.instance.AddString(formattedMessage);
                    //MerchantNPCsPlugin.Logger.LogInfo("Merchant message added to chat log");
                }

                // Also display as a center message for visibility
                if (_player != null)
                {
                    _player.Message(MessageHud.MessageType.Center, formattedMessage);
                    //MerchantNPCsPlugin.Logger.LogInfo("Merchant message displayed as center message");
                }
                else if (Player.m_localPlayer != null)
                {
                    Player.m_localPlayer.Message(MessageHud.MessageType.Center, formattedMessage);
                    //MerchantNPCsPlugin.Logger.LogInfo("Merchant message displayed as center message via local player");
                }
            }
            catch (Exception ex)
            {
                MerchantNPCsPlugin.Logger.LogError($"Error showing merchant message: {ex.Message}\n{ex.StackTrace}");
            }
        }
        private void AdjustItemButtonHeights(RectTransform container, float height)
        {
            if (container == null)
            {
                MerchantNPCsPlugin.Logger.LogWarning("Cannot adjust button heights: container is null");
                return;
            }

            foreach (Transform child in container)
            {
                RectTransform rectTransform = child.GetComponent<RectTransform>();
                if (rectTransform != null)
                {
                    // Keep the width the same, just adjust the height
                    rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x, height);

                    // Add a background to make the item card more visible
                    Image bgImage = child.GetComponent<Image>();
                    if (bgImage != null)
                    {
                        bgImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f); // Darker, more visible background
                    }

                    //MerchantNPCsPlugin.Logger.LogInfo($"Adjusted button height to {height} for {child.name}");
                }
            }
        }

        /// <summary>
        /// Updates the button states based on selection
        /// </summary>
        private void UpdateButtonStates()
        {
            _buyButton.interactable = _selectedBuyItem != null;
            _sellButton.interactable = _selectedSellItem != null && FindMerchantItem(_selectedSellItem.m_shared.m_name) != null;
        }

        /// <summary>
        /// Handles buy search input changes
        /// </summary>
        private void OnBuySearchChanged(string value)
        {
            _buySearchFilter = value;
            PopulateBuyItems();
        }

        /// <summary>
        /// Handles sell search input changes
        /// </summary>
        private void OnSellSearchChanged(string value)
        {
            _sellSearchFilter = value;
            PopulateSellItems();
        }

        /// <summary>
        /// Handles buy button click
        /// </summary>
        private void OnBuyClicked()
        {
            if (_selectedBuyItem != null && _player != null)
            {
                // Adjust quantity if it's more than available stack size
                int quantity = Mathf.Min(_currentQuantity, 999);

                // Try to buy the item
                bool success = MerchantManager.Instance.BuyItem(_player, _selectedBuyItem, quantity);
                if (success)
                {
                    // Update UI
                    // Player coins display removed
                    PopulateSellItems();
                }
            }
        }

        /// <summary>
        /// Handles sell button click
        /// </summary>
        private void OnSellClicked()
        {
            if (_selectedSellItem != null && _player != null)
            {
                // Adjust quantity if it's more than available stack size
                int quantity = Mathf.Min(_currentQuantity, _selectedSellItem.m_stack);

                // Try to sell the item
                bool success = MerchantManager.Instance.SellItem(_player, _selectedSellItem, quantity);
                if (success)
                {
                    // Update UI
                    // Player coins display removed
                    PopulateSellItems();
                    _selectedSellItem = null;
                    UpdateButtonStates();
                }
            }
        }

        /// <summary>
        /// Handles sell all button click - sells all stacks of the currently selected item type
        /// </summary>
        private void OnSellAllClicked()
        {
            if (_player == null || _player.GetInventory() == null) return;

            try
            {
                // Check if an item is selected
                if (_selectedSellItem == null)
                {
                    ShowMerchantMessage("Select an item first before using Sell All.");
                    return;
                }

                string selectedItemName = _selectedSellItem.m_shared.m_name;
                MerchantItem merchantItem = FindMerchantItem(selectedItemName);

                if (merchantItem == null)
                {
                    ShowMerchantMessage("I'm not interested in buying that item.");
                    return;
                }

                int totalSold = 0;
                int totalCoins = 0;

                // Calculate the sell value
                int sellValue = Mathf.RoundToInt(merchantItem.Cost * (MerchantNPCsPlugin.SellPercentage.Value / 100f));
                if (sellValue <= 0)
                {
                    ShowMerchantMessage("That item has no value to me.");
                    return;
                }

                // Find all stacks of the selected item type in the player's inventory
                List<ItemDrop.ItemData> itemsToSell = _player.GetInventory().GetAllItems()
                    .Where(item => item.m_shared.m_name == selectedItemName)
                    .ToList();

                foreach (ItemDrop.ItemData item in itemsToSell)
                {
                    // Sell the item and give coins
                    int stack = item.m_stack;
                    _player.GetInventory().RemoveItem(item.m_shared.m_name, item.m_stack);
                    int coinsEarned = sellValue * stack;
                    MerchantManager.Instance.AddCoins(_player, coinsEarned);

                    // Update counters
                    totalSold += stack;
                    totalCoins += coinsEarned;
                }

                // Refresh the UI
                PopulateSellItems();
                UpdateButtonStates();

                // Reset the selected item since it's been sold
                _selectedSellItem = null;

                // Show a message about what was sold
                if (totalSold > 0)
                {
                    ShowMerchantMessage($"Sold {totalSold} {GetItemDisplayName(selectedItemName)} for {totalCoins} coins.");
                }
                else
                {
                    ShowMerchantMessage("You don't have any of those items to sell.");
                }
            }
            catch (Exception ex)
            {
                MerchantNPCsPlugin.Logger.LogError($"Error in OnSellAllClicked: {ex.Message}\n{ex.StackTrace}");
                ShowMerchantMessage("Something went wrong while selling items.");
            }
        }

        /// <summary>
        /// Load custom background image from config folder
        /// </summary>
        private Sprite LoadCustomBackground()
        {
            try
            {
                // Ensure config directory exists
                if (!Directory.Exists(ConfigFolderPath))
                {
                    Directory.CreateDirectory(ConfigFolderPath);
                    //MerchantNPCsPlugin.Logger.LogInfo($"Created config directory: {ConfigFolderPath}");
                    return null; // No image yet since we just created the directory
                }

                // Check if background image exists
                if (!File.Exists(BackgroundImagePath))
                {
                    //MerchantNPCsPlugin.Logger.LogInfo($"Custom background image not found at: {BackgroundImagePath}");
                    return null;
                }

                // Load image file bytes
                byte[] fileData = File.ReadAllBytes(BackgroundImagePath);
                if (fileData == null || fileData.Length == 0)
                {
                    MerchantNPCsPlugin.Logger.LogWarning("Background image file is empty");
                    return null;
                }

                // Create texture and load image data
                Texture2D texture = new Texture2D(2, 2);

                // Load the image data
                bool loaded = false;
                try
                {
#if UNITY_2019_1_OR_NEWER
                    // Use ImageConversion.LoadImage if available (Unity 2019.1+)
                    loaded = ImageConversion.LoadImage(texture, fileData);
#else
                    // For older Unity versions, use the extension method if available
                    loaded = texture.LoadImage(fileData);
#endif
                }
                catch (Exception ex)
                {
                    MerchantNPCsPlugin.Logger.LogError($"Failed to load image: {ex.Message}");
                    return null;
                }

                if (!loaded)
                {
                    MerchantNPCsPlugin.Logger.LogWarning("Failed to load background image data");
                    return null;
                }

                // Create sprite from texture
                Sprite sprite = Sprite.Create(
                    texture,
                    new Rect(0, 0, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f));

                //MerchantNPCsPlugin.Logger.LogInfo($"Successfully loaded custom background: {texture.width}x{texture.height}");
                return sprite;
            }
            catch (Exception ex)
            {
                MerchantNPCsPlugin.Logger.LogError($"Error loading custom background: {ex.Message}\n{ex.StackTrace}");
                return null;
            }
        }


        /// <summary>
        /// Gets a user-friendly display name for an item
        /// </summary>
        /// <param name="itemName">The item prefab name</param>
        /// <returns>A user-friendly display name</returns>
        private string GetItemDisplayName(string itemName)
        {
            try
            {
                // Ensure the item name has the $item_ prefix for internal use
                string internalItemName = FormatPrefabName(itemName);

                // For display purposes, we'll format the name to be more user-friendly
                string displayName = internalItemName;

                // First try to get the prefab
                GameObject prefab = ZNetScene.instance?.GetPrefab(internalItemName);

                if (prefab != null)
                {
                    // Try to get ItemDrop component
                    ItemDrop itemDrop = prefab.GetComponent<ItemDrop>();
                    if (itemDrop != null && itemDrop.m_itemData != null)
                    {
                        // Get the localized name
                        string localizedName = Localization.instance.Localize(itemDrop.m_itemData.m_shared.m_name);

                        // If the name starts with $, it means localization failed
                        if (!localizedName.StartsWith("$"))
                            return localizedName;
                    }
                }

                // If we get here, we need to create a display name from the internal name
                if (displayName.StartsWith("$item_"))
                {
                    // Remove the $item_ prefix for display only
                    displayName = displayName.Substring(6);

                    // Insert spaces before capital letters
                    displayName = Regex.Replace(displayName, "([a-z])([A-Z])", "$1 $2");

                    // Capitalize first letter of each word
                    displayName = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(displayName.ToLower());
                }

                // For debugging
                // MerchantNPCsPlugin.Logger.LogDebug($"Item name: {itemName}, Internal name: {internalItemName}, Display name: {displayName}");

                return displayName;
            }
            catch (Exception ex)
            {
                MerchantNPCsPlugin.Logger.LogWarning($"Error getting display name for {itemName}: {ex.Message}");

                // Just return the item name as is for fallback
                if (itemName.StartsWith("$item_"))
                    return itemName.Substring(6); // Remove prefix for display
                return itemName;
            }
        }

        /// <summary>
        /// Formats a prefab name to be more user-friendly while preserving the $item_ prefix
        /// </summary>
        /// <param name="prefabName">The raw prefab name</param>
        /// <returns>A formatted, user-friendly name with $item_ prefix</returns>
        private string FormatPrefabName(string prefabName)
        {
            if (string.IsNullOrEmpty(prefabName))
                return "Unknown Item";

            // Ensure the name has the "$item_" prefix for compatibility with the game's localization system
            if (!prefabName.StartsWith("$item_") && !prefabName.StartsWith("$"))
                prefabName = "$item_" + prefabName;

            // Return the name with the $item_ prefix to ensure localization works correctly
            return prefabName;
        }

        /// <summary>
        /// Updates the UI
        /// </summary>
        private void Update()
        {
            // Add null check to prevent NullReferenceException
            if (_uiPanel != null && _uiPanel.activeSelf)
            {
                // Check for escape key to close UI
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    Hide();
                }
            }
        }
    }
}