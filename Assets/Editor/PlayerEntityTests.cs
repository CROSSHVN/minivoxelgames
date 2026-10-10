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
    public void PlayerEntity_ReleasingSneakAtEdge_DoesNotFall()
    {
        var world = new World();
        world.GetOrCreateChunk(Vector2Int.zero);
        world.SetVoxel(0, 0, 0, new Voxel(BlockType.Stone));

        var player = new PlayerEntity(1, new Vector3(0.5f, 1.0f, 0.5f));
        player.Move(world, new Vector3(0f, -0.05f, 0f));
        Assert.IsTrue(player.OnGround);

        // Игрок идет вперед по оси +Z (Yaw = 0) с зажатым Sneak
        for (int i = 0; i < 30; i++)
        {
            var input = new PlayerInput(new Vector2(0f, 1f), false, true, false, 0f, 0f);
            player.ApplyInput(input);
            player.Tick(world, 0.05f);
        }

        Assert.AreEqual(1.0f, player.Position.y, 0.01f);
        Assert.IsTrue(player.OnGround);

        // Игрок остановился (отпустил W), но еще держит Shift
        for (int i = 0; i < 10; i++)
        {
            var input = new PlayerInput(Vector2.zero, false, true, false, 0f, 0f);
            player.ApplyInput(input);
            player.Tick(world, 0.05f);
        }

        Assert.AreEqual(1.0f, player.Position.y, 0.01f);
        Assert.IsTrue(player.OnGround);

        // Игрок ОТПУСКАЕТ присед
        for (int i = 0; i < 20; i++)
        {
            var input = new PlayerInput(Vector2.zero, false, false, false, 0f, 0f);
            player.ApplyInput(input);
            player.Tick(world, 0.05f);
        }

        Assert.AreEqual(1.0f, player.Position.y, 0.01f, "Player must stay on block after releasing sneak");
        Assert.IsTrue(player.OnGround, "Player must remain on ground after releasing sneak");
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

    [Test]
    public void PlayerEntity_SneakOverridesSprint_SpeedAndFlags()
    {
        var world = new World();
        world.GetOrCreateChunk(Vector2Int.zero);
        world.SetVoxel(0, 0, 0, new Voxel(BlockType.Stone));

        var player = new PlayerEntity(1, new Vector3(0.5f, 1.0f, 0.5f));
        player.Move(world, new Vector3(0f, -0.05f, 0f)); // Приземление на землю Y = 1.0
        Assert.IsTrue(player.OnGround);

        // Когда зажаты одновременно и Sneak, и Sprint:
        var input = new PlayerInput(
            movement: new Vector2(0f, 1f),
            jump: false,
            sneak: true,
            sprint: true,
            yaw: 0f,
            pitch: 0f
        );

        player.ApplyInput(input);

        // Приседание блокирует спринт
        Assert.IsTrue(player.IsSneaking);
        Assert.IsFalse(player.IsSprinting);

        player.Velocity = Vector3.zero;
        player.Tick(world, 0.05f);

        // При спринте скорость была бы 0.1 * 1.3 * 0.546 = ~0.071
        // При приседе скорость 0.1 * 0.3 * 0.546 = ~0.0164
        float expectedSpeed = player.BaseMovementSpeed * player.SneakMultiplier * (0.6f * 0.91f);
        Assert.AreEqual(expectedSpeed, player.Velocity.z, 0.001f);
    }

    [Test]
    public void PlayerEntity_JumpInPlaceWithSprint_DoesNotMoveHorizontally()
    {
        var world = new World();
        world.GetOrCreateChunk(Vector2Int.zero);
        world.SetVoxel(0, 0, 0, new Voxel(BlockType.Stone));

        var player = new PlayerEntity(1, new Vector3(0.5f, 1.0f, 0.5f));
        player.Move(world, new Vector3(0f, -0.05f, 0f));
        Assert.IsTrue(player.OnGround);

        // Стоим на месте (Movement = Vector2.zero), но зажат спринт (Sprint = true) и прыжок (Jump = true)
        var input = new PlayerInput(
            movement: Vector2.zero,
            jump: true,
            sneak: false,
            sprint: true,
            yaw: 45f,
            pitch: 0f
        );

        player.ApplyInput(input);

        // Спринт не должен активироваться без движения вперед
        Assert.IsFalse(player.IsSprinting);
        // Вертикальная скорость прыжка должна быть 0.42
        Assert.AreEqual(0.42f, player.Velocity.y, 0.0001f);
        // Горизонтальная скорость должна остаться строго нулевой (никакого толчка вперед на месте)
        Assert.AreEqual(0.0f, player.Velocity.x, 0.0001f);
        Assert.AreEqual(0.0f, player.Velocity.z, 0.0001f);
    }

    [Test]
    public void LivingEntity_JumpApex_ReachesOneAndQuarterBlocks()
    {
        var world = new World();
        world.GetOrCreateChunk(Vector2Int.zero);
        for (int x = -1; x <= 1; x++)
        {
            for (int z = -1; z <= 1; z++)
            {
                world.SetVoxel(x, 0, z, new Voxel(BlockType.Stone));
            }
        }

        var player = new PlayerEntity(1, new Vector3(0.5f, 1.0f, 0.5f));
        player.Move(world, new Vector3(0f, -0.05f, 0f));
        Assert.IsTrue(player.OnGround);

        player.Jump();

        float maxElevation = 0f;
        for (int tick = 0; tick < 20; tick++)
        {
            player.Tick(world, 0.05f);
            float elevation = player.Position.y - 1.0f;
            if (elevation > maxElevation)
            {
                maxElevation = elevation;
            }
        }

        // В Minecraft Java apex прыжка составляет ровно ~1.2522 блока
        Assert.Greater(maxElevation, 1.20f, "Jump apex must exceed 1.2 blocks to clear a 1-block obstacle");
        Assert.Less(maxElevation, 1.30f, "Jump apex should not excessively exceed Minecraft Java physics (~1.25)");
    }

    [Test]
    public void PlayerEntity_JumpOntoOneBlockObstacle_Succeeds()
    {
        var world = new World();
        world.GetOrCreateChunk(Vector2Int.zero);
        // Пол Y = 0 (поверхность Y = 1.0) на X = 0..4
        for (int x = 0; x <= 4; x++)
        {
            for (int z = 0; z <= 2; z++)
            {
                world.SetVoxel(x, 0, z, new Voxel(BlockType.Stone));
            }
        }

        // Препятствие в 1 блок на X = 2..4 (поверхность Y = 2.0)
        for (int x = 2; x <= 4; x++)
        {
            for (int z = 0; z <= 2; z++)
            {
                world.SetVoxel(x, 1, z, new Voxel(BlockType.Stone));
            }
        }

        // Игрок стартует перед препятствием на X = 1.4 (Max.x = 1.7), Y = 1.0, Z = 1.0, смотрит на восток (+X, Yaw = 90)
        var player = new PlayerEntity(1, new Vector3(1.4f, 1.0f, 1.0f));
        player.Move(world, new Vector3(0f, -0.05f, 0f));
        Assert.IsTrue(player.OnGround);

        // Игрок совершает прыжок вперед на препятствие
        bool landedOnObstacle = false;
        for (int tick = 0; tick < 30; tick++)
        {
            // Прыжок на первом тике, затем игрок продолжает бежать вперед
            var input = new PlayerInput(
                movement: new Vector2(0f, 1f),
                jump: tick == 0,
                sneak: false,
                sprint: true,
                yaw: 90f,
                pitch: 0f
            );
            player.ApplyInput(input);
            player.Tick(world, 0.05f);

            if (player.Position.y >= 1.99f && player.Position.x >= 1.8f && player.OnGround)
            {
                landedOnObstacle = true;
                break;
            }
        }

        // Игрок должен успешно запрыгнуть на блок X = 2 (ноги на Y = 2.0)
        Assert.IsTrue(landedOnObstacle, "Player must land on top of the 1-block obstacle");
        Assert.AreEqual(2.0f, player.Position.y, 0.001f, "Player must be firmly on the block surface (Y = 2.0)");
        Assert.Greater(player.Position.x, 1.8f, "Player must have moved forward onto the block");
        Assert.IsTrue(player.OnGround, "Player must be on ground on top of the obstacle");
    }

    [Test]
    public void PlayerEntity_MovingIntoWall_JumpsContinuously()
    {
        var world = new World();
        world.GetOrCreateChunk(Vector2Int.zero);
        // Пол Y = 0 (поверхность Y = 1.0)
        for (int x = 0; x <= 3; x++)
        {
            for (int z = 0; z <= 2; z++)
            {
                world.SetVoxel(x, 0, z, new Voxel(BlockType.Stone));
            }
        }

        // Высокая стена в 3 блока на X = 2 (Y = 1..3)
        for (int y = 1; y <= 3; y++)
        {
            for (int z = 0; z <= 2; z++)
            {
                world.SetVoxel(2, y, z, new Voxel(BlockType.Stone));
            }
        }

        // Игрок прижат к стене: X = 1.7 (грань Max.x = 2.0), Y = 1.0, Z = 1.0, смотрит в стену (+X, Yaw = 90)
        var player = new PlayerEntity(1, new Vector3(1.7f, 1.0f, 1.0f));
        player.Move(world, new Vector3(0f, -0.05f, 0f));
        Assert.IsTrue(player.OnGround);

        var input = new PlayerInput(
            movement: new Vector2(0f, 1f), // Постоянно жмет W в стену
            jump: true,                   // Постоянно зажат Space
            sneak: false,
            sprint: false,
            yaw: 90f,
            pitch: 0f
        );

        int jumpsCount = 0;
        bool wasAirborne = false;

        // Симулируем 40 тиков (2 секунды)
        for (int tick = 0; tick < 40; tick++)
        {
            player.ApplyInput(input);
            player.Tick(world, 0.05f);

            if (!player.OnGround && !wasAirborne)
            {
                jumpsCount++;
                wasAirborne = true;
            }
            else if (player.OnGround)
            {
                wasAirborne = false;
            }
        }

        // За 2 секунды (40 тиков) при цикле прыжка ~11-12 тиков должно совершиться не менее 3 непрерывных прыжков
        Assert.GreaterOrEqual(jumpsCount, 3, "Player must jump continuously when holding Space against a wall without dropping jumps");
    }

    [Test]
    public void PlayerEntity_SneakingUnderLowCeiling_CannotStandUpUntilClear()
    {
        var world = new World();
        world.GetOrCreateChunk(Vector2Int.zero);

        // Потолок на Y = 2 (поверхность Y = 2.0..3.0) только в зоне X = 0..2
        for (int x = 0; x <= 2; x++)
        {
            for (int z = 0; z <= 2; z++)
            {
                world.SetVoxel(x, 2, z, new Voxel(BlockType.Stone));
            }
        }

        // Игрок на высоте Y = 0.5:
        // В приседе (1.5) верхняя грань Max.y = 2.0 (помещается под потолок)
        // Стоя (1.8) верхняя грань Max.y = 2.3 (врезается в потолок)
        var player = new PlayerEntity(1, new Vector3(1.0f, 0.5f, 1.0f));
        player.IsSneaking = true;
        Assert.AreEqual(1.5f, player.Height);

        // Под потолком встать невозможно
        Assert.IsFalse(player.CanStandUp(world));

        // Игрок отпускает клавишу Shift (sneak = false)
        var inputReleaseShift = new PlayerInput(
            movement: Vector2.zero,
            jump: false,
            sneak: false,
            sprint: false,
            yaw: 0f,
            pitch: 0f
        );

        player.ApplyInput(inputReleaseShift, world);
        Assert.IsTrue(player.IsSneaking, "Player must remain sneaking under low ceiling even when releasing Shift");
        Assert.AreEqual(1.5f, player.Height);

        player.Tick(world, 0.05f);
        Assert.IsTrue(player.IsSneaking, "Player must remain sneaking after tick under ceiling");

        // Перемещаем игрока на открытое пространство без потолка (X = 5.0)
        player.SetPosition(new Vector3(5.0f, 0.5f, 1.0f));
        Assert.IsTrue(player.CanStandUp(world), "Player should be able to stand up in open space");

        // Следующий тик должен автоматически выпрямить игрока в полный рост (1.8)
        player.Tick(world, 0.05f);
        Assert.IsFalse(player.IsSneaking, "Player must automatically stand up after leaving low ceiling");
        Assert.AreEqual(1.8f, player.Height);
    }

    [Test]
    public void PlayerEntity_SprintingIntoWall_CancelsSprint()
    {
        var world = new World();
        world.GetOrCreateChunk(Vector2Int.zero);
        for (int x = 0; x <= 3; x++)
        {
            world.SetVoxel(x, 0, 0, new Voxel(BlockType.Stone));
        }
        // Стена на X = 2 (Y = 1..2)
        world.SetVoxel(2, 1, 0, new Voxel(BlockType.Stone));
        world.SetVoxel(2, 2, 0, new Voxel(BlockType.Stone));

        // Игрок перед стеной на X = 1.65 (Max.x = 1.95), смотрит в стену (+X, Yaw = 90)
        var player = new PlayerEntity(1, new Vector3(1.65f, 1.0f, 0.5f));
        player.Move(world, new Vector3(0f, -0.05f, 0f));
        Assert.IsTrue(player.OnGround);

        var sprintInput = new PlayerInput(
            movement: new Vector2(0f, 1f), // вперед
            jump: false,
            sneak: false,
            sprint: true,                  // бег
            yaw: 90f,
            pitch: 0f
        );

        player.ApplyInput(sprintInput, world);
        Assert.IsTrue(player.IsSprinting);

        // Тикаем несколько раз — игрок добегает и упирается в стену X = 2
        for (int i = 0; i < 5; i++)
        {
            player.Tick(world, 0.05f);
        }

        Assert.IsTrue(player.IsCollidedHorizontally, "Player must collide horizontally with wall");
        Assert.IsFalse(player.IsSprinting, "Sprinting must be cancelled when colliding horizontally with a wall");
    }

    [Test]
    public void PlayerEntity_PushOutOfBlocks_NudgesPlayerIntoOpenSpace()
    {
        var world = new World();
        world.GetOrCreateChunk(Vector2Int.zero);

        // Стена на X = 0 вдоль оси Z (Z = -1..1), справа (+X) открытое пространство (воздух)
        for (int z = -1; z <= 1; z++)
        {
            world.SetVoxel(0, 1, z, new Voxel(BlockType.Stone));
        }

        // Размещаем игрока так, чтобы его левые углы основания (X - offset) попали внутрь стены:
        // Если x = 0.9, Width = 0.6, offset = 0.21 => x - offset = 0.69 (внутри стены X = 0)
        var player = new PlayerEntity(1, new Vector3(0.9f, 1.0f, 0.5f));
        player.Velocity = Vector3.zero;

        player.PushOutOfBlocks(world);

        // Ближайшая свободная сторона — Восток (+X), скорость должна стать +0.1f
        Assert.Greater(player.Velocity.x, 0.0f, "Player must receive positive velocity along X pushing out of the block");
    }

    [Test]
    public void LivingEntity_JumpTicks_PreventsRapidJumpSpamming()
    {
        var world = new World();
        world.GetOrCreateChunk(Vector2Int.zero);
        world.SetVoxel(0, 0, 0, new Voxel(BlockType.Stone));

        var player = new PlayerEntity(1, new Vector3(0.5f, 1.0f, 0.5f));
        player.Move(world, new Vector3(0f, -0.05f, 0f));
        Assert.IsTrue(player.OnGround);

        // Первый прыжок
        player.Jump();
        Assert.AreEqual(0.42f, player.Velocity.y, 0.0001f);
        Assert.AreEqual(10, player.JumpTicks);

        // Пытаемся вызвать Jump повторно на том же или следующем тике
        player.Velocity = Vector3.zero;
        player.Jump();
        // Прыжок не должен сработать, так как JumpTicks > 0
        Assert.AreEqual(0.0f, player.Velocity.y, 0.0001f);

        // Пропускаем 10 тиков
        for (int i = 0; i < 10; i++)
        {
            player.Tick(world, 0.05f);
        }

        Assert.AreEqual(0, player.JumpTicks);
    }
}
