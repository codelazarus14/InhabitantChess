using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpaceController : MonoBehaviour
{
    public bool inBeam = false;
    public (int up, int across) space { get; private set; }

    private GameObject _occupant = null;

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

    public void SetSpace(int up, int across)
    {
        space = (up, across);
    }
}
