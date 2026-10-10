using System.Collections.Generic;
using UnityEngine;
using VoxelCore;
using VoxelCore.Entities;
using Client.Player;
using Client.Rendering;

namespace Client
{
    /// <summary>
    /// Менеджер клиентского мира и спавнер тестовой игровой локации.
    /// Автоматически генерирует 3x3 чанка с ландшафтом (трава, камень, лестницы, уступы),
    /// строит воксельные меши через ChunkView и размещает игрока с PlayerController.
    /// </summary>
    public class WorldManager : MonoBehaviour
    {
        [Header("Материалы и ссылки")]
        [SerializeField] private Material _voxelMaterial;
        [SerializeField] private PlayerController _playerPrefab;
        [SerializeField] private PlayerController _playerController;

        [Header("Параметры генерации тестовой зоны")]
        [SerializeField] private int _renderDistance = 1; // 1 = 3x3 чанка (-1..1)
        [SerializeField] private Vector3 _spawnPosition = new Vector3(8f, 11f, 8f);

        private readonly Dictionary<Vector2Int, ChunkView> _chunkViews = new Dictionary<Vector2Int, ChunkView>();
        public World World { get; private set; }

#if UNITY_EDITOR
        private void Reset()
        {
            if (_voxelMaterial == null)
            {
                _voxelMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/voxel_mat.mat");
            }

            if (_playerPrefab == null)
            {
                _playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<PlayerController>("Assets/Prefabs/Player.prefab");
            }
        }
#endif

        private void Awake()
        {
            if (_voxelMaterial == null)
            {
#if UNITY_EDITOR
                _voxelMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/voxel_mat.mat");
#endif
            }

            if (_playerPrefab == null)
            {
#if UNITY_EDITOR
                _playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<PlayerController>("Assets/Prefabs/Player.prefab");
#endif
            }

            World = new World();
            GenerateTerrain();
            BuildChunkMeshes();
            SetupPlayer();
        }

        /// <summary>
        /// Процедурная генерация тестового полигона для проверки механик Minecraft.
        /// </summary>
        private void GenerateTerrain()
        {
            // 1. Создаем базовый плоский мир: Бедрок на 0, Камень 1..7, Земля 8..9, Трава на 10
            for (int cx = -_renderDistance; cx <= _renderDistance; cx++)
            {
                for (int cz = -_renderDistance; cz <= _renderDistance; cz++)
                {
                    var chunk = World.GetOrCreateChunk(new Vector2Int(cx, cz));

                    for (int x = 0; x < Chunk.Width; x++)
                    {
                        for (int z = 0; z < Chunk.Width; z++)
                        {
                            chunk.SetVoxel(x, 0, z, new Voxel(BlockType.Bedrock));

                            for (int y = 1; y <= 7; y++)
                            {
                                chunk.SetVoxel(x, y, z, new Voxel(BlockType.Stone));
                            }

                            for (int y = 8; y <= 9; y++)
                            {
                                chunk.SetVoxel(x, y, z, new Voxel(BlockType.Dirt));
                            }

                            chunk.SetVoxel(x, 10, z, new Voxel(BlockType.Grass));
                        }
                    }
                }
            }

            // 2. Строим препятствия на центральном чанке (0, 0):
            // А. Каменная стена высотой 2 блока для проверки блокировки ходьбы
            for (int z = 4; z <= 12; z++)
            {
                World.SetVoxel(12, 11, z, new Voxel(BlockType.Stone));
                World.SetVoxel(12, 12, z, new Voxel(BlockType.Stone));
            }

            // Б. Лестница из блоков для проверки прыжков (высота 1..4 блока)
            for (int step = 0; step < 4; step++)
            {
                int x = 4 + step;
                int z = 4;
                for (int y = 11; y <= 11 + step; y++)
                {
                    World.SetVoxel(x, y, z, new Voxel(BlockType.Wood));
                }
            }

            // В. Возвышенная платформа с обрывом для проверки безопасной походки (Safe Walk / Sneak)
            for (int x = 2; x <= 6; x++)
            {
                for (int z = 8; z <= 12; z++)
                {
                    World.SetVoxel(x, 13, z, new Voxel(BlockType.Stone));
                }
            }
            // Подъем на платформу
            World.SetVoxel(2, 11, 7, new Voxel(BlockType.Wood));
            World.SetVoxel(2, 12, 8, new Voxel(BlockType.Wood));

            // Г. Небольшая яма/каньон (воздух)
            for (int x = 8; x <= 10; x++)
            {
                for (int z = 12; z <= 14; z++)
                {
                    World.SetVoxel(x, 10, z, Voxel.Air);
                    World.SetVoxel(x, 9, z, Voxel.Air);
                }
            }
        }

        /// <summary>
        /// Создание объектов ChunkView и генерация процедурных полигональных сеток.
        /// </summary>
        private void BuildChunkMeshes()
        {
            var chunksRoot = new GameObject("ChunksRoot");
            chunksRoot.transform.SetParent(transform);

            for (int cx = -_renderDistance; cx <= _renderDistance; cx++)
            {
                for (int cz = -_renderDistance; cz <= _renderDistance; cz++)
                {
                    var coord = new Vector2Int(cx, cz);
                    var chunkGo = new GameObject($"Chunk_{cx}_{cz}");
                    chunkGo.transform.SetParent(chunksRoot.transform);

                    var chunkView = chunkGo.AddComponent<ChunkView>();
                    chunkView.Initialize(coord, _voxelMaterial);
                    _chunkViews.Add(coord, chunkView);
                }
            }

            // Перестраиваем все созданные чанки
            foreach (var kvp in _chunkViews)
            {
                kvp.Value.Rebuild(World);
            }
        }

        /// <summary>
        /// Изменяет воксель в мире и мгновенно перестраивает меши затронутого подчанка
        /// (а также смежных подчанков при изменении вокселя на границе).
        /// </summary>
        public bool ModifyVoxel(int worldX, int worldY, int worldZ, Voxel newVoxel)
        {
            if (World == null || worldY < 0 || worldY >= Chunk.Height)
            {
                return false;
            }

            if (!World.SetVoxel(worldX, worldY, worldZ, newVoxel))
            {
                return false;
            }

            int chunkX = worldX >> SubChunk.Shift;
            int chunkZ = worldZ >> SubChunk.Shift;
            int subIndex = worldY >> SubChunk.Shift;

            Vector2Int chunkCoord = new Vector2Int(chunkX, chunkZ);
            if (_chunkViews.TryGetValue(chunkCoord, out ChunkView chunkView))
            {
                chunkView.RebuildSubChunk(subIndex, World);

                int localX = worldX & SubChunk.Mask;
                int localY = worldY & SubChunk.Mask;
                int localZ = worldZ & SubChunk.Mask;

                // Если блок на вертикальной границе подчанка — обновляем смежный подчанк сверху/снизу
                if (localY == 0 && subIndex > 0)
                {
                    chunkView.RebuildSubChunk(subIndex - 1, World);
                }
                else if (localY == SubChunk.Size - 1 && subIndex < Chunk.SubChunksCount - 1)
                {
                    chunkView.RebuildSubChunk(subIndex + 1, World);
                }

                // Если блок на горизонтальной границе чанка — обновляем соседний чанк
                if (localX == 0 && _chunkViews.TryGetValue(new Vector2Int(chunkX - 1, chunkZ), out var westChunk))
                {
                    westChunk.RebuildSubChunk(subIndex, World);
                }
                else if (localX == SubChunk.Size - 1 && _chunkViews.TryGetValue(new Vector2Int(chunkX + 1, chunkZ), out var eastChunk))
                {
                    eastChunk.RebuildSubChunk(subIndex, World);
                }

                if (localZ == 0 && _chunkViews.TryGetValue(new Vector2Int(chunkX, chunkZ - 1), out var southChunk))
                {
                    southChunk.RebuildSubChunk(subIndex, World);
                }
                else if (localZ == SubChunk.Size - 1 && _chunkViews.TryGetValue(new Vector2Int(chunkX, chunkZ + 1), out var northChunk))
                {
                    northChunk.RebuildSubChunk(subIndex, World);
                }
            }

            return true;
        }

        /// <summary>
        /// Настройка и размещение игрока в мире через Player.prefab.
        /// </summary>
        private void SetupPlayer()
        {
            // 1. Если игрок уже вручную размещен на сцене (например, для отладки) — используем его
            if (_playerController == null)
            {
                _playerController = FindFirstObjectByType<PlayerController>();
            }

            // 2. Если в сцене игрока нет — создаем экземпляр из префаба
            if (_playerController == null && _playerPrefab != null)
            {
                _playerController = Instantiate(_playerPrefab, _spawnPosition, Quaternion.identity);
                _playerController.name = "Player";
            }

            if (_playerController == null)
            {
                Debug.LogError("[WorldManager] Не удалось создать игрока: _playerPrefab не назначен!");
                return;
            }

            var playerEntity = new PlayerEntity(1, _spawnPosition);
            _playerController.Initialize(World, playerEntity);
        }
    }
}
