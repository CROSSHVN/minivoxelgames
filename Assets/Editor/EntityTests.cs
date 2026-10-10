using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using VoxelCore;
using VoxelCore.Entities;
using VoxelCore.Physics;

public class EntityTests
{
    private class TestEntity : Entity
    {
        public override EntityType Type => EntityType.Player;

        public float CustomGravity { get; set; } = 0.08f;
        public float CustomDrag { get; set; } = 0.98f;

        public TestEntity(uint id, Vector3 position, float width, float height) : base(id, position, width, height)
        {
        }

        public override void Tick(World world, float dt)
        {
            Velocity = new Vector3(Velocity.x, Velocity.y - CustomGravity, Velocity.z);
            Move(world, Velocity);
            Velocity = new Vector3(Velocity.x * CustomDrag, Velocity.y * CustomDrag, Velocity.z * CustomDrag);
        }
    }

    [Test]
    public void Entity_PositionAndBoundingBox_SyncProperly()
    {
        // Сущность игрока 0.6 x 1.8 в точке (10, 5, 20)
        var entity = new TestEntity(1, new Vector3(10f, 5f, 20f), 0.6f, 1.8f);

        Assert.AreEqual(new Vector3(10f, 5f, 20f), entity.Position);
        Assert.AreEqual(0.6f, entity.Width);
        Assert.AreEqual(1.8f, entity.Height);

        // Min.y = 5.0 (уровень ног), Max.y = 6.8 (голова)
        Assert.AreEqual(5.0f, entity.BoundingBox.Min.y, 0.0001f);
        Assert.AreEqual(6.8f, entity.BoundingBox.Max.y, 0.0001f);
        // X = [9.7 .. 10.3], Z = [19.7 .. 20.3]
        Assert.AreEqual(9.7f, entity.BoundingBox.Min.x, 0.0001f);
        Assert.AreEqual(10.3f, entity.BoundingBox.Max.x, 0.0001f);
        Assert.AreEqual(19.7f, entity.BoundingBox.Min.z, 0.0001f);
        Assert.AreEqual(20.3f, entity.BoundingBox.Max.z, 0.0001f);
    }

    [Test]
    public void Entity_DefaultStepHeight_IsZero()
    {
        var entity = new TestEntity(1, Vector3.zero, 0.6f, 1.8f);
        // По умолчанию у базовой сущности авто-шаг равен нулю (для блоков, предметов и стрел)
        Assert.AreEqual(0.0f, entity.StepHeight);
    }

    [Test]
    public void Entity_Move_FallsAndLandsOnFloor()
    {
        var world = new World();
        world.GetOrCreateChunk(Vector2Int.zero);
        // Пол: блок (0, 0, 0) с Y = 0..1
        world.SetVoxel(0, 0, 0, new Voxel(BlockType.Stone));

        // Игрок в воздухе над полом: ноги на Y = 2.5
        var entity = new TestEntity(1, new Vector3(0.5f, 2.5f, 0.5f), 0.6f, 1.8f);
        entity.Velocity = new Vector3(0, -2.0f, 0);

        Assert.IsFalse(entity.OnGround);

        // Падаем вниз на 2.0 блока
        entity.Move(world, new Vector3(0, -2.0f, 0));

        // Ноги должны остановиться ровно на поверхности блока (Y = 1.0)
        Assert.AreEqual(1.0f, entity.Position.y, 0.0001f);
        Assert.IsTrue(entity.OnGround);
        Assert.IsTrue(entity.IsCollidedVertically);
        Assert.IsFalse(entity.IsCollidedHorizontally);
        // Вертикальная скорость сбросилась в ноль при ударе о пол
        Assert.AreEqual(0.0f, entity.Velocity.y, 0.0001f);
    }

    [Test]
    public void Entity_Move_WallStopsMovementAndZeroesVelocity()
    {
        var world = new World();
        world.GetOrCreateChunk(Vector2Int.zero);
        // Стена на X = 2 (занимает X = 2..3, Y = 0..3, Z = 0..1)
        world.SetVoxel(2, 0, 0, new Voxel(BlockType.Stone));
        world.SetVoxel(2, 1, 0, new Voxel(BlockType.Stone));
        world.SetVoxel(2, 2, 0, new Voxel(BlockType.Stone));

        // Игрок на X = 1.0 (ноги на Y = 0)
        var entity = new TestEntity(1, new Vector3(1.0f, 0f, 0.5f), 0.6f, 1.8f);
        entity.Velocity = new Vector3(2.0f, 0f, 0f);

        // Движение вперед на 2 блока
        entity.Move(world, new Vector3(2.0f, 0f, 0f));

        // Стена начинается на X = 2.0, полуширина игрока = 0.3.
        // Игрок должен остановиться на X = 1.7 (Max.x = 2.0)
        Assert.AreEqual(1.7f, entity.Position.x, 0.0001f);
        Assert.IsTrue(entity.IsCollidedHorizontally);
        Assert.AreEqual(0.0f, entity.Velocity.x, 0.0001f);
    }

    [Test]
    public void Entity_Move_CannotClimbHighWall_WhenStepHeightTooLow()
    {
        var world = new World();
        world.GetOrCreateChunk(Vector2Int.zero);

        // Пол под ногами (Y = 0)
        world.SetVoxel(0, 0, 0, new Voxel(BlockType.Stone));
        world.SetVoxel(1, 0, 0, new Voxel(BlockType.Stone));
        // Препятствие высотой 1 блок прямо перед игроком (Y = 1..2)
        world.SetVoxel(1, 1, 0, new Voxel(BlockType.Stone));

        // Игрок стоит на полу Y = 1.0 в точке X = 0.4
        var entity = new TestEntity(1, new Vector3(0.4f, 1.0f, 0.5f), 0.6f, 1.8f);
        entity.StepHeight = 0.6f; // Высота шага (0.6 < 1.0)
        entity.Move(world, new Vector3(0, -0.05f, 0)); // Фиксируем приземление на пол
        Assert.IsTrue(entity.OnGround);

        // Идем вперед на 0.5 блока
        entity.Move(world, new Vector3(0.5f, 0f, 0f));

        // Игрок не должен зашагнуть на блок высотой 1.0, он упирается в стенку (X = 1.0 - 0.3 = 0.7)
        Assert.AreEqual(0.7f, entity.Position.x, 0.0001f);
        Assert.AreEqual(1.0f, entity.Position.y, 0.0001f);
        Assert.IsTrue(entity.IsCollidedHorizontally);
    }

    [Test]
    public void Entity_Move_StepHeight_ClimbsObstacleWhenStepHeightSufficient()
    {
        var world = new World();
        world.GetOrCreateChunk(Vector2Int.zero);

        // Пол под ногами (Y = 0)
        world.SetVoxel(0, 0, 0, new Voxel(BlockType.Stone));
        // Ступенька на X = 1 (Y = 1..2)
        world.SetVoxel(1, 1, 0, new Voxel(BlockType.Stone));

        // Игрок стоит на полу Y = 1.0 в точке X = 0.5 (Max.x = 0.8)
        var entity = new TestEntity(1, new Vector3(0.5f, 1.0f, 0.5f), 0.6f, 1.8f);
        // Задаем StepHeight = 1.0f (например, способность зашагивать на 1 блок)
        entity.StepHeight = 1.0f;
        entity.Move(world, new Vector3(0, -0.05f, 0)); // Фиксируем приземление на пол
        Assert.IsTrue(entity.OnGround);

        // Пытаемся пойти вперед на 0.8 блока
        entity.Move(world, new Vector3(0.8f, 0f, 0f));

        // Сущность должна подняться на блок и встать на его поверхность (Y = 2.0)
        Assert.AreEqual(2.0f, entity.Position.y, 0.0001f);
        Assert.AreEqual(1.3f, entity.Position.x, 0.0001f);
        Assert.IsTrue(entity.OnGround);
    }

    [Test]
    public void Entity_Move_CeilingBlocksUpwardMotion()
    {
        var world = new World();
        world.GetOrCreateChunk(Vector2Int.zero);
        // Потолок на Y = 3..4
        world.SetVoxel(0, 3, 0, new Voxel(BlockType.Stone));

        // Игрок: ноги на Y = 1.0, рост 1.8 -> голова на 2.8
        var entity = new TestEntity(1, new Vector3(0.5f, 1.0f, 0.5f), 0.6f, 1.8f);
        entity.Velocity = new Vector3(0f, 1.5f, 0f);

        // Прыжок вверх на 1.5 блока
        entity.Move(world, new Vector3(0f, 1.5f, 0f));

        // Голова упирается в Y = 3.0, значит ноги на 3.0 - 1.8 = 1.2
        Assert.AreEqual(1.2f, entity.Position.y, 0.0001f);
        Assert.IsTrue(entity.IsCollidedVertically);
        Assert.AreEqual(0.0f, entity.Velocity.y, 0.0001f);
    }

    [Test]
    public void EntityState_CaptureAndApplyRoundtrip()
    {
        var entity = new TestEntity(42, new Vector3(12.5f, 64.0f, -30.2f), 0.6f, 1.8f);
        entity.Velocity = new Vector3(1.2f, -0.5f, 0.8f);

        // Делаем снимок
        EntityState state = entity.CaptureState();

        Assert.AreEqual(42u, state.EntityId);
        Assert.AreEqual(EntityType.Player, state.Type);
        Assert.AreEqual(new Vector3(12.5f, 64.0f, -30.2f), state.Position);
        Assert.AreEqual(new Vector3(1.2f, -0.5f, 0.8f), state.Velocity);
        Assert.AreEqual(0f, state.Yaw);
        Assert.AreEqual(0f, state.Pitch);

        // Изменяем сущность
        entity.SetPosition(Vector3.zero);
        entity.Velocity = Vector3.zero;

        // Восстанавливаем из снимка
        entity.ApplyState(state);

        Assert.AreEqual(new Vector3(12.5f, 64.0f, -30.2f), entity.Position);
        Assert.AreEqual(new Vector3(1.2f, -0.5f, 0.8f), entity.Velocity);
    }

    [Test]
    public void Entity_Tick_AppliesCustomPhysicsAndMoves()
    {
        var world = new World();
        world.GetOrCreateChunk(Vector2Int.zero);
        world.SetVoxel(0, 0, 0, new Voxel(BlockType.Stone));

        // Игрок на Y = 5.0 в воздухе
        var entity = new TestEntity(1, new Vector3(0.5f, 5.0f, 0.5f), 0.6f, 1.8f);
        entity.Velocity = Vector3.zero;
        entity.CustomGravity = 0.08f;
        entity.CustomDrag = 0.98f;

        // 1 тик (0.05 сек)
        entity.Tick(world, 0.05f);

        // Скорость по Y должна уменьшиться на гравитацию (с учетом drag 0.98)
        // newVel = (0 - 0.08) * 0.98 = -0.0784
        Assert.AreEqual(-0.0784f, entity.Velocity.y, 0.0001f);
        // Позиция сместилась вниз на величину скорости (-0.08)
        Assert.AreEqual(5.0f - 0.08f, entity.Position.y, 0.0001f);
    }
}
