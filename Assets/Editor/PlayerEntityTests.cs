using NUnit.Framework;
using UnityEngine;
using VoxelCore;
using VoxelCore.Entities;
using VoxelCore.Physics;

public class PlayerEntityTests
{
    private class DummyLivingEntity : LivingEntity
    {
        public override EntityType Type => EntityType.Mob;

        public DummyLivingEntity(uint id, Vector3 position) : base(id, position, 0.6f, 1.8f)
        {
        }
    }

    [Test]
    public void LivingEntity_MoveRelative_DirectionMatchesYaw()
    {
        var living = new DummyLivingEntity(1, Vector3.zero);

        // 1. При Yaw = 0 (взгляд вдоль +Z): движение вперед (Y=1) должно увеличивать Z
        living.Yaw = 0f;
        living.Velocity = Vector3.zero;
        living.MoveRelative(new Vector2(0f, 1f), 0.1f);

        Assert.AreEqual(0.0f, living.Velocity.x, 0.0001f);
        Assert.AreEqual(0.1f, living.Velocity.z, 0.0001f);

        // 2. При Yaw = 90 (взгляд на восток вдоль +X): движение вперед (Y=1) должно увеличивать X
        living.Yaw = 90f;
        living.Velocity = Vector3.zero;
        living.MoveRelative(new Vector2(0f, 1f), 0.1f);

        Assert.AreEqual(0.1f, living.Velocity.x, 0.0001f);
        Assert.AreEqual(0.0f, living.Velocity.z, 0.0001f);

        // 3. При Yaw = 0: стрейф вправо (X=1) должен увеличивать X
        living.Yaw = 0f;
        living.Velocity = Vector3.zero;
        living.MoveRelative(new Vector2(1f, 0f), 0.1f);

        Assert.AreEqual(0.1f, living.Velocity.x, 0.0001f);
        Assert.AreEqual(0.0f, living.Velocity.z, 0.0001f);
    }

    [Test]
    public void LivingEntity_Jump_OnlyWorksOnGround()
    {
        var world = new World();
        world.GetOrCreateChunk(Vector2Int.zero);
        world.SetVoxel(0, 0, 0, new Voxel(BlockType.Stone));

        var living = new DummyLivingEntity(1, new Vector3(0.5f, 1.0f, 0.5f));

        // В воздухе прыжок не срабатывает
        Assert.IsFalse(living.OnGround);
        living.Jump();
        Assert.AreEqual(0.0f, living.Velocity.y, 0.0001f);

        // Приземляем на пол
        living.Move(world, new Vector3(0f, -0.05f, 0f));
        Assert.IsTrue(living.OnGround);

        // Прыгаем с земли
        living.Jump();
        Assert.AreEqual(0.42f, living.Velocity.y, 0.0001f);
    }

    [Test]
    public void PlayerEntity_Sneaking_ChangesBoundingBoxHeight()
    {
        var player = new PlayerEntity(1, new Vector3(0f, 10f, 0f));

        // Стоя: высота 1.8
        Assert.IsFalse(player.IsSneaking);
        Assert.AreEqual(1.8f, player.Height);
        Assert.AreEqual(10f, player.BoundingBox.Min.y, 0.0001f);
        Assert.AreEqual(11.8f, player.BoundingBox.Max.y, 0.0001f);

        // Присед: высота 1.5
        player.IsSneaking = true;
        Assert.AreEqual(1.5f, player.Height);
        Assert.AreEqual(10f, player.BoundingBox.Min.y, 0.0001f);
        Assert.AreEqual(11.5f, player.BoundingBox.Max.y, 0.0001f);
    }

    [Test]
    public void PlayerEntity_Sneak_PreventsFallingOffCliffEdge()
    {
        var world = new World();
        world.GetOrCreateChunk(Vector2Int.zero);

        // Одинокий блок (0, 0, 0), вокруг пропасть (воздух)
        world.SetVoxel(0, 0, 0, new Voxel(BlockType.Stone));

        // Игрок стоит в центре блока
        var player = new PlayerEntity(1, new Vector3(0.5f, 1.0f, 0.5f));
        player.Move(world, new Vector3(0f, -0.05f, 0f));
        Assert.IsTrue(player.OnGround);

        // Включаем приседание
        player.IsSneaking = true;

        // Пытаемся пойти вправо (по X) на 2 блока в пропасть
        player.Move(world, new Vector3(2.0f, 0f, 0f));

        // Игрок не должен упасть! Ноги остаются на высоте Y = 1.0, а край AABB удерживается на блоке
        Assert.AreEqual(1.0f, player.Position.y, 0.0001f);
        Assert.IsTrue(player.OnGround);
        // Задняя грань ящика (Min.x) удерживается на блоке [0..1], не давая соскользнуть в пустоту
        Assert.Less(player.BoundingBox.Min.x, 1.0f);
        Assert.Greater(player.BoundingBox.Min.x, 0.5f);
    }

    [Test]
    public void PlayerEntity_WalkingWithoutSneak_FallsOffCliffEdge()
    {
        var world = new World();
        world.GetOrCreateChunk(Vector2Int.zero);

        // Одинокий блок (0, 0, 0), вокруг пропасть
        world.SetVoxel(0, 0, 0, new Voxel(BlockType.Stone));

        var player = new PlayerEntity(1, new Vector3(0.5f, 1.0f, 0.5f));
        player.Move(world, new Vector3(0f, -0.05f, 0f));
        Assert.IsTrue(player.OnGround);

        // БЕЗ приседания идем в сторону обрыва
        player.IsSneaking = false;
        player.Move(world, new Vector3(2.0f, 0f, 0f));

        // Игрок сошел с блока
        Assert.GreaterOrEqual(player.Position.x, 1.0f);
    }

    [Test]
    public void PlayerEntity_ApplyInput_ProcessesMovementAndJump()
    {
        var world = new World();
        world.GetOrCreateChunk(Vector2Int.zero);
        world.SetVoxel(0, 0, 0, new Voxel(BlockType.Stone));

        var player = new PlayerEntity(1, new Vector3(0.5f, 1.0f, 0.5f));
        player.Move(world, new Vector3(0f, -0.05f, 0f)); // Приземление
        Assert.IsTrue(player.OnGround);

        var input = new PlayerInput(
            movement: new Vector2(0f, 1f),
            jump: true,
            sneak: true,
            sprint: false,
            yaw: 45f,
            pitch: -15f
        );

        player.ApplyInput(input);

        Assert.AreEqual(45f, player.Yaw);
        Assert.AreEqual(-15f, player.Pitch);
        Assert.IsTrue(player.IsSneaking);
        Assert.IsFalse(player.IsSprinting);
        // Прыжок сработал
        Assert.AreEqual(0.42f, player.Velocity.y, 0.0001f);
    }
}
