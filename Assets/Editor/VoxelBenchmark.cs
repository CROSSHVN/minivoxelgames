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
        Debug.Log($"[BENCHMARK] SubChunkMeshing (Neighborhood Cache): {sw.ElapsedMilliseconds} ms for {iterations:N0} builds (Quads: {meshData.Triangles.Count / 6}, Verts: {meshData.Vertices.Count})");
    }

    [Test]
    public void Benchmark_NeighborhoodCache_DirectComparison()
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

        var meshDataLegacy = new Client.Rendering.MeshData();
        var meshDataCached = new Client.Rendering.MeshData();

        // Прогрев JIT для обоих алгоритмов
        for (int i = 0; i < 50; i++)
        {
            Client.Rendering.SubChunkMeshBuilder.BuildLegacyDirect(world, Vector2Int.zero, 0, meshDataLegacy);
            Client.Rendering.SubChunkMeshBuilder.Build(world, Vector2Int.zero, 0, meshDataCached);
        }

        // Проверка корректности: результаты должны быть абсолютно идентичны
        Assert.AreEqual(meshDataLegacy.Vertices.Count, meshDataCached.Vertices.Count, "Vertex count mismatch!");
        Assert.AreEqual(meshDataLegacy.Triangles.Count, meshDataCached.Triangles.Count, "Triangle count mismatch!");

        const int iterations = 1000;

        // 1. Тест Legacy (World Dictionary Lookup на границах)
        var swLegacy = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            Client.Rendering.SubChunkMeshBuilder.BuildLegacyDirect(world, Vector2Int.zero, 0, meshDataLegacy);
        }
        swLegacy.Stop();
        long legacyMs = swLegacy.ElapsedMilliseconds;

        // 2. Тест Neighborhood Cache (SubChunkNeighbors)
        var swCached = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            Client.Rendering.SubChunkMeshBuilder.Build(world, Vector2Int.zero, 0, meshDataCached);
        }
        swCached.Stop();
        long cachedMs = swCached.ElapsedMilliseconds;

        double speedup = (double)legacyMs / (cachedMs > 0 ? cachedMs : 1);
        double percent = legacyMs > 0 ? ((legacyMs - cachedMs) / (double)legacyMs) * 100.0 : 0.0;

        Debug.Log($"[BENCHMARK] === COMPARISON FOR {iterations:N0} SUBCHUNK BUILDS ===");
        Debug.Log($"[BENCHMARK] Legacy Direct Lookup:    {legacyMs} ms ({legacyMs / (double)iterations:F3} ms/build)");
        Debug.Log($"[BENCHMARK] Neighborhood Cache:       {cachedMs} ms ({cachedMs / (double)iterations:F3} ms/build)");
        Debug.Log($"[BENCHMARK] Result: {speedup:F2}x faster ({percent:F1}% time saved!)");

        Assert.LessOrEqual(cachedMs, legacyMs + 15, "Neighborhood cache should be faster than legacy direct lookup");
    }

    [Test]
    public void Benchmark_FullChunkRebuild_Comparison()
    {
        var world = new World();
        // 3x3 чанка
        for (int cx = -1; cx <= 1; cx++)
        {
            for (int cz = -1; cz <= 1; cz++)
            {
                var chunk = world.GetOrCreateChunk(new Vector2Int(cx, cz));
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

        var meshData = new Client.Rendering.MeshData();
        const int chunkIterations = 100; // 100 чанков * 8 подчанков = 800 подчанков

        // 1. Legacy подход (каждый подчанк опрашивает мир независимо)
        var swLegacy = Stopwatch.StartNew();
        for (int iter = 0; iter < chunkIterations; iter++)
        {
            for (int s = 0; s < Chunk.SubChunksCount; s++)
            {
                Client.Rendering.SubChunkMeshBuilder.BuildLegacyDirect(world, Vector2Int.zero, s, meshData);
            }
        }
        swLegacy.Stop();
        long legacyMs = swLegacy.ElapsedMilliseconds;

        // 2. Оптимизированный подход с ChunkNeighbors (горизонтальные соседи кэшируются 1 раз на всю колонну)
        var swChunkCache = Stopwatch.StartNew();
        for (int iter = 0; iter < chunkIterations; iter++)
        {
            var chunkNeighbors = new ChunkNeighbors(world, Vector2Int.zero);
            for (int s = 0; s < Chunk.SubChunksCount; s++)
            {
                var subNeighbors = chunkNeighbors.GetSubChunkNeighbors(s);
                Client.Rendering.SubChunkMeshBuilder.Build(in subNeighbors, meshData);
            }
        }
        swChunkCache.Stop();
        long chunkCacheMs = swChunkCache.ElapsedMilliseconds;

        double speedup = (double)legacyMs / (chunkCacheMs > 0 ? chunkCacheMs : 1);
        double percent = legacyMs > 0 ? ((legacyMs - chunkCacheMs) / (double)legacyMs) * 100.0 : 0.0;

        Debug.Log($"[BENCHMARK] === FULL COLUMN REBUILD ({chunkIterations * Chunk.SubChunksCount:N0} SUBCHUNKS) ===");
        Debug.Log($"[BENCHMARK] Legacy Column Rebuild:        {legacyMs} ms");
        Debug.Log($"[BENCHMARK] ChunkNeighbors Column Cache:  {chunkCacheMs} ms");
        Debug.Log($"[BENCHMARK] Result: {speedup:F2}x faster ({percent:F1}% time saved!)");
    }
}
