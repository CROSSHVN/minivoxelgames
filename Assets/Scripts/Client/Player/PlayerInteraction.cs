using UnityEngine;
using UnityEngine.InputSystem;
using VoxelCore;
using VoxelCore.Physics;

namespace Client.Player
{
    /// <summary>
    /// Компонент взаимодействия игрока с воксельным миром через DDA Raycast (Minecraft 1.8.9).
    /// Реализует:
    /// - Выделение целевого блока черной рамкой (Selection Outline).
    /// - Разрушение блока по ЛКМ (ломание).
    /// - Установку блока по ПКМ (с проверкой хитбокса игрока, чтобы не застрять в блоке).
    /// - Выбор типа блока колесиком мыши (Pick Block).
    /// </summary>
    public class PlayerInteraction : MonoBehaviour
    {
        [Header("Ссылки на компоненты")]
        [SerializeField] private PlayerController _controller;
        [SerializeField] private WorldManager _worldManager;
        [SerializeField] private Camera _camera;

        [Header("Настройки DDA-рейкастинга")]
        [Tooltip("Дистанция взаимодействия в блоках (в Minecraft равна 4.5 в Survival и 5.0 в Creative).")]
        [SerializeField] private float _reachDistance = 4.5f;

        [Header("Установка блоков")]
        [SerializeField] private BlockType _placeBlockType = BlockType.Stone;

        [Header("Визуализация прицела")]
        [SerializeField] private bool _enableSelectionOutline = true;

        private GameObject _outlineGo;
        private MeshRenderer _outlineRenderer;
        private VoxelHitResult _lastHit;

        public VoxelHitResult CurrentHit => _lastHit;
        public BlockType PlaceBlockType
        {
            get => _placeBlockType;
            set => _placeBlockType = value;
        }

        private void Awake()
        {
            if (_controller == null)
            {
                _controller = GetComponent<PlayerController>();
            }

            if (_worldManager == null)
            {
                _worldManager = FindFirstObjectByType<WorldManager>();
            }

            if (_camera == null)
            {
                _camera = Camera.main;
            }

            CreateSelectionOutline();
        }

        private void Update()
        {
            PerformRaycast();
            HandleInteractionInput();
        }

        /// <summary>
        /// Пускает воксельный луч из камеры по направлению взгляда.
        /// </summary>
        private void PerformRaycast()
        {
            if (_controller == null || _controller.World == null || _camera == null)
            {
                HideOutline();
                return;
            }

            Vector3 origin = _camera.transform.position;
            Vector3 direction = _camera.transform.forward;

            _lastHit = _controller.World.Raycast(origin, direction, _reachDistance);

            if (_lastHit.HasHit && _enableSelectionOutline)
            {
                ShowOutline(_lastHit.BlockPos);
            }
            else
            {
                HideOutline();
            }
        }

        /// <summary>
        /// Обработка нажатий кнопок мыши для разрушения и установки блоков.
        /// </summary>
        private void HandleInteractionInput()
        {
            if (Cursor.lockState != CursorLockMode.Locked || !_lastHit.HasHit || _worldManager == null)
            {
                return;
            }

            var mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            // ЛКМ: Разрушение блока
            if (mouse.leftButton.wasPressedThisFrame)
            {
                _worldManager.ModifyVoxel(_lastHit.BlockPos.x, _lastHit.BlockPos.y, _lastHit.BlockPos.z, Voxel.Air);
            }
            // ПКМ: Установка блока
            else if (mouse.rightButton.wasPressedThisFrame)
            {
                Vector3Int placePos = _lastHit.AdjacentPos;

                // Защита: нельзя поставить блок внутрь собственного хитбокса (AABB) игрока
                AABB blockBox = AABB.FromBlock(placePos.x, placePos.y, placePos.z);
                if (_controller.Entity == null || !_controller.Entity.BoundingBox.Intersects(blockBox))
                {
                    _worldManager.ModifyVoxel(placePos.x, placePos.y, placePos.z, new Voxel(_placeBlockType));
                }
            }
            // СКМ: Pick Block (выбор блока прицела)
            else if (mouse.middleButton.wasPressedThisFrame && !_lastHit.Voxel.IsAir)
            {
                _placeBlockType = _lastHit.Voxel.Type;
            }
        }

        #region Selection Outline Visualization

        private void CreateSelectionOutline()
        {
            _outlineGo = new GameObject("BlockSelectionOutline");
            _outlineGo.transform.SetParent(transform.root);

            var filter = _outlineGo.AddComponent<MeshFilter>();
            _outlineRenderer = _outlineGo.AddComponent<MeshRenderer>();

            // Процедурный меш контура куба (12 ребер из отрезков линий)
            var mesh = new Mesh { name = "SelectionCubeOutline" };

            // Небольшой отступ (-0.002 .. 1.002) для исключения z-fighting с текстурой блока
            const float min = -0.002f;
            const float max = 1.002f;

            mesh.vertices = new Vector3[]
            {
                new Vector3(min, min, min), new Vector3(max, min, min),
                new Vector3(max, max, min), new Vector3(min, max, min),
                new Vector3(min, min, max), new Vector3(max, min, max),
                new Vector3(max, max, max), new Vector3(min, max, max)
            };

            mesh.SetIndices(new int[]
            {
                0, 1, 1, 2, 2, 3, 3, 0, // Передняя грань
                4, 5, 5, 6, 6, 7, 7, 4, // Задняя грань
                0, 4, 1, 5, 2, 6, 3, 7  // Ребра между гранями
            }, MeshTopology.Lines, 0);

            filter.sharedMesh = mesh;

            // Черный полупрозрачный материал линий
            Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            if (shader != null)
            {
                var mat = new Material(shader)
                {
                    color = new Color(0f, 0f, 0f, 0.7f)
                };
                _outlineRenderer.sharedMaterial = mat;
            }

            _outlineGo.SetActive(false);
        }

        private void ShowOutline(Vector3Int blockPos)
        {
            if (_outlineGo != null)
            {
                _outlineGo.transform.position = new Vector3(blockPos.x, blockPos.y, blockPos.z);
                _outlineGo.SetActive(true);
            }
        }

        private void HideOutline()
        {
            if (_outlineGo != null && _outlineGo.activeSelf)
            {
                _outlineGo.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (_outlineGo != null)
            {
                Destroy(_outlineGo);
            }
        }

        #endregion
    }
}
