using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpaceController : MonoBehaviour
{
    public bool inBeam = false;
    public (int up, int across) space { get; private set; }

    private GameObject _occupant = null;
    private float min = 0.0f, max = 0.4f;
    private Material _mat;

    void Start()
    {
        FixMaterial();
    }

    void Update()
    {
        // animate slow blinking
        if (!inBeam)
        {
            Material m = GetComponent<MeshRenderer>().material;
            m.color = new Color(m.color.r, m.color.g, m.color.b, Mathf.Lerp(min, max, Synchronizer.t));
            
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
        float temp = max;
        max = min;
        min = temp;
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
        space = (up, across);
    }
}
