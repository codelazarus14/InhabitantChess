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

    private static float TriSize = 0.19346f;
    private static float TriHeight = Mathf.Sqrt(3) / 2 * TriSize;
    private static Vector3 WOffset = new Vector3(-0.05584711f, 0, 0.09673002f);
    private static float[] BoardLevels = { 0.05f, 0.0815f, 0.1142f };
    private static int Rows = 7;
    private static (int, int) BadPos = (99, 99);

    // this may change in future bc it depends on world, not local space
    private Vector3 _startingPos = new Vector3(0.3350971f, BoardLevels[0], -0.58038f);
    private Dictionary<(int up, int across), GameObject> _spaceDict;
    private List<(GameObject g, (int up, int across) pos, PieceType type)> _pieces;
    private bool _playingGame = false;

    private enum PieceType
    {
        Blocker,
        Antler,
        Eye
    }

    void Start()
    {
        _spaceDict = GenerateBoard();
        _pieces = SetupBoard();
        _playingGame = true;
    }

    void Update()
    {
        if (!_playingGame) return;
        // testing without input
        StartCoroutine(moveWaiter());
        _playingGame = false;
    }

    private IEnumerator moveWaiter()
    {
        int waitTime = 1;
        yield return new WaitForSeconds(waitTime);
        TryMove(0, (1, 1));
        // should log an error
        yield return new WaitForSeconds(waitTime);
        TryMove(0, (0, 1));
        yield return new WaitForSeconds(waitTime);
        TryMove(0, (1, 2));
        yield return new WaitForSeconds(waitTime);
        TryMove(0, (1, 3));
        yield return new WaitForSeconds(waitTime);
        TryMove(0, (1, 4));
        yield return new WaitForSeconds(waitTime);
        TryMove(0, (2, 4));
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

    private List<(GameObject, (int, int), PieceType)> SetupBoard()
    {
        var pieces = new List<(GameObject g, (int up, int across) pos, PieceType type)>();
        // place players
        Transform pieceParent = new GameObject("pieces").transform;

        pieces.Add(CreateAndPlacePiece(pieceParent, (0,0), PieceType.Blocker));
        // oldPos argument optional if we're not updating a previous space
        pieces.Add(CreateAndPlacePiece(pieceParent, (0,1), PieceType.Antler));
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

    private bool IsBlack((int up, int across) pos)
    {
        bool even = (pos.up + pos.across) % 2 == 0;
        return even && pos.up == 0 || !even && pos.up > 0;
    }

    private bool MoveToSpace((GameObject obj, (int up, int across)? oldPos, PieceType type) piece, (int up, int across) newPos)
    {
        (int up, int across) oldPosNN = piece.oldPos ?? BadPos;
        // nullable arg for oldPos = first-time setup
        bool settingUp = oldPosNN.Item1 == BadPos.Item1;

        GameObject newSpc = _spaceDict[(newPos.up, newPos.across)];
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
                    GameObject oldSpc = _spaceDict[(oldPosNN.up, oldPosNN.across)];
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
            else
            {
                Debug.LogWarning($"Tried to move to occupied position {newPos.up}, {newPos.across}!");
                return false;
            }
        }
        Debug.LogWarning($"Tried to move to impossible position {newPos.up}, {newPos.across}!");
        return false;
    }

    private bool TryMove(int pieceIdx, (int, int) newPos)
    {
        // avoid updating position unless we succeeded
        bool succeeded = MoveToSpace(_pieces[pieceIdx], newPos);
        if (succeeded)
        {
            _pieces[pieceIdx] = (_pieces[pieceIdx].g, newPos, _pieces[pieceIdx].type);
        }
        return succeeded;
    }
}
