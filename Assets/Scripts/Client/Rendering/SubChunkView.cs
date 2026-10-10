using UnityEngine;

namespace Client.Rendering
{
    /// <summary>
    /// Представление одного подчанка (16x16x16) в сцене Unity.
    /// Содержит MeshFilter и MeshRenderer для отображения геометрии.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class SubChunkView : MonoBehaviour
    {
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private Mesh _mesh;

        private void Awake()
        {
            _meshFilter = GetComponent<MeshFilter>();
            _meshRenderer = GetComponent<MeshRenderer>();

            _mesh = new Mesh
            {
                name = $"SubChunkMesh_{gameObject.name}"
            };
            _meshFilter.sharedMesh = _mesh;
        }

        private void OnDestroy()
        {
            if (_mesh != null)
            {
                Destroy(_mesh);
            }
        }

        /// <summary>
        /// Применение сформированных полигональных данных к мешу подчанка.
        /// </summary>
        public void ApplyMeshData(MeshData meshData, Material material)
        {
            if (_meshRenderer == null)
            {
                _meshRenderer = GetComponent<MeshRenderer>();
                _meshFilter = GetComponent<MeshFilter>();
                if (_mesh == null)
                {
                    _mesh = new Mesh { name = $"SubChunkMesh_{gameObject.name}" };
                    _meshFilter.sharedMesh = _mesh;
                }
            }

            if (meshData == null || meshData.IsEmpty)
            {
                _meshRenderer.enabled = false;
                _mesh.Clear();
                return;
            }

            _meshRenderer.enabled = true;
            _meshRenderer.sharedMaterial = material;
            meshData.ApplyToMesh(_mesh);
        }
    }
}
