using UnityEngine;

namespace VoxelCore
{
    /// <summary>
    /// 6 направлений граней куба вокселя.
    /// </summary>
    public enum BlockFace : byte
    {
        Up = 0,    // +Y (Верх)
        Down = 1,  // -Y (Низ)
        North = 2, // +Z (Север)
        South = 3, // -Z (Юг)
        East = 4,  // +X (Восток)
        West = 5   // -X (Запад)
    }

    /// <summary>
    /// Вспомогательные методы расширения для BlockFace.
    /// </summary>
    public static class BlockFaceExtensions
    {
        /// <summary>
        /// Возвращает единичное целочисленное смещение для заданной грани.
        /// </summary>
        public static Vector3Int ToVector3Int(this BlockFace face)
        {
            return face switch
            {
                BlockFace.Up => Vector3Int.up,
                BlockFace.Down => Vector3Int.down,
                BlockFace.North => new Vector3Int(0, 0, 1),
                BlockFace.South => new Vector3Int(0, 0, -1),
                BlockFace.East => Vector3Int.right,
                BlockFace.West => Vector3Int.left,
                _ => Vector3Int.zero
            };
        }

        /// <summary>
        /// Возвращает противоположную грань (Up <-> Down, North <-> South, East <-> West).
        /// </summary>
        public static BlockFace Opposite(this BlockFace face)
        {
            return face switch
            {
                BlockFace.Up => BlockFace.Down,
                BlockFace.Down => BlockFace.Up,
                BlockFace.North => BlockFace.South,
                BlockFace.South => BlockFace.North,
                BlockFace.East => BlockFace.West,
                BlockFace.West => BlockFace.East,
                _ => face
            };
        }
    }
}
