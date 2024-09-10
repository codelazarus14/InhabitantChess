using UnityEngine;

namespace InhabitantChess.BoardGame
{
    public class SpaceController : MonoBehaviour
    {
        public bool InBeam { get; private set; }
        public (int up, int across) Position { get; private set; }

        private const float BeamEmissionMin = 0.2f, BeamEmissionMax = 0.8f;
        private const float BeamEmissionTimeScale = 1f;

        private Material _ogMaterial, _beamMaterial;
        private MeshRenderer _meshRenderer;
        private Collider[] _colliders;

        private void Update()
        {
            if (!InBeam) return;

            float opacity = (Mathf.Sin(Time.time * BeamEmissionTimeScale) + 1f) / 2;
            opacity = (BeamEmissionMax - BeamEmissionMin) * opacity + BeamEmissionMin;
            _beamMaterial.SetFloat("_VertColor", opacity);
        }

        public void SetMaterials(Material beamMat)
        {
            if (_meshRenderer == null)
                _meshRenderer = GetComponent<MeshRenderer>();
            _ogMaterial = _meshRenderer.sharedMaterial;
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
            _meshRenderer.sharedMaterial = inBeam ? _beamMaterial : _ogMaterial;
            InBeam = inBeam;
        }

        public void SetPosition(int up, int across)
        {
            Position = (up, across);
        }
    }
}