using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlayerSort
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class PlayerSortPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "w00ds.valheim.playersort";
        public const string PluginName = "PlayerSort";
        public const string PluginVersion = "1.0.0";

        private GameObject _sortButtonObject;
        private float _nextUiCheck;

        private void Awake()
        {
            Logger.LogInfo($"{PluginName} {PluginVersion} loaded");
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextUiCheck)
            {
                return;
            }

            _nextUiCheck = Time.unscaledTime + 1f;

            EnsureSortButton();
        }

        private void EnsureSortButton()
        {
            if (InventoryGui.instance == null)
            {
                return;
            }

            Transform inventoryGui =
                InventoryGui.instance.transform;

            /*
             * From our previous hierarchy dump:
             *
             * InventoryGui
             * └── root
             *     └── Player
             *         ├── ...
             *         └── Container
             *             └── StackAll
             *
             * We resolve this hierarchy explicitly instead
             * of recursively searching the entire UI.
             */

            Transform root = FindDirectChild(
                inventoryGui,
                "root"
            );

            if (root == null)
            {
                return;
            }

            Transform playerPanel = FindDirectChild(
                root,
                "Player"
            );

            if (playerPanel == null)
            {
                return;
            }

            Transform container = FindDirectChild(
                playerPanel,
                "Container"
            );

            if (container == null)
            {
                return;
            }

            Transform buttonTemplate = FindDirectChild(
                container,
                "StackAll"
            );

            if (buttonTemplate == null)
            {
                return;
            }

            /*
             * We do not yet rely on the exact PlayerGrid
             * hierarchy.
             *
             * Search only inside the Player panel.
             */
            Transform playerGrid = FindRecursive(
                playerPanel,
                "PlayerGrid"
            );

            if (playerGrid == null)
            {
                return;
            }

            RectTransform playerGridRect =
                playerGrid as RectTransform;

            RectTransform playerPanelRect =
                playerPanel as RectTransform;

            if (
                playerGridRect == null ||
                playerPanelRect == null
            )
            {
                return;
            }

            if (_sortButtonObject == null)
            {
                CreateSortButton(
                    buttonTemplate,
                    playerPanel
                );
            }

            if (_sortButtonObject != null)
            {
                UpdateSortButtonPosition(
                    playerGridRect,
                    playerPanelRect
                );
            }
        }

        private void CreateSortButton(
            Transform buttonTemplate,
            Transform playerPanel
        )
        {
            GameObject clone = null;

            try
            {
                clone = Instantiate(
                    buttonTemplate.gameObject,
                    playerPanel
                );

                clone.name = "PlayerSort_Sort";

                Button button =
                    clone.GetComponent<Button>();

                if (button == null)
                {
                    Logger.LogWarning(
                        "StackAll template has no Button component"
                    );

                    Destroy(clone);
                    return;
                }

                /*
                 * Remove the original StackAll action.
                 */
                button.onClick =
                    new Button.ButtonClickedEvent();

                /*
                 * Install only our action.
                 */
                button.onClick.AddListener(
                    SortPlayerInventory
                );

                SetButtonText(
                    clone,
                    "Sort"
                );

                RectTransform cloneRect =
                    clone.GetComponent<RectTransform>();

                if (cloneRect == null)
                {
                    Logger.LogWarning(
                        "Sort button has no RectTransform"
                    );

                    Destroy(clone);
                    return;
                }

                /*
                 * Compact button below the player grid.
                 */
                cloneRect.sizeDelta =
                    new Vector2(
                        65f,
                        30f
                    );

                /*
                 * Vanilla StackAll can be inactive while
                 * no chest/container is open.
                 *
                 * Our clone should be available whenever
                 * InventoryGui displays the player inventory.
                 */
                clone.SetActive(true);

                clone.transform.SetAsLastSibling();

                _sortButtonObject = clone;

                Logger.LogInfo(
                    "Sort button created from vanilla StackAll template"
                );
            }
            catch (Exception ex)
            {
                Logger.LogError(
                    $"Could not create Sort button: {ex}"
                );

                if (clone != null)
                {
                    Destroy(clone);
                }

                _sortButtonObject = null;
            }
        }

        private void UpdateSortButtonPosition(
            RectTransform playerGrid,
            RectTransform parent
        )
        {
            if (
                _sortButtonObject == null ||
                playerGrid == null ||
                parent == null
            )
            {
                return;
            }

            RectTransform sortRect =
                _sortButtonObject.GetComponent<RectTransform>();

            if (sortRect == null)
            {
                return;
            }

            /*
             * Get the actual transformed PlayerGrid bounds.
             *
             * This follows UI scaling and inventory size
             * changes instead of relying on hardcoded
             * absolute coordinates.
             */
            Vector3[] corners =
                new Vector3[4];

            playerGrid.GetWorldCorners(
                corners
            );

            /*
             * corners[0] = bottom-left
             */
            Vector3 bottomLeft =
                parent.InverseTransformPoint(
                    corners[0]
                );

            const float margin = 5f;

            sortRect.anchorMin =
                new Vector2(
                    0.5f,
                    0.5f
                );

            sortRect.anchorMax =
                new Vector2(
                    0.5f,
                    0.5f
                );

            sortRect.pivot =
                new Vector2(
                    0.5f,
                    0.5f
                );

            sortRect.localPosition =
                new Vector3(
                    bottomLeft.x +
                    sortRect.rect.width / 2f,

                    bottomLeft.y -
                    margin -
                    sortRect.rect.height / 2f,

                    0f
                );
        }

        private void SortPlayerInventory()
        {
            try
            {
                Player player =
                    Player.m_localPlayer;

                if (player == null)
                {
                    return;
                }

                Inventory inventory =
                    player.GetInventory();

                if (inventory == null)
                {
                    return;
                }

                int width =
                    inventory.GetWidth();

                int height =
                    inventory.GetHeight();

                /*
                 * Row 0 is the hotbar.
                 *
                 * Only normal inventory rows participate
                 * in sorting.
                 */
                List<ItemDrop.ItemData> sortableItems =
                    inventory
                        .GetAllItems()
                        .Where(
                            item =>
                                item != null &&
                                item.m_gridPos.y > 0
                        )
                        .ToList();

                if (sortableItems.Count <= 1)
                {
                    return;
                }

                List<ItemDrop.ItemData> sortedItems =
                    sortableItems
                        .OrderBy(
                            item =>
                                GetSortCategory(item)
                        )
                        .ThenBy(
                            item =>
                                item.m_shared.m_name,
                            StringComparer.Ordinal
                        )
                        .ThenByDescending(
                            item =>
                                item.m_quality
                        )
                        .ThenByDescending(
                            item =>
                                item.m_stack
                        )
                        .ToList();

                int index = 0;

                /*
                 * Start at y = 1.
                 *
                 * y = 0 remains untouched because it is
                 * the player's hotbar.
                 */
                for (int y = 1; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        if (index >= sortedItems.Count)
                        {
                            break;
                        }

                        sortedItems[index].m_gridPos =
                            new Vector2i(
                                x,
                                y
                            );

                        index++;
                    }
                }

                Logger.LogInfo(
                    $"Sorted {sortedItems.Count} player inventory items"
                );
            }
            catch (Exception ex)
            {
                Logger.LogError(
                    $"Inventory sorting failed: {ex}"
                );
            }
        }

        private static int GetSortCategory(
            ItemDrop.ItemData item
        )
        {
            switch (item.m_shared.m_itemType)
            {
                /*
                 * Weapons
                 */
                case ItemDrop.ItemData.ItemType.OneHandedWeapon:
                case ItemDrop.ItemData.ItemType.TwoHandedWeapon:
                case ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft:
                case ItemDrop.ItemData.ItemType.Bow:
                    return 10;

                /*
                 * Tools
                 */
                case ItemDrop.ItemData.ItemType.Tool:
                    return 20;

                /*
                 * Equipment
                 */
                case ItemDrop.ItemData.ItemType.Shield:
                case ItemDrop.ItemData.ItemType.Helmet:
                case ItemDrop.ItemData.ItemType.Chest:
                case ItemDrop.ItemData.ItemType.Legs:
                case ItemDrop.ItemData.ItemType.Shoulder:
                case ItemDrop.ItemData.ItemType.Utility:
                    return 30;

                /*
                 * Food, meads and consumables
                 */
                case ItemDrop.ItemData.ItemType.Consumable:
                    return 40;

                /*
                 * Ammunition
                 */
                case ItemDrop.ItemData.ItemType.Ammo:
                case ItemDrop.ItemData.ItemType.AmmoNonEquipable:
                    return 50;

                /*
                 * Crafting materials
                 */
                case ItemDrop.ItemData.ItemType.Material:
                    return 60;

                /*
                 * Trophies
                 */
                case ItemDrop.ItemData.ItemType.Trophy:
                    return 70;

                /*
                 * Unknown or modded item types.
                 *
                 * Keep these at the end rather than
                 * making assumptions about them.
                 */
                default:
                    return 100;
            }
        }

        private static void SetButtonText(
            GameObject buttonObject,
            string text
        )
        {
            TMP_Text[] labels =
                buttonObject
                    .GetComponentsInChildren<TMP_Text>(
                        true
                    );

            foreach (TMP_Text label in labels)
            {
                label.text = text;
            }
        }

        private static Transform FindDirectChild(
            Transform parent,
            string name
        )
        {
            if (parent == null)
            {
                return null;
            }

            foreach (Transform child in parent)
            {
                if (child.name == name)
                {
                    return child;
                }
            }

            return null;
        }

        private static Transform FindRecursive(
            Transform parent,
            string name
        )
        {
            if (parent == null)
            {
                return null;
            }

            foreach (Transform child in parent)
            {
                if (child.name == name)
                {
                    return child;
                }

                Transform result =
                    FindRecursive(
                        child,
                        name
                    );

                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }
    }
}