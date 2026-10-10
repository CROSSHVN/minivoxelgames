using Client.Rendering;
using NUnit.Framework;
using UnityEngine;
using VoxelCore;

public class SubChunkMeshBuilderTests
{
    [Test]
    public void MeshBuilder_SingleIsolatedBlock_Generates6Faces()
    {
        var world = new World();
        var chunk = world.GetOrCreateChunk(Vector2Int.zero);
        chunk.SetVoxel(5, 5, 5, new Voxel(BlockType.Stone));

        var meshData = new MeshData();
        SubChunkMeshBuilder.Build(world, Vector2Int.zero, 0, meshData);

        // 6 граней * 4 вершины = 24 вершины
        Assert.AreEqual(24, meshData.Vertices.Count);
        // 6 граней * 6 индексов = 36 треугольников/индексов
        Assert.AreEqual(36, meshData.Triangles.Count);
    }

    [Test]
    public void MeshBuilder_TwoAdjacentBlocks_CullsInternalFaces()
    {
        var world = new World();
        var chunk = world.GetOrCreateChunk(Vector2Int.zero);

        // Два блока соприкасаются по оси Z
        chunk.SetVoxel(5, 5, 5, new Voxel(BlockType.Stone));
        chunk.SetVoxel(5, 5, 6, new Voxel(BlockType.Stone));

        var meshData = new MeshData();
        SubChunkMeshBuilder.Build(world, Vector2Int.zero, 0, meshData);

        // 2 блока без куллинга = 12 граней (48 вершин).
        // С куллингом 2 смежные внутренние грани отсекаются = 10 граней (40 вершин).
        Assert.AreEqual(40, meshData.Vertices.Count);
        Assert.AreEqual(60, meshData.Triangles.Count);
    }

    [Test]
    public void MeshBuilder_SolidSubChunk_GeneratesOnlyOuterShell()
    {
        var world = new World();
        var chunk = world.GetOrCreateChunk(Vector2Int.zero);

        // Заполняем весь подчанк камнем (4096 блоков)
        for (int x = 0; x < 16; x++)
        {
            for (int y = 0; y < 16; y++)
            {
                for (int z = 0; z < 16; z++)
                {
                    chunk.SetVoxel(x, y, z, new Voxel(BlockType.Stone));
                }
            }
        }

        var meshData = new MeshData();
        SubChunkMeshBuilder.Build(world, Vector2Int.zero, 0, meshData);

        // 6 внешних сторон по 16*16 = 256 граней каждая = 1536 граней
        // 1536 * 4 вершины = 6144 вершины (вместо 4096 * 24 = 98 304 без куллинга)
        int expectedFaces = 6 * 16 * 16;
        Assert.AreEqual(expectedFaces * 4, meshData.Vertices.Count);
        Assert.AreEqual(expectedFaces * 6, meshData.Triangles.Count);
    }

    [Test]
    public void MeshBuilder_AcrossChunkBoundary_CullsAdjacentFace()
    {
        var world = new World();
        var chunkA = world.GetOrCreateChunk(new Vector2Int(0, 0));
        var chunkB = world.GetOrCreateChunk(new Vector2Int(1, 0));

        // Блок на восточном краю чанка A (x=15)
        chunkA.SetVoxel(15, 5, 5, new Voxel(BlockType.Stone));
        // Блок на западном краю чанка B (мировой x=16, локальный x=0)
        chunkB.SetVoxel(0, 5, 5, new Voxel(BlockType.Stone));

        var meshData = new MeshData();
        SubChunkMeshBuilder.Build(world, Vector2Int.zero, 0, meshData);

        // У блока в чанке A восточная грань (+X) должна быть отсечена блоком в чанке B.
        // Остается 5 граней = 20 вершин.
        Assert.AreEqual(20, meshData.Vertices.Count);
        Assert.AreEqual(30, meshData.Triangles.Count);
    }
}
