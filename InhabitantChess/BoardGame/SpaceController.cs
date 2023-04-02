using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace InhabitantChess.BoardGame
{
    public class SpaceController : MonoBehaviour
    {
        public bool InBeam = false;
        public (int up, int across) Space { get; private set; }

        private GameObject _occupant = null;
        private Material _ogMaterial, _beamMaterial;
        private float _min = 0.0f, _max = 0.4f;

        private void Start()
        {

        }

        private void Update()
        {

        }

        public void SetMaterials(Material beamMat)
        {
            _ogMaterial = GetComponent<MeshRenderer>().material;
            _beamMaterial = beamMat;
        }

        public void SetBeam(bool inBeam)
        {
            MeshRenderer mesh = GetComponent<MeshRenderer>();
            if (inBeam) mesh.material = _beamMaterial;
            else mesh.material = _ogMaterial;
            InBeam = inBeam;
        }

        public void FlipHighlightLerp()
        {
            float temp = _max;
            _max = _min;
            _min = temp;
        }

        public void SetOccupant(GameObject g)
        {
            _occupant = g;
        }

        public GameObject GetOccupant()
        {
            return _occupant;
        }

        public void SetSpace(int up, int across)
        {
            Space = (up, across);
        }
    }
}