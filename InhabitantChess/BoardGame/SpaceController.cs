using System;
using UnityEngine;

namespace InhabitantChess.BoardGame
{
    public class SpaceController : MonoBehaviour
    {
        public (int up, int across) Position { get; private set; }

        private const int VertColorID = 621;
        private const int VertexGradientColorID = 623;
        // control range of opacity/glow of beam highlight
        private const float BeamEmissionMin = 0.2f, BeamEmissionMax = 0.6f;
        private const float BeamEmissionTimeScale = 1f;

        private static Color s_beamDefault = new Color(0.0786f, 4.5948f, 3.4089f);
        private static Color s_beamHighlighted = new Color(3.0786f, 4.5948f, 0.4089f);
        // TODO: add color variant for blocker (yellow - caution, green - good/blockable?)

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
        private MaterialPropertyBlock _beamMPB;
        private MeshRenderer _meshRenderer;
        private Collider[] _colliders;

        public bool InBeam => _state.HasFlag(SpaceState.InBeam);

        private void Update()
        {
            if (!InBeam) return;

            // sin wave
            float opacity = (Mathf.Sin(Time.time * BeamEmissionTimeScale) + 1f) / 2;
            // scale sin to specific min/max range
            opacity = (BeamEmissionMax - BeamEmissionMin) * opacity + BeamEmissionMin;
            _beamMPB.SetFloat(VertColorID, opacity);
            _meshRenderer.SetPropertyBlock(_beamMPB);
        }

        public void SetMaterials(Material beamMat)
        {
            if (_meshRenderer == null)
                _meshRenderer = GetComponent<MeshRenderer>();
            _defaultMaterial = _meshRenderer.sharedMaterial;
            _beamMaterial = beamMat;
            _beamMPB = new MaterialPropertyBlock();
        }

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
            // SetVector works properly when setting a color, unlike SetColor
            // (restricts color space? something to do with being a float4 behind the scenes?)
            _beamMPB.SetVector(VertexGradientColorID, highlightedMove ? s_beamHighlighted : s_beamDefault);
            SetStateFlag(highlightedMove, SpaceState.HighlightedMove);
        }

        private void SetStateFlag(bool value, SpaceState flag)
        {
            // clear or set flag bit based on boolean
            _state = value ? _state | flag : _state & ~flag;
        }
    }
}