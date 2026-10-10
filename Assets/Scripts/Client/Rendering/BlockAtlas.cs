using UnityEngine;
using VoxelCore;

namespace Client.Rendering
{
    /// <summary>
    /// Конфигурация и расчет UV-координат для 16x16 текстурного атласа (minecraft_old_texture_atlas.png).
    /// </summary>
    public static class BlockAtlas
    {
        public const int AtlasSizeInTiles = 16;
        private const float TileSize = 1.0f / AtlasSizeInTiles; // 0.0625
        private const float Padding = 0.001f; // Отступ от краев тайла для исключения артефактов на стыках пикселей

        /// <summary>
        /// Возвращает 4 UV координаты для указанной грани блока:
        /// 0: Bottom-Left, 1: Top-Left, 2: Top-Right, 3: Bottom-Right.
        /// </summary>
        public static void GetFaceUVs(BlockType blockType, BlockFace face, Vector2[] uvsOut, int startIndex)
        {
            Vector2Int tile = GetTileCoord(blockType, face);

            float uMin = tile.x * TileSize + Padding;
            float uMax = (tile.x + 1) * TileSize - Padding;

            // В атласе row = 0 это верхняя строка, а в Unity UV v = 1 это верх
            float vMax = (AtlasSizeInTiles - tile.y) * TileSize - Padding;
            float vMin = (AtlasSizeInTiles - tile.y - 1) * TileSize + Padding;

            uvsOut[startIndex + 0] = new Vector2(uMin, vMin); // Bottom-Left
            uvsOut[startIndex + 1] = new Vector2(uMin, vMax); // Top-Left
            uvsOut[startIndex + 2] = new Vector2(uMax, vMax); // Top-Right
            uvsOut[startIndex + 3] = new Vector2(uMax, vMin); // Bottom-Right
        }

        /// <summary>
        /// Возвращает координаты тайла (колонка 0..15, строка 0..15 сверху) в атласе.
        /// </summary>
        public static Vector2Int GetTileCoord(BlockType type, BlockFace face)
        {
            switch (type)
            {
                case BlockType.Grass:
                    if (face == BlockFace.Up) return new Vector2Int(0, 0); // Каноническая текстура травы сверху
                    if (face == BlockFace.Down) return new Vector2Int(2, 0); // Земля снизу
                    return new Vector2Int(3, 0); // Земля с травой по бокам

                case BlockType.Dirt:
                    return new Vector2Int(2, 0);

                case BlockType.Stone:
                    return new Vector2Int(1, 0);

                case BlockType.Wood:
                    if (face == BlockFace.Up || face == BlockFace.Down) return new Vector2Int(5, 1); // Кольца спила
                    return new Vector2Int(4, 1); // Кора по бокам

                case BlockType.Leaves:
                    return new Vector2Int(5, 4); // Зеленая листва

                case BlockType.Bedrock:
                    return new Vector2Int(1, 1);

                case BlockType.Sand:
                    return new Vector2Int(2, 1);

                case BlockType.Water:
                    return new Vector2Int(13, 12);

                default:
                    return new Vector2Int(1, 0); // По умолчанию камень
            }
        }
    }
}
