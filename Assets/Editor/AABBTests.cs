using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using VoxelCore;
using VoxelCore.Physics;

public class AABBTests
{
    [Test]
    public void AABB_ConstructorsAndProperties_CalculatesCorrectly()
    {
        var box1 = new AABB(new Vector3(0, 0, 0), new Vector3(1, 2, 3));
        Assert.AreEqual(new Vector3(1, 2, 3), box1.Size);
        Assert.AreEqual(new Vector3(0.5f, 1f, 1.5f), box1.Center);

        var box2 = AABB.FromCenterAndSize(new Vector3(10, 10, 10), new Vector3(2, 4, 6));
        Assert.AreEqual(new Vector3(9, 8, 7), box2.Min);
        Assert.AreEqual(new Vector3(11, 12, 13), box2.Max);

        var block = AABB.FromBlock(5, 10, 15);
        Assert.AreEqual(new Vector3(5, 10, 15), block.Min);
        Assert.AreEqual(new Vector3(6, 11, 16), block.Max);
    }

    [Test]
    public void AABB_Offset_TranslatesBox()
    {
        var box = new AABB(new Vector3(0, 0, 0), new Vector3(1, 1, 1));
        var moved = box.Offset(new Vector3(2, 3, 4));
        Assert.AreEqual(new Vector3(2, 3, 4), moved.Min);
        Assert.AreEqual(new Vector3(3, 4, 5), moved.Max);
    }

    [Test]
    public void AABB_Expand_SweptVolumeCoversMotion()
    {
        var box = new AABB(new Vector3(0, 0, 0), new Vector3(1, 1, 1));

        // Движение вперед и вверх (+X, +Y)
        var expPositive = box.Expand(new Vector3(2, 3, 0));
        Assert.AreEqual(new Vector3(0, 0, 0), expPositive.Min);
        Assert.AreEqual(new Vector3(3, 4, 1), expPositive.Max);

        // Движение назад и вниз (-X, -Y)
        var expNegative = box.Expand(new Vector3(-2, -3, 0));
        Assert.AreEqual(new Vector3(-2, -3, 0), expNegative.Min);
        Assert.AreEqual(new Vector3(1, 1, 1), expNegative.Max);
    }

    [Test]
    public void AABB_Intersects_DetectsOverlap()
    {
        var boxA = new AABB(new Vector3(0, 0, 0), new Vector3(1, 1, 1));
        var boxB = new AABB(new Vector3(0.5f, 0.5f, 0.5f), new Vector3(1.5f, 1.5f, 1.5f));
        var boxC = new AABB(new Vector3(2, 2, 2), new Vector3(3, 3, 3));
        var boxTouching = new AABB(new Vector3(1, 0, 0), new Vector3(2, 1, 1)); // Касается только гранью

        Assert.IsTrue(boxA.Intersects(boxB));
        Assert.IsTrue(boxB.Intersects(boxA));
        Assert.IsFalse(boxA.Intersects(boxC));
        // Касание гранями без проникновения НЕ считается пересечением объемов
        Assert.IsFalse(boxA.Intersects(boxTouching));
    }

    [Test]
    public void AABB_CalculateYOffset_LandingOnFloor()
    {
        // Препятствие: блок пола на Y = 0..1
        var floor = AABB.FromBlock(0, 0, 0);

        // Игрок над блоком: Y = 1.5..3.3 (ноги на Y = 1.5)
        var player = new AABB(new Vector3(0.2f, 1.5f, 0.2f), new Vector3(0.8f, 3.3f, 0.8f));

        // Желаемое падение вниз на 1.0 блок (dy = -1.0)
        float desiredDy = -1.0f;
        float allowedDy = floor.CalculateYOffset(player, desiredDy);

        // Должно остановиться ровно на поверхности блока (1.0 - 1.5 = -0.5)
        Assert.AreEqual(-0.5f, allowedDy, 0.0001f);

        // После перемещения ноги игрока должны оказаться ровно на Y = 1.0
        var movedPlayer = player.Offset(new Vector3(0, allowedDy, 0));
        Assert.AreEqual(1.0f, movedPlayer.Min.y, 0.0001f);
    }

    [Test]
    public void AABB_CalculateYOffset_HittingCeiling()
    {
        // Препятствие: потолок на Y = 4..5
        var ceiling = AABB.FromBlock(0, 4, 0);

        // Игрок: Y = 1..2.8 (голова на 2.8)
        var player = new AABB(new Vector3(0.2f, 1f, 0.2f), new Vector3(0.8f, 2.8f, 0.8f));

        // Прыжок вверх на 2.0 блока
        float desiredDy = 2.0f;
        float allowedDy = ceiling.CalculateYOffset(player, desiredDy);

        // Должно остановиться при касании потолка (4.0 - 2.8 = 1.2)
        Assert.AreEqual(1.2f, allowedDy, 0.0001f);

        var movedPlayer = player.Offset(new Vector3(0, allowedDy, 0));
        Assert.AreEqual(4.0f, movedPlayer.Max.y, 0.0001f);
    }

    [Test]
    public void AABB_CalculateXOffset_WalkingIntoWall()
    {
        // Препятствие: стена на X = 3..4
        var wall = AABB.FromBlock(3, 0, 0);

        // Игрок идет слева направо к стене: X = 1.5..2.1
        var player = new AABB(new Vector3(1.5f, 0.1f, 0.2f), new Vector3(2.1f, 1.9f, 0.8f));

        // Движение вперед на 1.5 блока по X
        float desiredDx = 1.5f;
        float allowedDx = wall.CalculateXOffset(player, desiredDx);

        // До стены: 3.0 - 2.1 = 0.9
        Assert.AreEqual(0.9f, allowedDx, 0.0001f);

        var movedPlayer = player.Offset(new Vector3(allowedDx, 0, 0));
        Assert.AreEqual(3.0f, movedPlayer.Max.x, 0.0001f);
    }

    [Test]
    public void AABB_CalculateOffset_NoOverlapInOtherAxes_AllowsFullMotion()
    {
        // Стена на Z = 5..6, но X = 0..1
        var wall = AABB.FromBlock(0, 0, 5);

        // Игрок на X = 10..10.6 (далеко сбоку)
        var player = new AABB(new Vector3(10f, 0f, 2f), new Vector3(10.6f, 1.8f, 2.6f));

        // Движение вперед по Z на 5 блоков
        float desiredDz = 5.0f;
        float allowedDz = wall.CalculateZOffset(player, desiredDz);

        // Так как по X игрок не перекрывает стену, стена не должна мешать
        Assert.AreEqual(5.0f, allowedDz, 0.0001f);
    }

    [Test]
    public void AABB_Epsilon_WalkingAcrossFloorSeams_DoesNotBlockHorizontalMovement()
    {
        // Препятствие: соседний блок пола на (1, 0, 0), занимающий Y = 0..1
        var nextFloorBlock = AABB.FromBlock(1, 0, 0);

        // Игрок стоит на полу Y = 1.0, но из-за float погрешности ноги на 0.99999f
        var playerWithFloatNoise = new AABB(new Vector3(0.5f, 0.99999f, 0.2f), new Vector3(0.9f, 2.79999f, 0.8f));

        // Игрок шагает вперед по X на 0.5 блока
        float desiredDx = 0.5f;
        float allowedDx = nextFloorBlock.CalculateXOffset(playerWithFloatNoise, desiredDx);

        // Благодаря Epsilon блок пола не должен ошибочно посчитаться вертикальным препятствием
        Assert.AreEqual(0.5f, allowedDx, 0.0001f);
    }

    [Test]
    public void AABB_Epsilon_NearWall_ClampsSpuriousOppositePush()
    {
        // Стена на X = 3..4
        var wall = AABB.FromBlock(3, 0, 0);

        // Игрок вплотную к стене с микропроникновением (X max = 3.00002)
        var playerTouchingWall = new AABB(new Vector3(2.4f, 0.1f, 0.2f), new Vector3(3.00002f, 1.9f, 0.8f));

        // Движение вперед к стене
        float desiredDx = 0.1f;
        float allowedDx = wall.CalculateXOffset(playerTouchingWall, desiredDx);

        // Смещение должно обнулиться, а не стать отрицательным (не толкать назад)
        Assert.AreEqual(0.0f, allowedDx, 0.00001f);
    }

    [Test]
    public void World_GetCollidingBoxes_FindsSolidBlocksOnly()
    {
        var world = new World();
        world.GetOrCreateChunk(Vector2Int.zero);

        // Устанавливаем блоки: пол и одна каменная колонна
        world.SetVoxel(0, 0, 0, new Voxel(BlockType.Stone));
        world.SetVoxel(1, 0, 0, new Voxel(BlockType.Dirt));
        world.SetVoxel(0, 1, 0, Voxel.Air);

        var boxes = new List<AABB>();
        var queryBox = new AABB(new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(1.5f, 1.5f, 1.5f));

        world.GetCollidingBoxes(queryBox, boxes);

        // Должны найтись только (0, 0, 0) и (1, 0, 0)
        Assert.AreEqual(2, boxes.Count);
        Assert.IsTrue(boxes.Contains(AABB.FromBlock(0, 0, 0)));
        Assert.IsTrue(boxes.Contains(AABB.FromBlock(1, 0, 0)));
    }

    [Test]
    public void World_GetCollidingBoxes_IgnoresAdjacentNonPenetratingBlocks_DueToEpsilon()
    {
        var world = new World();
        world.GetOrCreateChunk(Vector2Int.zero);
        world.SetVoxel(0, 0, 0, new Voxel(BlockType.Stone));

        var boxes = new List<AABB>();

        // Область стоит вплотную к блоку (0,0,0) по грани X=1.0, но не проникает внутрь
        // Min.x = 1.0f (или 0.99999f с погрешностью)
        var boxTouching = new AABB(new Vector3(0.99999f, 0.0f, 0.0f), new Vector3(2.0f, 1.0f, 1.0f));

        world.GetCollidingBoxes(boxTouching, boxes);

        // Благодаря Epsilon блок (0,0,0) не должен попасть в выборку коллизий
        Assert.AreEqual(0, boxes.Count);
    }

    [Test]
    public void World_GetCollidingBoxes_HandlesChunkBoundariesAndNegativeCoordinates()
    {
        var world = new World();
        world.GetOrCreateChunk(new Vector2Int(-1, 0));
        world.GetOrCreateChunk(new Vector2Int(0, 0));

        // Блок слева от границы X=0 (в чанке -1)
        world.SetVoxel(-1, 5, 0, new Voxel(BlockType.Stone));
        // Блок справа от границы X=0 (в чанке 0)
        world.SetVoxel(0, 5, 0, new Voxel(BlockType.Dirt));

        var boxes = new List<AABB>();
        // Область, пересекающая границу между -1 и 0
        var crossingBox = new AABB(new Vector3(-1.5f, 4.5f, -0.5f), new Vector3(0.5f, 5.5f, 0.5f));

        world.GetCollidingBoxes(crossingBox, boxes);

        Assert.AreEqual(2, boxes.Count);
        Assert.IsTrue(boxes.Contains(AABB.FromBlock(-1, 5, 0)));
        Assert.IsTrue(boxes.Contains(AABB.FromBlock(0, 5, 0)));
    }

    [Test]
    public void World_GetCollidingBoxes_OutOfBoundsHeight_ReturnsEmpty()
    {
        var world = new World();
        world.GetOrCreateChunk(Vector2Int.zero);

        var boxes = new List<AABB>();

        // Запрос под миром (Y < 0)
        var boxBelow = new AABB(new Vector3(0, -5, 0), new Vector3(1, -1, 1));
        world.GetCollidingBoxes(boxBelow, boxes);
        Assert.AreEqual(0, boxes.Count);

        // Запрос над миром (Y >= 128)
        var boxAbove = new AABB(new Vector3(0, 130, 0), new Vector3(1, 135, 1));
        world.GetCollidingBoxes(boxAbove, boxes);
        Assert.AreEqual(0, boxes.Count);
    }
}
