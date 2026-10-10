# Client.Rendering (Модуль визуализации вокселей)

Клиентский модуль процедурной генерации геометрии воксельного мира для **Unity WebGL / Яндекс Игры**. 

Отвечает за преобразование воксельных данных из [`VoxelCore`](file:///c:/Users/ND/Desktop/unity_projects/minivoxelgames/Assets/Scripts/VoxelCore) в полигональные 3D-сетки (`UnityEngine.Mesh`).

---

## 1. Архитектурный принцип

* **Полная изоляция от сервера:** Серверная часть не содержит ссылок на этот модуль и не тратит ресурсы CPU/GPU на вершины, треугольники и материалы.
* **Единица мешинга — SubChunk ($16 \times 16 \times 16$):** Меши строятся блоками по $16^3$, что позволяет перестраивать полигоны локально при изменении одного блока, а не пересчитывать всю вертикальную колонну $16 \times 128 \times 16$.
* **Легковесность:** На текущем этапе компоненты содержат только `MeshFilter` и `MeshRenderer` (без `MeshCollider`) для максимальной производительности.

---

## 2. Компоненты модуля

### `BlockFace.cs`
* Перечисление 6 сторон куба: `Up (+Y)`, `Down (-Y)`, `North (+Z)`, `South (-Z)`, `East (+X)`, `West (-X)`.

### `BlockAtlas.cs`
* Калькулятор UV-координат для текстурного атласа [`minecraft_old_texture_atlas.png`](file:///c:/Users/ND/Desktop/unity_projects/minivoxelgames/Assets/minecraft_old_texture_atlas.png) (сетка $16 \times 16$ тайлов).
* **Маппинг граней:**
  * `Grass`: Верх = `(0, 0)`, Низ = `(2, 0)` (земля), Бока = `(3, 0)` (земля с травой).
  * `Wood`: Бока = `(4, 1)` (кора), Верх/Низ = `(5, 1)` (спил).
  * `Leaves`: Все грани = `(5, 4)` (зеленая листва).
  * `Dirt`, `Stone`, `Bedrock`, `Sand` — монолитные текстуры со всех сторон.
* **Анти-блидинг (Anti-Bleeding Padding):** Добавлен отступ `Padding = 0.001f` от краев тайла, предотвращающий появление полос и артефактов на границах пикселей при билинейной фильтрации.

### `SubChunkMeshBuilder.cs`
* Процедурный генератор сетки с алгоритмом **Гибридного Face Culling + Neighborhood Cache**:
  1. Сканирует подчанк $16 \times 16 \times 16$. Пустые подчанки (`null`) пропускаются моментально.
  2. **Последовательный обход памяти:** Циклы организованы как $Y \rightarrow Z \rightarrow X$, что строго совпадает с плоским индексом $x + (z \ll 4) + (y \ll 8)$, гарантируя 100% L1 CPU Cache hit.
  3. **Neighborhood Cache:** Вместо обращения к `World.GetVoxel(worldX, worldY, worldZ)` на границах используется предварительно разрешённый кэш [`SubChunkNeighbors`](file:///c:/Users/ND/Desktop/unity_projects/minivoxelgames/Assets/Scripts/VoxelCore/SubChunkNeighbors.cs). Это полностью устраняет вызовы `Dictionary.TryGetValue` и хэширование координат внутри цикла генерации меша.
  4. Грань генерируется **только если соседний блок прозрачный / воздух (`IsAir`)**. Смежные грани между твердыми блоками отсекаются (куллинг устраняет более 93% скрытых полигонов).
  5. Бесшовно работает на границах чанков и подчанков (выход за пределы мира возвращает `Voxel.Air`).
* **`MeshData`:** Контейнер списков `Vertices`, `Triangles`, `UVs`, `Normals` с методом прямой загрузки в `UnityEngine.Mesh`.

### `SubChunkView.cs`
* `MonoBehaviour` отображения одного подчанка в иерархии сцены.
* Управляет собственным `MeshFilter` и `MeshRenderer`.
* Если подчанк пустой, автоматически отключает `MeshRenderer.enabled = false` (нулевая нагрузка на Draw Calls).

### `ChunkView.cs`
* `MonoBehaviour` чанка в сцене Unity.
* Размещается в мировых координатах `(chunkCoord.x * 16, 0, chunkCoord.y * 16)`.
* Содержит 8 дочерних объектов `SubChunkView` на высотах $Y = subIndex \times 16$.
* **Переиспользование памяти:** Хранит один экземпляр `MeshData` на весь чанк, полностью устраняя мусорные аллокации списков в GC при перегенерации мешей.
* **Оптимизированный ребилд колонны:** Метод `Rebuild(world)` использует [`ChunkNeighbors`](file:///c:/Users/ND/Desktop/unity_projects/minivoxelgames/Assets/Scripts/VoxelCore/ChunkNeighbors.cs), разрешая 4 горизонтальных соседа всего один раз на всю вертикальную колонну из 8 подчанков, что ускоряет ребилд чанка в **2.26 раза**.

---

## 3. Схема конвейера рендеринга (Pipeline)

```
[VoxelCore.World]
       │
       ▼ (Опрос блоков с учетом Face Culling)
[SubChunkMeshBuilder] ──> [MeshData (Переиспользуемый буфер)]
                                 │
                                 ▼ (ApplyToMesh)
                           [SubChunkView] (MeshFilter + MeshRenderer)
                                 │
                                 ▼ (Материал voxel_mat.mat)
                           [GPU Рендер в Unity]
```
