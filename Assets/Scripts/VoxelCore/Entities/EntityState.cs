using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace VoxelCore.Entities
{
    /// <summary>
    /// Битовые флаги состояния сущности для сетевой синхронизации и физики.
    /// Занимает ровно 1 байт.
    /// </summary>
    [Flags]
    public enum EntityFlags : byte
    {
        None = 0,
        OnGround = 1 << 0,
        CollidedHorizontally = 1 << 1,
        CollidedVertically = 1 << 2
    }

    /// <summary>
    /// Типы сущностей воксельного мира.
    /// </summary>
    public enum EntityType : byte
    {
        Unknown = 0,
        Player = 1,
        Mob = 2,
        FallingBlock = 3
    }

    /// <summary>
    /// Неизменяемый, компактный (blittable) снапшот состояния сущности.
    /// Предназначен для:
    /// 1. Сетевых пакетов сервер -> клиент без сборки мусора (Zero GC).
    /// 2. Кольцевого буфера предсказания клиента (Client-Side Prediction & Reconciliation).
    /// Размер в памяти: 38 байт.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public readonly struct EntityState : IEquatable<EntityState>
    {
        public readonly uint EntityId;
        public readonly EntityType Type;
        public readonly Vector3 Position;
        public readonly Vector3 Velocity;
        public readonly float Yaw;
        public readonly float Pitch;
        public readonly EntityFlags Flags;

        public EntityState(
            uint entityId,
            EntityType type,
            Vector3 position,
            Vector3 velocity,
            float yaw,
            float pitch,
            EntityFlags flags)
        {
            EntityId = entityId;
            Type = type;
            Position = position;
            Velocity = velocity;
            Yaw = yaw;
            Pitch = pitch;
            Flags = flags;
        }

        public bool HasFlag(EntityFlags flag) => (Flags & flag) != 0;
        public bool OnGround => HasFlag(EntityFlags.OnGround);
        public bool CollidedHorizontally => HasFlag(EntityFlags.CollidedHorizontally);
        public bool CollidedVertically => HasFlag(EntityFlags.CollidedVertically);

        public bool Equals(EntityState other)
        {
            return EntityId == other.EntityId &&
                   Type == other.Type &&
                   Position.Equals(other.Position) &&
                   Velocity.Equals(other.Velocity) &&
                   Mathf.Approximately(Yaw, other.Yaw) &&
                   Mathf.Approximately(Pitch, other.Pitch) &&
                   Flags == other.Flags;
        }

        public override bool Equals(object obj) => obj is EntityState other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)EntityId;
                hash = (hash * 397) ^ (int)Type;
                hash = (hash * 397) ^ Position.GetHashCode();
                hash = (hash * 397) ^ Velocity.GetHashCode();
                hash = (hash * 397) ^ Yaw.GetHashCode();
                hash = (hash * 397) ^ Pitch.GetHashCode();
                hash = (hash * 397) ^ (int)Flags;
                return hash;
            }
        }

        public static bool operator ==(EntityState left, EntityState right) => left.Equals(right);
        public static bool operator !=(EntityState left, EntityState right) => !left.Equals(right);

        public override string ToString()
        {
            return $"EntityState[Id={EntityId}, Type={Type}, Pos={Position:F2}, Vel={Velocity:F2}, Ground={OnGround}]";
        }
    }
}
