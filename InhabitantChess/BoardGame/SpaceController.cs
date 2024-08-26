using UnityEngine;


namespace InhabitantChess.BoardGame
{
    public class SpaceController : MonoBehaviour
    {
        public bool InBeam { get; private set; }
        public (int up, int across) Space { get; private set; }

        private Material _ogMaterial, _beamMaterial;
        private MeshRenderer _meshRenderer;
        private Collider[] _colliders;
        private float _min = 0.0f, _max = 0.4f;

        private void Update()
        {
            // TODO: fix someday - MPB

            //if (InBeam)
            //{
            //    float o = Synchronizer.t;
            //    var mat = GetComponent<MeshRenderer>().material;
            //    mat.SetColor("_EmissionColor", new Color(o, o, o, o));
            //}
        }

        public void SetMaterials(Material beamMat)
        {
            if (_meshRenderer == null)
                _meshRenderer = GetComponent<MeshRenderer>();
            _ogMaterial = _meshRenderer.material;
            _beamMaterial = beamMat;
        }

        public void SetVisible(bool visible)
        {
            _meshRenderer.enabled = visible;
        }

        public void SetInteractive(bool active)
        {
            if (_colliders == null)
                _colliders = GetComponentsInChildren<Collider>();
            foreach (var col in _colliders)
                col.enabled = active;
        }

        public void SetBeam(bool inBeam)
        {
            _meshRenderer.material = inBeam ? _beamMaterial : _ogMaterial;
            InBeam = inBeam;
        }

        public void FlipHighlightLerp()
        {
            float temp = _max;
            _max = _min;
            _min = temp;
        }

        public void SetSpace(int up, int across)
        {
            Space = (up, across);
        }
    }
}