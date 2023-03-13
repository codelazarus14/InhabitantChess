using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpaceController : MonoBehaviour
{
    public bool InBeam = false;
    public (int up, int across) Space { get; private set; }

    private GameObject _occupant = null;
    private Material _mat;
    private Color _beamColor = Color.green;
    private float _min = 0.0f, _max = 0.4f;
    private bool _beamVisualized;

    void Start()
    {
        FixMaterial();
    }

    void Update()
    {
        // animate slow blinking
        if (!InBeam)
        {
            if (_beamVisualized) _beamVisualized = false;
            _mat.color = new Color(1, 1, 1, Mathf.Lerp(_min, _max, Synchronizer.t));
        }
        else if (!_beamVisualized)
        {
            _mat.color = _beamColor;
            _beamVisualized = true;
        }
    }

    private void FixMaterial()
    {
        // see SetupMaterialWithBlendMode from Standard Shader UI code - really just forcing this to be transparent
        // https://github.com/Unity-Technologies/UnityCsReference/blob/master/Editor/Mono/Inspector/StandardShaderGUI.cs
        _mat = GetComponent<MeshRenderer>().material;
        _mat.SetOverrideTag("RenderType", "Transparent");
        _mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
        _mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        _mat.SetFloat("_ZWrite", 0.0f);
        _mat.DisableKeyword("_ALPHATEST_ON");
        _mat.DisableKeyword("_ALPHABLEND_ON");
        _mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        _mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
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
