using NUnit.Framework;
using UnityEngine;
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
}
