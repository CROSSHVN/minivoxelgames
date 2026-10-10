using System.Collections.Generic;
using UnityEngine;
using VoxelCore;

namespace Client.Rendering
{
    /// <summary>
    /// Контейнер полигональных данных меша подчанка.
    /// Хранит списки вершин, индексов треугольников, UV и нормалей.
    /// </summary>
    public sealed class MeshData
    {
        public readonly List<Vector3> Vertices = new List<Vector3>();
        public readonly List<int> Triangles = new List<int>();
        public readonly List<Vector2> UVs = new List<Vector2>();
        public readonly List<Vector3> Normals = new List<Vector3>();

        private readonly Vector2[] _tempUVs = new Vector2[4];

        // Статическая таблица смещений вершин для каждой из 6 граней единичного куба
        private static readonly Vector3[] FaceOffsets = new Vector3[]
        {
            // Up (+Y)
            new Vector3(0, 1, 0), new Vector3(0, 1, 1), new Vector3(1, 1, 1), new Vector3(1, 1, 0),
            // Down (-Y)
            new Vector3(0, 0, 1), new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(1, 0, 1),
            // North (+Z)
            new Vector3(1, 0, 1), new Vector3(1, 1, 1), new Vector3(0, 1, 1), new Vector3(0, 0, 1),
            // South (-Z)
            new Vector3(0, 0, 0), new Vector3(0, 1, 0), new Vector3(1, 1, 0), new Vector3(1, 0, 0),
            // East (+X)
            new Vector3(1, 0, 0), new Vector3(1, 1, 0), new Vector3(1, 1, 1), new Vector3(1, 0, 1),
            // West (-X)
            new Vector3(0, 0, 1), new Vector3(0, 1, 1), new Vector3(0, 1, 0), new Vector3(0, 0, 0)
        };

        // Статическая таблица нормалей для каждой грани
        private static readonly Vector3[] FaceNormals = new Vector3[]
        {
            Vector3.up,
            Vector3.down,
            Vector3.forward,
            Vector3.back,
            Vector3.right,
            Vector3.left
        };

        private static readonly Bounds SubChunkBounds = new Bounds(
            new Vector3(SubChunk.Size * 0.5f, SubChunk.Size * 0.5f, SubChunk.Size * 0.5f),
            new Vector3(SubChunk.Size, SubChunk.Size, SubChunk.Size)
        );

        public bool IsEmpty => Vertices.Count == 0;

        public void Clear()
        {
            Vertices.Clear();
            Triangles.Clear();
            UVs.Clear();
            Normals.Clear();
        }

        /// <summary>
        /// Добавление грани куба по координатам вокселя (x, y, z) и типу грани.
        /// Вершины вычисляются через статическую таблицу смещений FaceOffsets.
        /// </summary>
        public void AddFace(int x, int y, int z, BlockFace face, BlockType blockType)
        {
            int startIndex = Vertices.Count;
            int offsetIdx = (int)face * 4;

            Vector3 o0 = FaceOffsets[offsetIdx + 0];
            Vector3 o1 = FaceOffsets[offsetIdx + 1];
            Vector3 o2 = FaceOffsets[offsetIdx + 2];
            Vector3 o3 = FaceOffsets[offsetIdx + 3];

            Vertices.Add(new Vector3(x + o0.x, y + o0.y, z + o0.z));
            Vertices.Add(new Vector3(x + o1.x, y + o1.y, z + o1.z));
            Vertices.Add(new Vector3(x + o2.x, y + o2.y, z + o2.z));
            Vertices.Add(new Vector3(x + o3.x, y + o3.y, z + o3.z));

            Triangles.Add(startIndex + 0);
            Triangles.Add(startIndex + 1);
            Triangles.Add(startIndex + 2);

            Triangles.Add(startIndex + 0);
            Triangles.Add(startIndex + 2);
            Triangles.Add(startIndex + 3);

            Vector3 normal = FaceNormals[(int)face];
            Normals.Add(normal);
            Normals.Add(normal);
            Normals.Add(normal);
            Normals.Add(normal);

            BlockAtlas.GetFaceUVs(blockType, face, _tempUVs, 0);
            UVs.Add(_tempUVs[0]);
            UVs.Add(_tempUVs[1]);
            UVs.Add(_tempUVs[2]);
            UVs.Add(_tempUVs[3]);
        }

        /// <summary>
        /// Низкоуровневое добавление грани с произвольными вершинами.
        /// </summary>
        public void AddFace(
            Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3,
            Vector3 normal, BlockType blockType, BlockFace face)
        {
            int startIndex = Vertices.Count;

            Vertices.Add(v0);
            Vertices.Add(v1);
            Vertices.Add(v2);
            Vertices.Add(v3);

            Triangles.Add(startIndex + 0);
            Triangles.Add(startIndex + 1);
            Triangles.Add(startIndex + 2);

            Triangles.Add(startIndex + 0);
            Triangles.Add(startIndex + 2);
            Triangles.Add(startIndex + 3);

            Normals.Add(normal);
            Normals.Add(normal);
            Normals.Add(normal);
            Normals.Add(normal);

            BlockAtlas.GetFaceUVs(blockType, face, _tempUVs, 0);
            UVs.Add(_tempUVs[0]);
            UVs.Add(_tempUVs[1]);
            UVs.Add(_tempUVs[2]);
            UVs.Add(_tempUVs[3]);
        }

        public void ApplyToMesh(Mesh mesh)
        {
            mesh.Clear(false);
            if (IsEmpty)
            {
                return;
            }

            mesh.SetVertices(Vertices);
            mesh.SetTriangles(Triangles, 0, false);
            mesh.SetUVs(0, UVs);
            mesh.SetNormals(Normals);
            mesh.bounds = SubChunkBounds;
        }
    }

    /// <summary>
    /// Процедурный генератор полигональной сетки подчанка с гибридным Face Culling.
    /// </summary>
    public static class SubChunkMeshBuilder
    {
        /// <summary>
        /// Построение меша для указанного подчанка в мире.
        /// </summary>
        public static void Build(World world, Vector2Int chunkCoord, int subChunkIndex, MeshData meshData)
        {
            meshData.Clear();

            if (!world.TryGetChunk(chunkCoord, out Chunk chunk))
            {
                return;
            }

            SubChunk subChunk = chunk.GetSubChunk(subChunkIndex);
            if (subChunk == null)
            {
                return;
            }

            int originX = chunkCoord.x * Chunk.Width;
            int originY = subChunkIndex * SubChunk.Size;
            int originZ = chunkCoord.y * Chunk.Width;

            for (int x = 0; x < SubChunk.Size; x++)
            {
                for (int y = 0; y < SubChunk.Size; y++)
                {
                    for (int z = 0; z < SubChunk.Size; z++)
                    {
                        Voxel voxel = subChunk.GetVoxel(x, y, z);
                        if (voxel.IsAir)
                        {
                            continue;
                        }

                        int worldX = originX + x;
                        int worldY = originY + y;
                        int worldZ = originZ + z;

                        // 1. Up (+Y)
                        if (y < SubChunk.Size - 1 ? subChunk.GetVoxel(x, y + 1, z).IsAir : world.GetVoxel(worldX, worldY + 1, worldZ).IsAir)
                            meshData.AddFace(x, y, z, BlockFace.Up, voxel.Type);

                        // 2. Down (-Y)
                        if (y > 0 ? subChunk.GetVoxel(x, y - 1, z).IsAir : world.GetVoxel(worldX, worldY - 1, worldZ).IsAir)
                            meshData.AddFace(x, y, z, BlockFace.Down, voxel.Type);

                        // 3. North (+Z)
                        if (z < SubChunk.Size - 1 ? subChunk.GetVoxel(x, y, z + 1).IsAir : world.GetVoxel(worldX, worldY, worldZ + 1).IsAir)
                            meshData.AddFace(x, y, z, BlockFace.North, voxel.Type);

                        // 4. South (-Z)
                        if (z > 0 ? subChunk.GetVoxel(x, y, z - 1).IsAir : world.GetVoxel(worldX, worldY, worldZ - 1).IsAir)
                            meshData.AddFace(x, y, z, BlockFace.South, voxel.Type);

                        // 5. East (+X)
                        if (x < SubChunk.Size - 1 ? subChunk.GetVoxel(x + 1, y, z).IsAir : world.GetVoxel(worldX + 1, worldY, worldZ).IsAir)
                            meshData.AddFace(x, y, z, BlockFace.East, voxel.Type);

                        // 6. West (-X)
                        if (x > 0 ? subChunk.GetVoxel(x - 1, y, z).IsAir : world.GetVoxel(worldX - 1, worldY, worldZ).IsAir)
                            meshData.AddFace(x, y, z, BlockFace.West, voxel.Type);
                    }
                }
            }
        }
    }
}
