using System;
using System.Diagnostics;
using NUnit.Framework;
using UnityEngine;
using VoxelCore;
using Debug = UnityEngine.Debug;

public class VoxelBenchmark
{
    private const int MathIterations = 10_000_000;
    private const int VoxelAccessIterations = 5_000_000;

    [Test]
    public void Benchmark_CoordinateMath()
    {
        // Разогрев JIT
        for (int i = -100; i < 100; i++)
        {
            _ = World.WorldToChunkCoord(i, i);
            _ = World.WorldToLocalCoord(i);
        }

        var sw = Stopwatch.StartNew();

        int sum = 0;
        for (int i = -MathIterations / 2; i < MathIterations / 2; i++)
        {
            Vector2Int chunkCoord = World.WorldToChunkCoord(i, i);
            int localX = World.WorldToLocalCoord(i);
            sum += chunkCoord.x + localX;
        }

        sw.Stop();
        Debug.Log($"[BENCHMARK] CoordinateMath: {sw.ElapsedMilliseconds} ms for {MathIterations:N0} iterations (Checksum: {sum})");
    }

    [Test]
    public void Benchmark_WorldGetVoxel()
    {
        var world = new World();
        // Создаем область 4x4 чанка (16 чанков)
        for (int cx = -2; cx < 2; cx++)
        {
            for (int cz = -2; cz < 2; cz++)
            {
                var chunk = world.GetOrCreateChunk(new Vector2Int(cx, cz));
                for (int x = 0; x < 16; x++)
                    for (int z = 0; z < 16; z++)
                        chunk.SetVoxel(x, 30, z, new Voxel(BlockType.Stone));
            }
        }

        // Разогрев
        for (int i = 0; i < 1000; i++)
        {
            _ = world.GetVoxel(i % 32 - 16, 30, i % 32 - 16);
        }

        var sw = Stopwatch.StartNew();

        int solidCount = 0;
        for (int i = 0; i < VoxelAccessIterations; i++)
        {
            // Случайный доступ в пределах созданных чанков (-32..31 по X и Z)
            int x = (i % 64) - 32;
            int z = ((i * 7) % 64) - 32;
            int y = 30;

            var v = world.GetVoxel(x, y, z);
            if (v.IsSolid) solidCount++;
        }

        sw.Stop();
        Debug.Log($"[BENCHMARK] WorldGetVoxel: {sw.ElapsedMilliseconds} ms for {VoxelAccessIterations:N0} iterations (Solid found: {solidCount:N0})");
    }

    [Test]
    public void Benchmark_MemoryUsage_100_Chunks()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long memBefore = GC.GetTotalMemory(true);

        var world = new World();
        // Создаем 100 чанков (область 10x10)
        for (int cx = 0; cx < 10; cx++)
        {
            for (int cz = 0; cz < 10; cz++)
            {
                var chunk = world.GetOrCreateChunk(new Vector2Int(cx, cz));
                // Заполняем только нижние 64 блока (4 подчанка из 8)
                for (int x = 0; x < 16; x++)
                {
                    for (int z = 0; z < 16; z++)
                    {
                        for (int y = 0; y < 64; y++)
                        {
                            chunk.SetVoxel(x, y, z, new Voxel(BlockType.Stone));
                        }
                    }
                }
            }
        }

        long memAfter = GC.GetTotalMemory(true);
        long allocatedBytes = memAfter - memBefore;
        double allocatedMb = allocatedBytes / (1024.0 * 1024.0);

        Debug.Log($"[BENCHMARK] Memory for 100 chunks (half full): {allocatedMb:F2} MB ({allocatedBytes:N0} bytes)");
    }

    [Test]
    public void Benchmark_SubChunkMeshing()
    {
        var world = new World();
        // 3x3 чанка для реалистичных границ
        for (int cx = -1; cx <= 1; cx++)
        {
            for (int cz = -1; cz <= 1; cz++)
            {
                var chunk = world.GetOrCreateChunk(new Vector2Int(cx, cz));
                for (int x = 0; x < 16; x++)
                {
                    for (int z = 0; z < 16; z++)
                    {
                        for (int y = 0; y < 8; y++)
                        {
                            chunk.SetVoxel(x, y, z, new Voxel(BlockType.Stone));
                        }
                        chunk.SetVoxel(x, 8, z, new Voxel(BlockType.Grass));
                    }
                }
            }
        }

        var meshData = new Client.Rendering.MeshData();

        // Прогрев JIT
        for (int i = 0; i < 50; i++)
        {
            Client.Rendering.SubChunkMeshBuilder.Build(world, Vector2Int.zero, 0, meshData);
        }

        const int iterations = 1000;
        var sw = Stopwatch.StartNew();

        for (int i = 0; i < iterations; i++)
        {
            Client.Rendering.SubChunkMeshBuilder.Build(world, Vector2Int.zero, 0, meshData);
        }

        sw.Stop();
        Debug.Log($"[BENCHMARK] SubChunkMeshing: {sw.ElapsedMilliseconds} ms for {iterations:N0} builds (Quads: {meshData.Triangles.Count / 6}, Verts: {meshData.Vertices.Count})");
    }
}
