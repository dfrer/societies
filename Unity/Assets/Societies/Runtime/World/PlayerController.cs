using UnityEngine;
using UnityEngine.InputSystem;

namespace Societies.Runtime.World
{
    /// <summary>
    /// First-person player controller with voxel interaction
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float _walkSpeed = 3f;
        [SerializeField] private float _sprintSpeed = 6f;
        [SerializeField] private float _jumpForce = 5f;
        [SerializeField] private float _gravity = -15f;
        [SerializeField] private float _stepHeight = 0.5f;
        [SerializeField] private float _slopeLimit = 45f;

        [Header("Interaction")]
        [SerializeField] private float _interactionDistance = 5f;
        [SerializeField] private LayerMask _interactionLayers;
        
        [Header("Camera")]
        [SerializeField] private Camera _playerCamera;
        [SerializeField] private float _mouseSensitivity = 2f;
        [SerializeField] private float _lookXLimit = 89f;

        // State
        private CharacterController _characterController;
        private Vector3 _velocity;
        private Vector3 _groundCheckOffset = new(0, -1f, 0);
        private float _groundDistance = 0.2f;
        private bool _isGrounded;
        private bool _isSprinting;
        private bool _isCrouching;
        
        private float _rotationX;
        private float _currentSpeed;
        
        // Interaction
        private BlockCoord _targetBlock;
        private GameObject _targetObject;
        private bool _isMining;
        private float _miningProgress;
        
        // Inventory reference
        private Inventory.InventoryManager _inventory;

        // Input
        private PlayerInput _playerInput;
        private InputAction _moveAction;
        private InputAction _lookAction;
        private InputAction _jumpAction;
        private InputAction _sprintAction;
        private InputAction _mineAction;
        private InputAction _placeAction;
        private InputAction _interactAction;

        // Encumbrance
        private float _encumbrancePercent;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            
            if (_playerCamera == null)
                _playerCamera = GetComponentInChildren<Camera>();
            
            _inventory = GetComponent<Inventory.InventoryManager>();
        }

        private void Start()
        {
            // Setup input
            _playerInput = GetComponent<PlayerInput>();
            if (_playerInput != null)
            {
                _moveAction = _playerInput.actions["Move"];
                _lookAction = _playerInput.actions["Look"];
                _jumpAction = _playerInput.actions["Jump"];
                _sprintAction = _playerInput.actions["Sprint"];
                _mineAction = _playerInput.actions["Mine"];
                _placeAction = _playerInput.actions["Place"];
                _interactAction = _playerInput.actions["Interact"];
            }

            // Lock cursor
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void OnDestroy()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Update()
        {
            UpdateEncumbrance();
            UpdateGroundState();
            UpdateMovement();
            UpdateInteraction();
            UpdateCamera();
        }

        private void UpdateEncumbrance()
        {
            if (_inventory != null)
            {
                _encumbrancePercent = _inventory.EncumbrancePercent;
            }
            else
            {
                _encumbrancePercent = 0f;
            }
        }

        private void UpdateGroundState()
        {
            _isGrounded = _characterController.isGrounded;
            
            if (_isGrounded && _velocity.y < 0)
            {
                _velocity.y = -2f; // Small downward force to keep grounded
            }
        }

        private void UpdateMovement()
        {
            // Get input
            Vector2 moveInput = Vector2.zero;
            if (_moveAction != null)
            {
                moveInput = _moveAction.ReadValue<Vector2>();
            }

            // Sprint check
            _isSprinting = _sprintAction?.IsPressed() ?? false;
            
            // Encumbrance affects movement
            float speedMultiplier = GetSpeedMultiplier();
            
            // Calculate speed
            float targetSpeed = _isSprinting ? _sprintSpeed : _walkSpeed;
            targetSpeed *= speedMultiplier;
            
            _currentSpeed = Mathf.Lerp(_currentSpeed, targetSpeed, Time.deltaTime * 5f);

            // Movement direction
            Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;
            
            // Apply gravity
            if (!_isGrounded)
            {
                _velocity.y += _gravity * Time.deltaTime;
            }

            // Move
            _characterController.Move(move * _currentSpeed * Time.deltaTime);
            _characterController.Move(_velocity * Time.deltaTime);
            
            // Jump
            if (_jumpAction?.WasPressedThisFrame() ?? false)
            {
                if (_isGrounded && _encumbrancePercent < 75f)
                {
                    _velocity.y = _jumpForce;
                }
            }
        }

        private float GetSpeedMultiplier()
        {
            if (_encumbrancePercent < 25f) return 1f;
            if (_encumbrancePercent < 50f) return 0.9f;
            if (_encumbrancePercent < 75f) return 0.7f;
            return 0.5f; // Heavy encumbrance
        }

        private void UpdateInteraction()
        {
            // Raycast for target
            UpdateTarget();

            // Mining
            if (_mineAction?.IsPressed() ?? false)
            {
                if (_targetBlock.Y >= 0)
                {
                    _isMining = true;
                    _miningProgress += Time.deltaTime;
                    
                    // TODO: Check tool compatibility and calculate mining time
                    float miningTime = 1f; // Base mining time
                    
                    if (_miningProgress >= miningTime)
                    {
                        // Harvest the block
                        HarvestBlock(_targetBlock);
                        _miningProgress = 0f;
                    }
                }
            }
            else
            {
                _isMining = false;
                _miningProgress = 0f;
            }

            // Placing
            if (_placeAction?.WasPressedThisFrame() ?? false)
            {
                if (_targetBlock.Y >= 0)
                {
                    // Get block to place from inventory
                    // TODO: Implement block placement
                }
            }

            // Interact
            if (_interactAction?.WasPressedThisFrame() ?? false)
            {
                InteractWithTarget();
            }
        }

        private void UpdateTarget()
        {
            Ray ray = _playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
            RaycastHit hit;
            
            if (Physics.Raycast(ray, out hit, _interactionDistance, _interactionLayers))
            {
                // Hit a collider - find the block position
                Vector3 hitPoint = hit.point;
                Vector3 normal = hit.normal;
                
                // Get block coordinate in front of hit
                BlockCoord hitBlock = BlockCoord.FromVector3Int(Vector3Int.FloorToInt(hitPoint));
                
                // If we hit from below, the block we're targeting is above
                if (normal.y > 0.5f)
                {
                    _targetBlock = new BlockCoord(hitBlock.X, hitBlock.Y + 1, hitBlock.Z);
                }
                else
                {
                    _targetBlock = hitBlock;
                }
                
                _targetObject = hit.collider.gameObject;
            }
            else
            {
                _targetBlock = new BlockCoord(0, -1000, 0); // Invalid
                _targetObject = null;
            }
        }

        private void HarvestBlock(BlockCoord coord)
        {
            if (VoxelWorld.Instance == null) return;
            
            var block = VoxelWorld.Instance.GetBlock(coord);
            if (block.IsAir) return;

            // Get harvest yield
            // TODO: Use harvest rules from spec
            
            // Add to inventory
            if (_inventory != null)
            {
                // TODO: Determine item ID from block type
                // _inventory.TryAddItem(itemId, 1);
            }

            // Remove block
            VoxelWorld.Instance.SetBlock(coord, BlockData.Air);
            
            UnityEngine.Debug.Log($"[Player] Harvested {(BlockType)block.Id} at {coord}");
        }

        private void InteractWithTarget()
        {
            if (_targetObject != null)
            {
                // Check for interactable component
                var interactable = _targetObject.GetComponent<IInteractable>();
                interactable?.Interact(gameObject);
            }
        }

        private void UpdateCamera()
        {
            // Mouse look
            Vector2 lookInput = Vector2.zero;
            if (_lookAction != null)
            {
                lookInput = _lookAction.ReadValue<Vector2>();
            }

            _rotationX += -lookInput.y * _mouseSensitivity;
            _rotationX = Mathf.Clamp(_rotationX, -_lookXLimit, _lookXLimit);

            _playerCamera.transform.localRotation = Quaternion.Euler(_rotationX, 0, 0);
            transform.rotation *= Quaternion.Euler(0, lookInput.x * _mouseSensitivity, 0);
        }

        public Vector3 GetTargetBlockPosition()
        {
            if (_targetBlock.Y < 0) return Vector3.zero;
            return new Vector3(_targetBlock.X + 0.5f, _targetBlock.Y + 0.5f, _targetBlock.Z + 0.5f);
        }

        public bool IsTargetingBlock => _targetBlock.Y >= 0;

        public BlockCoord TargetBlock => _targetBlock;
    }

    public interface IInteractable
    {
        void Interact(GameObject interactor);
    }
}