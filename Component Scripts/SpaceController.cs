using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpaceController : MonoBehaviour
{
    private GameObject _occupant;
    private bool _inBeam = false;

    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void SetOccupant(GameObject g)
    {
        _occupant = g;
    }

    public GameObject GetOccupant()
    {
        return _occupant;
    }
}
