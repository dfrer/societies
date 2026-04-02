using UnityEngine;
using UnityEngine.UI;
using Societies.Runtime.Inventory;

namespace Societies.Runtime.UI
{
    /// <summary>
    /// Simple game HUD
    /// </summary>
    public class GameHUD : MonoBehaviour
    {
        [Header("Crosshair")]
        [SerializeField] private RectTransform _crosshair;
        [SerializeField] private Color _crosshairColor = Color.white;

        [Header("Inventory Display")]
        [SerializeField] private Text _weightText;
        [SerializeField] private Slider _weightBar;
        [SerializeField] private Text _slotText;

        [Header("Hotbar")]
        [SerializeField] private RectTransform _hotbarContainer;
        [SerializeField] private GameObject _slotPrefab;
        [SerializeField] private int _hotbarSlots = 9;

        private InventoryManager _inventory;
        private int _selectedSlot;

        private void Start()
        {
            var player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                _inventory = player.GetComponent<InventoryManager>();
                if (_inventory != null)
                {
                    _inventory.OnSlotSelected += OnSlotSelected;
                    _inventory.OnInventoryChanged += OnInventoryChanged;
                }
            }

            CreateHotbar();
            UpdateHUD();
        }

        private void CreateHotbar()
        {
            if (_hotbarContainer == null || _slotPrefab == null) return;

            for (int i = 0; i < _hotbarSlots; i++)
            {
                var slot = Instantiate(_slotPrefab, _hotbarContainer);
                slot.name = $"HotbarSlot_{i}";
            }
        }

        private void Update()
        {
            UpdateHUD();
            HandleInput();
        }

        private void HandleInput()
        {
            // Number keys for slot selection
            for (int i = 0; i < 9; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                {
                    _inventory?.SelectSlot(i);
                }
            }

            // Mouse scroll for slot cycling
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll != 0)
            {
                int dir = scroll > 0 ? -1 : 1;
                _inventory?.CycleSlot(dir);
            }
        }

        private void OnSlotSelected(int slot)
        {
            _selectedSlot = slot;
            UpdateHotbarSelection();
        }

        private void OnInventoryChanged()
        {
            UpdateHUD();
        }

        private void UpdateHUD()
        {
            if (_inventory == null) return;

            // Weight
            if (_weightText != null)
            {
                float weight = _inventory.CurrentWeight;
                float capacity = _inventory.Capacity;
                _weightText.text = $"{weight:F1} / {capacity:F0} kg";
            }

            if (_weightBar != null)
            {
                _weightBar.maxValue = _inventory.Capacity;
                _weightBar.value = _inventory.CurrentWeight;
                
                // Color by encumbrance
                float percent = _inventory.EncumbrancePercent;
                if (percent < 25f)
                    _weightBar.fillRect.GetComponent<Image>().color = Color.green;
                else if (percent < 50f)
                    _weightBar.fillRect.GetComponent<Image>().color = Color.yellow;
                else if (percent < 75f)
                    _weightBar.fillRect.GetComponent<Image>().color = new Color(1f, 0.5f, 0f);
                else
                    _weightBar.fillRect.GetComponent<Image>().color = Color.red;
            }

            // Current slot
            if (_slotText != null)
            {
                var item = _inventory.GetSelectedItem();
                if (!item.IsEmpty)
                    _slotText.text = $"Slot {_selectedSlot + 1}: {item.ItemId} x{item.Quantity}";
                else
                    _slotText.text = $"Slot {_selectedSlot + 1}: Empty";
            }
        }

        private void UpdateHotbarSelection()
        {
            // Highlight selected slot
            // (Simplified - would need proper slot references)
        }

        private void OnDestroy()
        {
            if (_inventory != null)
            {
                _inventory.OnSlotSelected -= OnSlotSelected;
                _inventory.OnInventoryChanged -= OnInventoryChanged;
            }
        }
    }
}