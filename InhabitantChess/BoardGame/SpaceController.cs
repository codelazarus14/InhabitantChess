using System;
using UnityEngine;

namespace InhabitantChess.BoardGame
{
    public class SpaceController : MonoBehaviour
    {
        public (int up, int across) Position { get; private set; }

        private const float BeamEmissionMin = 0.2f, BeamEmissionMax = 0.8f;
        private const float BeamEmissionTimeScale = 1f;

        [Flags]
        private enum SpaceState
        {
            Visible = 1,
            Interactive = 2,
            HighlightedMove = 4,
            InBeam = 8
        }
        private SpaceState _state;

        private Material _defaultMaterial, _beamMaterial;
        private MeshRenderer _meshRenderer;
        private Collider[] _colliders;

        public bool InBeam => _state.HasFlag(SpaceState.InBeam);

        private void Update()
        {
            if (!InBeam) return;

            // TODO: add new color for beam when it is also a legal move
            // sin wave
            float opacity = (Mathf.Sin(Time.time * BeamEmissionTimeScale) + 1f) / 2;
            // scale sin to specific min/max range
            opacity = (BeamEmissionMax - BeamEmissionMin) * opacity + BeamEmissionMin;
            _beamMaterial.SetFloat("_VertColor", opacity);
        }

        public void SetMaterials(Material beamMat)
        {
            if (_meshRenderer == null)
                _meshRenderer = GetComponent<MeshRenderer>();
            _defaultMaterial = _meshRenderer.sharedMaterial;
            _beamMaterial = beamMat;

        public void SetPosition(int up, int across)
        {
            Position = (up, across);
        }

        public void SetVisible(bool visible)
        {
            _meshRenderer.enabled = visible;
            SetStateFlag(visible, SpaceState.Visible);
        }

        public void SetInteractive(bool active)
        {
            if (_colliders == null)
                _colliders = GetComponentsInChildren<Collider>();
            foreach (var col in _colliders)
                col.enabled = active;
            SetStateFlag(active, SpaceState.Interactive);
        }

        public void SetInBeam(bool inBeam)
        {
            _meshRenderer.sharedMaterial = inBeam ? _beamMaterial : _defaultMaterial;
            SetStateFlag(inBeam, SpaceState.InBeam);
        }

        public void SetHighlightedMove(bool highlightedMove)
        {
            _beamMaterial.SetColor(VertexGradientColorID, highlightedMove ? _beamHighlightedMove : _beamDefault);
            SetStateFlag(highlightedMove, SpaceState.HighlightedMove);
        }

        private void SetStateFlag(bool value, SpaceState flag)
        {
            // clear or set flag bit based on boolean
            _state = value ? _state | flag : _state & ~flag;
        }
    }
}