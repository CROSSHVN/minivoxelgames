using System;

namespace VoxelCore
{
    /// <summary>
    /// Идентификаторы типов вокселей (блоков).
    /// Используем byte (0..255) для компактности в памяти.
    /// </summary>
    public enum BlockType : byte
    {
        Air = 0,
        Stone = 1,
        Dirt = 2,
        Grass = 3,
        Wood = 4,
        Leaves = 5,
        Bedrock = 6,
        Sand = 7,
        Water = 8
    }

    /// <summary>
    /// Неделимая воксельная единица (блок).
    /// Занимает ровно 1 байт в памяти (размер enum BlockType).
    /// </summary>
    public readonly struct Voxel : IEquatable<Voxel>
    {
        public readonly BlockType Type;

        public Voxel(BlockType type)
        {
            Type = type;
        }

        public bool IsAir => Type == BlockType.Air;
        public bool IsSolid => Type != BlockType.Air;

        public static readonly Voxel Air = new Voxel(BlockType.Air);

        public bool Equals(Voxel other) => Type == other.Type;

        public override bool Equals(object obj) => obj is Voxel other && Equals(other);

        public override int GetHashCode() => (int)Type;

        public override string ToString() => Type.ToString();

        public static bool operator ==(Voxel left, Voxel right) => left.Equals(right);

        public static bool operator !=(Voxel left, Voxel right) => !left.Equals(right);
    }
}
