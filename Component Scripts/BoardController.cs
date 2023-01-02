using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoardController : MonoBehaviour

{
    public GameObject spacePrefab;
    public GameObject blockerPrefab;
    public GameObject antlerPrefab;
    public GameObject eyePrefab;

    private static float TriSize = 0.19346f;
    private static float TriHeight = Mathf.Sqrt(3) / 2 * TriSize;
    private static Vector3 WOffset = new Vector3(-0.05584711f, 0, 0.09673002f);
    private static float[] BoardLevels = { 0.05f, 0.0815f, 0.1142f };
    private static int Rows = 7;

    private Vector3 _startingPos = new Vector3(0.3350971f, BoardLevels[0], -0.58038f);
    private Dictionary<(int up, int across), GameObject> _spaceDict;

    void Start()
    {
        _spaceDict = GenerateBoard();
        Setup();
        
    }

    void Update()
    {

    }

    private Dictionary<(int, int), GameObject> GenerateBoard()
    {
        var resDict = new Dictionary<(int up, int across), GameObject>();
        Transform spcParent = new GameObject("spaces").transform;

        for (int i = Rows; i > 0; i--)
        {
            // j keep track of # of B spaces per row,
            // i will store real count in dict
            int idx = Rows - i;
            float rowOffset = idx * TriSize / 2;

            for (int j = 0; j < i; j++)
            {
                // default to lowest height level
                float newHeight = BoardLevels[0];
                GameObject hSpace;

                // find B positions relative to starting pos
                Vector3 newPos = new Vector3(
                    _startingPos.x - TriHeight * (Rows - i),
                    newHeight,
                    _startingPos.z + rowOffset + j * TriSize);

                // don't add W to left corner
                if (Rows > i && j == 0)
                {
                    // add W spaces to left edge 
                    Vector3 newPosW = new Vector3(
                        newPos.x - WOffset.x,
                        newHeight,
                        newPos.z - WOffset.z);

                    hSpace = GameObject.Instantiate(spacePrefab, newPosW, Quaternion.identity);
                    resDict[(Rows - i, idx)] = hSpace;

                    idx++;
                }

                // determine height based on position
                if (Rows - 1 > i && i > 4 && 1 < j && j < i - 2) newHeight = BoardLevels[2];
                else if (Rows > i && i > 1 && 0 < j && j < i - 1) newHeight = BoardLevels[1];

                // only update y (x/z already defined for left edge W, which can never be elevated)
                newPos.y = newHeight;

                // add B spaces
                hSpace = GameObject.Instantiate(spacePrefab, newPos, Quaternion.identity);
                resDict[(Rows - i, idx)] = hSpace;

                idx++;

                // don't add W to right corner
                if (Rows > i || j < i - 1)
                {
                    // add W spaces to right of every other B piece
                    if (Rows - 1 > i && i > 3 && 0 < j && j < i - 2) newHeight = BoardLevels[2];
                    else if (Rows > i && i > 1 && 0 <= j && j < i - 1) newHeight = BoardLevels[1];
                    else newHeight = BoardLevels[0];

                    // need to update all three components (right/down of B and also diff height)
                    Vector3 newPosW = new Vector3(
                        newPos.x - WOffset.x,
                        newHeight,
                        newPos.z + WOffset.z);

                    hSpace = GameObject.Instantiate(spacePrefab, newPosW, Quaternion.identity);
                    resDict[(Rows - i, idx)] = hSpace;

                    idx++;
                }
            }
        }
        // set to invisible until further notice
        foreach (var k in resDict.Keys)
        {
            GameObject spc = resDict[k];
            spc.transform.SetParent(spcParent, true);
            spc.SetActive(false);
        }
        return resDict;
    }

    private void Setup()
    {
        // place players
        Transform pieceParent = new GameObject("pieces").transform;
        GameObject p1 = GameObject.Instantiate(blockerPrefab, pieceParent);
        // optional oldPos argument if we're not updating a previous space
        Transform p1_pos = MoveToFromSpace(p1, (0,0));

        GameObject p2 = GameObject.Instantiate(antlerPrefab, pieceParent);
        Transform p2_pos = MoveToFromSpace(p2, (0,1));

        // testing - in future, should check for null returns
        // and loop until we get a new move
        p1_pos = MoveToFromSpace(p1, (1,1), p1_pos);
        // should log an error
        p1_pos = MoveToFromSpace(p1, (0,1), p1_pos);
    }

    public GameObject GetSpace(int x, int y)
    {
        GameObject space = _spaceDict[(x, y)];
        if (space == null)
        {
            Debug.LogWarning($"Invalid board position {x}, {y}!");
            return null;
        }
        else return space;
    }

    public (int, int)? GetPos(Transform t)
    {
        (int, int)? pos = null;
        foreach (var s in _spaceDict)
        {
            if (s.Value == t) pos = s.Key;
        }
        return pos;
    }

    private Transform MoveToFromSpace(GameObject piece, (int up, int across) newPos, Transform oldSpc = null)
    {
        // nullable arg for oldPos = first-time setup
        bool settingUp = oldSpc == null;

        GameObject newSpc = GetSpace(newPos.up, newPos.across);
        // check for valid position
        if (newSpc != null)
        {
            SpaceController newSpcController = newSpc.GetComponent<SpaceController>();
            // don't move to occupied position!
            bool isOccupied = newSpcController.GetOccupant() != null;
            if (!isOccupied)
            {                
                // move/rotate piece, set controller occupants
                if (!settingUp) {
                    SpaceController oldSpcController = oldSpc.GetComponent<SpaceController>();
                    oldSpcController.SetOccupant(null);

                    //TODO: still not working
                    Vector3 lookPos = newSpc.transform.position - oldSpc.transform.position;
                    piece.transform.localRotation = Quaternion.LookRotation(lookPos);
                }
                newSpcController.SetOccupant(piece);
                piece.transform.localPosition = newSpc.transform.localPosition;
                return newSpc.transform;
            }
            else
            {
                Debug.LogWarning($"Tried to move to occupied position {newPos.up}, {newPos.across}!");
                return null;
            }
        }
        Debug.LogWarning($"Tried to move to impossible position {newPos.up}, {newPos.across}!");
        return null;
    }
}
