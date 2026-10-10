using NUnit.Framework;
using UnityEngine;
using VoxelCore;
using VoxelCore.Physics;

public class VoxelRaycastTests
{
    [Test]
    public void Raycast_HitsAllSixFaces_CorrectFaceAndAdjacentPosition()
    {
        var world = new World();
        world.GetOrCreateChunk(Vector2Int.zero);

        // Твердый блок в точке (0, 10, 0): X in [0..1], Y in [10..11], Z in [0..1]
        world.SetVoxel(0, 10, 0, new Voxel(BlockType.Stone));

        // 1. Сверху вниз -> попадание в грань Up (+Y)
        var hitUp = world.Raycast(new Vector3(0.5f, 13f, 0.5f), Vector3.down, 5f);
        Assert.IsTrue(hitUp.HasHit);
        Assert.AreEqual(new Vector3Int(0, 10, 0), hitUp.BlockPos);
        Assert.AreEqual(new Vector3Int(0, 11, 0), hitUp.AdjacentPos);
        Assert.AreEqual(BlockFace.Up, hitUp.Face);
        Assert.AreEqual(2.0f, hitUp.Distance, 0.001f);
        Assert.AreEqual(new Vector3(0.5f, 11f, 0.5f), hitUp.HitPoint);

        // 2. Снизу вверх -> попадание в грань Down (-Y)
        var hitDown = world.Raycast(new Vector3(0.5f, 8f, 0.5f), Vector3.up, 5f);
        Assert.IsTrue(hitDown.HasHit);
        Assert.AreEqual(new Vector3Int(0, 10, 0), hitDown.BlockPos);
        Assert.AreEqual(new Vector3Int(0, 9, 0), hitDown.AdjacentPos);
        Assert.AreEqual(BlockFace.Down, hitDown.Face);
        Assert.AreEqual(2.0f, hitDown.Distance, 0.001f);
        Assert.AreEqual(new Vector3(0.5f, 10f, 0.5f), hitDown.HitPoint);

        // 3. С юга на север (+Z) -> попадание в грань South (-Z)
        var hitSouth = world.Raycast(new Vector3(0.5f, 10.5f, -2f), Vector3.forward, 5f);
        Assert.IsTrue(hitSouth.HasHit);
        Assert.AreEqual(new Vector3Int(0, 10, 0), hitSouth.BlockPos);
        Assert.AreEqual(new Vector3Int(0, 10, -1), hitSouth.AdjacentPos);
        Assert.AreEqual(BlockFace.South, hitSouth.Face);
        Assert.AreEqual(2.0f, hitSouth.Distance, 0.001f);
        Assert.AreEqual(new Vector3(0.5f, 10.5f, 0f), hitSouth.HitPoint);

        // 4. С севера на юг (-Z) -> попадание в грань North (+Z)
        var hitNorth = world.Raycast(new Vector3(0.5f, 10.5f, 3f), Vector3.back, 5f);
        Assert.IsTrue(hitNorth.HasHit);
        Assert.AreEqual(new Vector3Int(0, 10, 0), hitNorth.BlockPos);
        Assert.AreEqual(new Vector3Int(0, 10, 1), hitNorth.AdjacentPos);
        Assert.AreEqual(BlockFace.North, hitNorth.Face);
        Assert.AreEqual(2.0f, hitNorth.Distance, 0.001f);
        Assert.AreEqual(new Vector3(0.5f, 10.5f, 1f), hitNorth.HitPoint);

        // 5. С запада на восток (+X) -> попадание в грань West (-X)
        var hitWest = world.Raycast(new Vector3(-2f, 10.5f, 0.5f), Vector3.right, 5f);
        Assert.IsTrue(hitWest.HasHit);
        Assert.AreEqual(new Vector3Int(0, 10, 0), hitWest.BlockPos);
        Assert.AreEqual(new Vector3Int(-1, 10, 0), hitWest.AdjacentPos);
        Assert.AreEqual(BlockFace.West, hitWest.Face);
        Assert.AreEqual(2.0f, hitWest.Distance, 0.001f);
        Assert.AreEqual(new Vector3(0f, 10.5f, 0.5f), hitWest.HitPoint);

        // 6. С востока на запад (-X) -> попадание в грань East (+X)
        var hitEast = world.Raycast(new Vector3(3f, 10.5f, 0.5f), Vector3.left, 5f);
        Assert.IsTrue(hitEast.HasHit);
        Assert.AreEqual(new Vector3Int(0, 10, 0), hitEast.BlockPos);
        Assert.AreEqual(new Vector3Int(1, 10, 0), hitEast.AdjacentPos);
        Assert.AreEqual(BlockFace.East, hitEast.Face);
        Assert.AreEqual(2.0f, hitEast.Distance, 0.001f);
        Assert.AreEqual(new Vector3(1f, 10.5f, 0.5f), hitEast.HitPoint);
    }

    [Test]
    public void Raycast_ExceedsMaxDistance_ReturnsMiss()
    {
        var world = new World();
        world.GetOrCreateChunk(Vector2Int.zero);
        world.SetVoxel(0, 10, 0, new Voxel(BlockType.Stone));

        // До блока 5.0 метров (с Y=16.0 до Y=11.0), а maxDistance = 4.5f
        var hit = world.Raycast(new Vector3(0.5f, 16f, 0.5f), Vector3.down, 4.5f);
        Assert.IsFalse(hit.HasHit);
        Assert.AreEqual(VoxelHitResult.Miss.HasHit, hit.HasHit);
    }

    [Test]
    public void Raycast_StartsInsideSolidBlock_ReturnsImmediateHit()
    {
        var world = new World();
        world.GetOrCreateChunk(Vector2Int.zero);
        world.SetVoxel(0, 10, 0, new Voxel(BlockType.Stone));

        Vector3 insidePoint = new Vector3(0.5f, 10.5f, 0.5f);
        var hit = world.Raycast(insidePoint, Vector3.forward, 5f);

        Assert.IsTrue(hit.HasHit);
        Assert.AreEqual(0.0f, hit.Distance);
        Assert.AreEqual(new Vector3Int(0, 10, 0), hit.BlockPos);
        Assert.AreEqual(insidePoint, hit.HitPoint);
    }

    [Test]
    public void Raycast_CrossesChunkBoundariesDiagonally_FindsTargetBlock()
    {
        var world = new World();
        // Создаем чанк (0, 0) и соседний чанк (1, 1)
        world.GetOrCreateChunk(Vector2Int.zero);
        world.GetOrCreateChunk(new Vector2Int(1, 1));

        // Блок в начале чанка (1, 1) на координатах X=16, Z=16
        world.SetVoxel(16, 5, 16, new Voxel(BlockType.Stone));

        // Луч стартует в чанке (0, 0) около границы: (15.5, 5.5, 15.5) и летит диагонально (+X, +Z)
        Vector3 origin = new Vector3(15.5f, 5.5f, 15.5f);
        Vector3 dir = new Vector3(1f, 0f, 1f).normalized;

        var hit = world.Raycast(origin, dir, 5f);
        Assert.IsTrue(hit.HasHit);
        Assert.AreEqual(new Vector3Int(16, 5, 16), hit.BlockPos);
    }

    [Test]
    public void Raycast_ZeroLengthOrNegativeDistance_ReturnsMiss()
    {
        var world = new World();
        world.GetOrCreateChunk(Vector2Int.zero);
        world.SetVoxel(0, 10, 0, new Voxel(BlockType.Stone));

        var hitZeroDir = world.Raycast(new Vector3(0.5f, 12f, 0.5f), Vector3.zero, 5f);
        Assert.IsFalse(hitZeroDir.HasHit);

        var hitZeroDist = world.Raycast(new Vector3(0.5f, 12f, 0.5f), Vector3.down, 0f);
        Assert.IsFalse(hitZeroDist.HasHit);
    }
}
