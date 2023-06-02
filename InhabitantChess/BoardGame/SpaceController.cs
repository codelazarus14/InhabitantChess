using UnityEngine;


namespace InhabitantChess.BoardGame
{
    public class SpaceController : MonoBehaviour
    {
        public bool InBeam { get; private set; }
        public (int up, int across) Space { get; private set; }

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

        public void SetVisible(bool visible)
        {
            GetComponent<MeshRenderer>().enabled = visible;
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