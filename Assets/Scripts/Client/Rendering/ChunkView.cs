using UnityEngine;
using VoxelCore;

namespace Client.Rendering
{
    /// <summary>
    /// Представление чанка в сцене Unity.
    /// Управляет 8 дочерними подчанками SubChunkView и запускает их мешинг.
    /// </summary>
    public sealed class ChunkView : MonoBehaviour
    {
        [SerializeField] private Material _material;

        public Vector2Int Coord { get; private set; }

        private SubChunkView[] _subViews;
        private readonly MeshData _reusableMeshData = new MeshData();

        /// <summary>
        /// Инициализация чанка: установка мировой позиции и создание 8 дочерних подчанков.
        /// </summary>
        public void Initialize(Vector2Int coord, Material material)
        {
            Coord = coord;
            _material = material;

            // Позиционируем чанк в мировых координатах Unity
            transform.position = new Vector3(coord.x * Chunk.Width, 0, coord.y * Chunk.Width);

            if (_subViews == null)
            {
                _subViews = new SubChunkView[Chunk.SubChunksCount];

                for (int i = 0; i < Chunk.SubChunksCount; i++)
                {
                    var subGo = new GameObject($"SubChunk_{i}");
                    subGo.transform.SetParent(transform, false);
                    subGo.transform.localPosition = new Vector3(0, i * SubChunk.Size, 0);

                    _subViews[i] = subGo.AddComponent<SubChunkView>();
                }
            }
        }

        /// <summary>
        /// Перестроение мешей всех 8 подчанков чанка с использованием кэша соседних чанков (ChunkNeighbors).
        /// Исключает повторный опрос словаря мира для каждого подчанка.
        /// </summary>
        public void Rebuild(World world)
        {
            var chunkNeighbors = new ChunkNeighbors(world, Coord);
            for (int i = 0; i < Chunk.SubChunksCount; i++)
            {
                var subNeighbors = chunkNeighbors.GetSubChunkNeighbors(i);
                RebuildSubChunkWithNeighbors(i, in subNeighbors);
            }
        }

        /// <summary>
        /// Перестроение меша только одного конкретного подчанка (0..7).
        /// </summary>
        public void RebuildSubChunk(int subIndex, World world)
        {
            if (_subViews == null || subIndex < 0 || subIndex >= Chunk.SubChunksCount)
            {
                return;
            }

            var neighbors = SubChunkNeighbors.Create(world, Coord, subIndex);
            RebuildSubChunkWithNeighbors(subIndex, in neighbors);
        }

        /// <summary>
        /// Перестроение меша подчанка с готовым кэшем соседей.
        /// </summary>
        public void RebuildSubChunkWithNeighbors(int subIndex, in SubChunkNeighbors neighbors)
        {
            if (_subViews == null || subIndex < 0 || subIndex >= Chunk.SubChunksCount)
            {
                return;
            }

            SubChunkMeshBuilder.Build(in neighbors, _reusableMeshData);
            _subViews[subIndex].ApplyMeshData(_reusableMeshData, _material);
        }
    }
}
