using NUnit.Framework;
using UnityEngine;
using VoxelCore;

public class VoxelCoreTests
{
    [Test]
    public void Voxel_DefaultAndTypes_BehaveCorrectly()
    {
        var air = Voxel.Air;
        Assert.IsTrue(air.IsAir);
        Assert.IsFalse(air.IsSolid);
        Assert.AreEqual(BlockType.Air, air.Type);

        var stone = new Voxel(BlockType.Stone);
        Assert.IsFalse(stone.IsAir);
        Assert.IsTrue(stone.IsSolid);
        Assert.AreEqual(BlockType.Stone, stone.Type);

        var anotherStone = new Voxel(BlockType.Stone);
        Assert.AreEqual(stone, anotherStone);
        Assert.IsTrue(stone == anotherStone);
    }

    [Test]
    public void SubChunk_InitialState_AllAir()
    {
        var subChunk = new SubChunk();

        Assert.AreEqual(Voxel.Air, subChunk.GetVoxel(0, 0, 0));
        Assert.AreEqual(Voxel.Air, subChunk.GetVoxel(15, 15, 15));
        Assert.AreEqual(Voxel.Air, subChunk.GetVoxel(7, 7, 7));
    }

    [Test]
    public void SubChunk_SetAndGetVoxel_ValidCoordinates()
    {
        var subChunk = new SubChunk();
        var dirt = new Voxel(BlockType.Dirt);

        subChunk.SetVoxel(5, 10, 15, dirt);
        Assert.AreEqual(dirt, subChunk.GetVoxel(5, 10, 15));

        // Соседние воксели не должны быть затронуты
        Assert.AreEqual(Voxel.Air, subChunk.GetVoxel(5, 10, 14));
    }

    [Test]
    public void Chunk_RoutesToCorrectSubChunkByHeight()
    {
        var chunk = new Chunk(Vector2Int.zero);
        var grass = new Voxel(BlockType.Grass);
        var stone = new Voxel(BlockType.Stone);

        // Y = 15 -> подчанк 0 (локальный Y = 15)
        chunk.SetVoxel(0, 15, 0, grass);
        // Y = 16 -> подчанк 1 (локальный Y = 0)
        chunk.SetVoxel(0, 16, 0, stone);
        // Y = 127 -> подчанк 7 (локальный Y = 15)
        chunk.SetVoxel(15, 127, 15, stone);

        Assert.AreEqual(grass, chunk.GetVoxel(0, 15, 0));
        Assert.AreEqual(stone, chunk.GetVoxel(0, 16, 0));
        Assert.AreEqual(stone, chunk.GetVoxel(15, 127, 15));

        // Прямая проверка подчанков
        Assert.AreEqual(grass, chunk.GetSubChunk(0).GetVoxel(0, 15, 0));
        Assert.AreEqual(stone, chunk.GetSubChunk(1).GetVoxel(0, 0, 0));
        Assert.AreEqual(stone, chunk.GetSubChunk(7).GetVoxel(15, 15, 15));
    }

    [Test]
    public void World_HeightBoundsCheck()
    {
        var world = new World();
        world.GetOrCreateChunk(Vector2Int.zero);
        var stone = new Voxel(BlockType.Stone);

        Assert.IsFalse(world.SetVoxel(0, -1, 0, stone));
        Assert.IsFalse(world.SetVoxel(0, 128, 0, stone));

        Assert.AreEqual(Voxel.Air, world.GetVoxel(0, -1, 0));
        Assert.AreEqual(Voxel.Air, world.GetVoxel(0, 128, 0));
    }

    [Test]
    public void World_CoordinateMath_PositiveAndNegative()
    {
        // Положительные координаты
        Assert.AreEqual(new Vector2Int(0, 0), World.WorldToChunkCoord(0, 0));
        Assert.AreEqual(new Vector2Int(0, 0), World.WorldToChunkCoord(15, 15));
        Assert.AreEqual(new Vector2Int(1, 0), World.WorldToChunkCoord(16, 0));
        Assert.AreEqual(0, World.WorldToLocalCoord(0));
        Assert.AreEqual(15, World.WorldToLocalCoord(15));
        Assert.AreEqual(0, World.WorldToLocalCoord(16));

        // Отрицательные координаты
        Assert.AreEqual(new Vector2Int(-1, 0), World.WorldToChunkCoord(-1, 0));
        Assert.AreEqual(15, World.WorldToLocalCoord(-1));

        Assert.AreEqual(new Vector2Int(-1, -1), World.WorldToChunkCoord(-16, -16));
        Assert.AreEqual(0, World.WorldToLocalCoord(-16));

        Assert.AreEqual(new Vector2Int(-2, 0), World.WorldToChunkCoord(-17, 0));
        Assert.AreEqual(15, World.WorldToLocalCoord(-17));
    }

    [Test]
    public void World_StrictMode_DoesNotCreateChunkAutomatically()
    {
        var world = new World();
        var stone = new Voxel(BlockType.Stone);

        // Попытка записать в несуществующий чанк
        bool success = world.SetVoxel(10, 50, 10, stone);
        Assert.IsFalse(success);
        Assert.AreEqual(0, world.LoadedChunksCount);

        // Чтение из несуществующего чанка возвращает Air
        Assert.AreEqual(Voxel.Air, world.GetVoxel(10, 50, 10));
    }

    [Test]
    public void World_CreateChunk_AndAccessAcrossNegativeCoordinates()
    {
        var world = new World();
        var dirt = new Voxel(BlockType.Dirt);

        // Получаем или создаем чанк (-1, 0)
        var chunkNegative = world.GetOrCreateChunk(new Vector2Int(-1, 0));
        Assert.AreEqual(1, world.LoadedChunksCount);

        // Мировые координаты (-1, 30, 5) попадают в чанк (-1, 0), локальные X = 15, Z = 5
        bool success = world.SetVoxel(-1, 30, 5, dirt);
        Assert.IsTrue(success);

        // Проверяем через интерфейс мира
        Assert.AreEqual(dirt, world.GetVoxel(-1, 30, 5));

        // Проверяем напрямую через чанк
        Assert.AreEqual(dirt, chunkNegative.GetVoxel(15, 30, 5));
    }

    [Test]
    public void World_GetOrCreateChunk_ReturnsSameInstanceOnSubsequentCalls()
    {
        var world = new World();
        var chunk1 = world.GetOrCreateChunk(Vector2Int.zero);
        var chunk2 = world.GetOrCreateChunk(Vector2Int.zero);

        Assert.AreSame(chunk1, chunk2);
        Assert.AreEqual(1, world.LoadedChunksCount);
    }
}
