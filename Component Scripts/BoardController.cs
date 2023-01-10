using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BoardController : MonoBehaviour

{
    public GameObject spacePrefab;
    public GameObject blockerPrefab;
    public GameObject antlerPrefab;
    public GameObject eyePrefab;
    public Synchronizer synchronizer;

    public List<(GameObject g, (int up, int across) pos, PieceType type)> players { get; private set; }
    // this may change in future bc it depends on world, not local space
    public Dictionary<(int up, int across), GameObject> spaceDict { get; private set; }
    public bool isInitialized { get; private set; }

    private static float TriSize = 0.19346f;
    private static float TriHeight = Mathf.Sqrt(3) / 2 * TriSize;
    private static float[] BoardLevels = { 0.051f, 0.083f, 0.115f };
    private static Vector3 WOffset = new Vector3(-0.05584711f, 0, 0.09673002f);
    private static Vector3 StartingPos = new Vector3(0.3350971f, BoardLevels[0], -0.58038f);
    private static (Color def, Color highlight) highlightColors = (Color.white, Color.cyan);
    private static int Rows = 7;

    private List<(int, int)> _beamSpaces;

    void Start()
    {
        spaceDict = GenerateBoard();
        players = SetupBoard();
        _beamSpaces = new List<(int, int)>();
        isInitialized = true;
    }

    void Update()
    {

    }

    private Dictionary<(int, int), GameObject> GenerateBoard()
    {
        var resDict = new Dictionary<(int up, int across), GameObject>();
        Transform spcParent = new GameObject("BoardGame_Spaces").transform;
        spcParent.SetParent(transform.parent);

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
                    StartingPos.x - TriHeight * (Rows - i),
                    newHeight,
                    StartingPos.z + rowOffset + j * TriSize);

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
        // final edits, set to invisible until further notice
        foreach ((int u, int a) k in resDict.Keys)
        {
            GameObject spc = resDict[k];
            spc.GetComponent<SpaceController>().SetSpace(k.u, k.a);
            spc.transform.SetParent(spcParent, true);
            if (!IsBlack(k)) spc.transform.localRotation = Quaternion.AngleAxis(-180, Vector3.up);
            spc.SetActive(false);
            synchronizer.OnLerpComplete.AddListener(spc.GetComponent<SpaceController>().FlipHighlightLerp);
        }
        return resDict;
    }

    private List<(GameObject, (int, int), PieceType)> SetupBoard()
    {
        var pieces = new List<(GameObject g, (int up, int across) pos, PieceType type)>();
        // place players
        Transform pieceParent = new GameObject("BoardGame_Pieces").transform;
        pieceParent.SetParent(transform.parent);

        pieces.Add(CreateAndPlacePiece(pieceParent, (0, 0), PieceType.Blocker));
        pieces.Add(CreateAndPlacePiece(pieceParent, (0, 1), PieceType.Antler));
        pieces.Add(CreateAndPlacePiece(pieceParent, (6, 7), PieceType.Eye));
        return pieces;
    }

    private (GameObject, (int, int), PieceType) CreateAndPlacePiece(Transform parent, (int up, int across) pos, PieceType type)
    {
        GameObject piece = null;
        // instantiate prefab
        switch (type)
        {
            case PieceType.Blocker:
                piece = GameObject.Instantiate(blockerPrefab, parent);
                break;
            case PieceType.Antler:
                piece = GameObject.Instantiate(antlerPrefab, parent);
                break;
            case PieceType.Eye:
                piece = GameObject.Instantiate(eyePrefab, parent);
                break;
            default:
                Debug.LogWarning($"Invalid piece type {type}!");
                break;
        }

        // fix rotation from prefab
        ChildRotationFix(piece, type);

        // put in starting position/rotation
        (GameObject, (int, int)?, PieceType) pieceTemp = (piece, null, type);
        MoveToSpace(pieceTemp, pos);
        if (IsBlack(pos))
        {
            piece.transform.localRotation = Quaternion.AngleAxis(-30, Vector3.up);
        }
        else
        {
            piece.transform.localRotation = Quaternion.AngleAxis(-90, Vector3.up);
        }
        return (piece, pos, type);
    }

    private void ChildRotationFix(GameObject g, PieceType type)
    {
        // manually adjust prefab children's rotations to align w their Vector3.forward
        foreach (Transform t in g.transform)
        {
            switch (type)
            {
                case PieceType.Blocker:
                case PieceType.Eye:
                    t.localRotation = Quaternion.AngleAxis(-90, Vector3.up);
                    break;
                case PieceType.Antler:
                    t.localRotation = Quaternion.AngleAxis(-30, Vector3.up);
                    break;
            }
        }
    }

    // helper to determine B/W based on coords
    private bool IsBlack((int up, int across) pos)
    {
        bool even = (pos.up + pos.across) % 2 == 0;
        // black is only even on first row
        return even && pos.up == 0 || !even && pos.up > 0;
    }

    // check if coord pos is in bounds
    private bool InBounds((int up, int across) pos)
    {
        bool firstRow = pos.up == 0 && pos.up <= pos.across && pos.across < (2 * Rows) - 1;
        return firstRow || (0 < pos.up && pos.up < Rows && pos.up <= pos.across && pos.across <= 2 * Rows - pos.up);
    }

    // return list of adjacent positions to (up, across)
    public List<(int, int)> GetAdjacent(int up, int across)
    {
        var adj = new List<(int, int)>();

        (int u, int a) left = (up, across - 1);
        if (InBounds(left)) adj.Add(left);

        (int u, int a) right = (up, across + 1);
        if (InBounds(right)) adj.Add(right);

        // W has lower face, B has upper face
        (int u, int a) up_down;
        if (IsBlack((up, across)))
        {
            if (up == 0) up_down = (up + 1, across + 1);
            else up_down = (up + 1, across);
        }
        else
        {
            if (up == 1) up_down = (up - 1, across - 1);
            else up_down = (up - 1, across);
        }
        if (InBounds(up_down)) adj.Add(up_down);

        // filter out occupied spaces - would use a mask if i understood them
        for (int i = 0; i < adj.Count; i++)
        {
            bool foundOccupied = false;
            for (int j = 0; j < players.Count && !foundOccupied; j++)
            {
                if (players[j].pos == adj[i])
                {
                    adj.RemoveAt(i);
                    i--;
                    foundOccupied = true;
                }
            }
        }
        return adj;
    }

    // set highlight visibility
    public void ToggleSpaces(List<(int up, int across)> spaces, bool? inBeam = null)
    {
        foreach (var s in spaces)
        {
            SpaceController spc = spaceDict[(s.up, s.across)].GetComponent<SpaceController>();
            // don't toggle beam spaces if we're not updating the beam!
            if (!spc.inBeam || inBeam != null)
                spc.gameObject.SetActive(!spc.gameObject.activeSelf);
            // allow beam spaces to be toggled when parameter supplied
            if (inBeam != null)
                spc.inBeam = inBeam ?? spc.inBeam;
        }
    }

    public void ToggleHighlight(GameObject piece)
    {
        MeshRenderer[] renderers = piece.GetComponentsInChildren<MeshRenderer>();

        foreach (Renderer r in renderers)
        {
            foreach (Material m in r.materials)
            {
                // update to use the custom simulation shader?
                if (m.color == highlightColors.def)
                {
                    m.color = highlightColors.highlight;
                }
                else m.color = highlightColors.def;
            }
        }
    }

    private bool MoveToSpace((GameObject obj, (int up, int across)? oldPos, PieceType type) piece, (int up, int across) newPos)
    {
        (int, int) BadPos = (99, 99);
        (int up, int across) oldPosNN = piece.oldPos ?? BadPos;
        // nullable arg for oldPos = first-time setup
        bool settingUp = oldPosNN.Item1 == BadPos.Item1;

        GameObject newSpc = spaceDict[(newPos.up, newPos.across)];
        // check for valid position
        if (newSpc != null)
        {
            SpaceController newSpcController = newSpc.GetComponent<SpaceController>();
            // move/rotate piece, set controller occupants
            if (!settingUp)
            {
                GameObject oldSpc = spaceDict[(oldPosNN.up, oldPosNN.across)];
                SpaceController oldSpcController = oldSpc.GetComponent<SpaceController>();
                oldSpcController.SetOccupant(null);

                Vector3 lookPos = newSpc.transform.position - oldSpc.transform.position;
                // remove y component - only rotating in X/Z plane
                lookPos.y = 0.0f;
                Debug.DrawLine(newSpc.transform.position, oldSpc.transform.position, Color.white, 10);
                piece.obj.transform.localRotation = Quaternion.LookRotation(lookPos);
            }
            newSpcController.SetOccupant(piece.obj);
            piece.obj.transform.localPosition = newSpc.transform.localPosition;
            return true;
        }
        Debug.LogWarning($"Tried to move to impossible position {newPos.up}, {newPos.across}!");
        return false;
    }

    public void TryMove(int pIdx, (int, int) newPos)
    {
        // avoid updating position unless we succeeded
        bool succeeded = MoveToSpace(players[pIdx], newPos);
        if (succeeded)
        {
            players[pIdx] = (players[pIdx].g, newPos, players[pIdx].type);
        }
    }

    public void UpdateBeam()
    {
        // see who's been hit and remove
        var newBeamSpaces = new List<(int, int)>();
        (int u, int a) eyePos = players.Where(p => p.type == PieceType.Eye).FirstOrDefault().pos;

        for (int i = 1; i < Rows; i++)
        {
            (int, int)[] currDepthSpaces;
            // check first row conditions
            int lowerOffset() => eyePos.u - i == 0 ? 1 : 0;
            int upperOffset() => eyePos.u + i == 1 ? 1 : 0;
            // add spaces to array along 3 lines stretching from triangle vertices
            if (IsBlack(eyePos))
            {
                currDepthSpaces = new (int, int)[] {
                    // below
                    (eyePos.u - i, eyePos.a - lowerOffset()),
                    // upper R diagonal
                    (eyePos.u + i, eyePos.a + 3 * i - 1 + upperOffset()),
                    (eyePos.u + i, eyePos.a + 3 * i + upperOffset()),
                    // upper L diagonal
                    (eyePos.u + i, eyePos.a - 3 * i + 1 + upperOffset()),
                    (eyePos.u + i, eyePos.a - 3 * i + upperOffset())
                };
            }
            else
            {
                currDepthSpaces = new (int, int)[] {
                    // above
                    (eyePos.u + i, eyePos.a + upperOffset()),
                    // lower R diagonal
                    (eyePos.u - i, eyePos.a + 3 * i - 1 - lowerOffset()),
                    (eyePos.u - i, eyePos.a + 3 * i - lowerOffset()),
                    // lower L diagonal
                    (eyePos.u - i, eyePos.a - 3 * i + 1 - lowerOffset()),
                    (eyePos.u - i, eyePos.a - 3 * i - lowerOffset())
                };
            }
            // filter out-of-bounds
            var currInBounds = from cSpc in currDepthSpaces where InBounds(cSpc) select cSpc;
            newBeamSpaces.AddRange(currInBounds.ToList());
        }
        // reset (turn off) old spaces
        ToggleSpaces(_beamSpaces, false);
        // show new ones
        _beamSpaces = newBeamSpaces;
        ToggleSpaces(_beamSpaces, true);
    }

    public List<int> CheckBeam()
    {
        // return a list of players by index to be removed
        var result = new List<int>();
        for (int i = 0; i < players.Count; i++)
        {
            foreach ((int, int) spc in _beamSpaces)
            {
                if (players[i].pos == spc)
                {
                    Debug.Log($"player {players[i].g.name} hit at {players[i].pos}");
                    result.Add(i);
                }
            }
        }
        return result;
    }
}
