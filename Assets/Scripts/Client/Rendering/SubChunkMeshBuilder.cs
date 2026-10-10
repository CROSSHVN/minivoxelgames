using System.Collections.Generic;
using UnityEngine;
using VoxelCore;

namespace Client.Rendering
{
    /// <summary>
    /// Контейнер полигональных данных меша подчанка.
    /// </summary>
    public sealed class MeshData
    {
        public readonly List<Vector3> Vertices = new List<Vector3>();
        public readonly List<int> Triangles = new List<int>();
        public readonly List<Vector2> UVs = new List<Vector2>();
        public readonly List<Vector3> Normals = new List<Vector3>();

        private readonly Vector2[] _tempUVs = new Vector2[4];

        public bool IsEmpty => Vertices.Count == 0;

        public void Clear()
        {
            Vertices.Clear();
            Triangles.Clear();
            UVs.Clear();
            Normals.Clear();
        }

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
            mesh.Clear();
            if (IsEmpty)
            {
                return;
            }

            mesh.SetVertices(Vertices);
            mesh.SetTriangles(Triangles, 0);
            mesh.SetUVs(0, UVs);
            mesh.SetNormals(Normals);
        }
    }

    /// <summary>
    /// Процедурный генератор полигональной сетки подчанка с алгоритмом Face Culling.
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
                        if (world.GetVoxel(worldX, worldY + 1, worldZ).IsAir)
                        {
                            meshData.AddFace(
                                new Vector3(x, y + 1, z),
                                new Vector3(x, y + 1, z + 1),
                                new Vector3(x + 1, y + 1, z + 1),
                                new Vector3(x + 1, y + 1, z),
                                Vector3.up, voxel.Type, BlockFace.Up
                            );
                        }

                        // 2. Down (-Y)
                        if (world.GetVoxel(worldX, worldY - 1, worldZ).IsAir)
                        {
                            meshData.AddFace(
                                new Vector3(x, y, z + 1),
                                new Vector3(x, y, z),
                                new Vector3(x + 1, y, z),
                                new Vector3(x + 1, y, z + 1),
                                Vector3.down, voxel.Type, BlockFace.Down
                            );
                        }

                        // 3. North (+Z)
                        if (world.GetVoxel(worldX, worldY, worldZ + 1).IsAir)
                        {
                            meshData.AddFace(
                                new Vector3(x + 1, y, z + 1),
                                new Vector3(x + 1, y + 1, z + 1),
                                new Vector3(x, y + 1, z + 1),
                                new Vector3(x, y, z + 1),
                                Vector3.forward, voxel.Type, BlockFace.North
                            );
                        }

                        // 4. South (-Z)
                        if (world.GetVoxel(worldX, worldY, worldZ - 1).IsAir)
                        {
                            meshData.AddFace(
                                new Vector3(x, y, z),
                                new Vector3(x, y + 1, z),
                                new Vector3(x + 1, y + 1, z),
                                new Vector3(x + 1, y, z),
                                Vector3.back, voxel.Type, BlockFace.South
                            );
                        }

                        // 5. East (+X)
                        if (world.GetVoxel(worldX + 1, worldY, worldZ).IsAir)
                        {
                            meshData.AddFace(
                                new Vector3(x + 1, y, z),
                                new Vector3(x + 1, y + 1, z),
                                new Vector3(x + 1, y + 1, z + 1),
                                new Vector3(x + 1, y, z + 1),
                                Vector3.right, voxel.Type, BlockFace.East
                            );
                        }

                        // 6. West (-X)
                        if (world.GetVoxel(worldX - 1, worldY, worldZ).IsAir)
                        {
                            meshData.AddFace(
                                new Vector3(x, y, z + 1),
                                new Vector3(x, y + 1, z + 1),
                                new Vector3(x, y + 1, z),
                                new Vector3(x, y, z),
                                Vector3.left, voxel.Type, BlockFace.West
                            );
                        }
                    }
                }
            }
        }
    }
}
