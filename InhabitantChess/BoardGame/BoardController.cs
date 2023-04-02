using System.Collections;
using System.Collections.Generic;
using System.EnterpriseServices;
using System.Linq;
using UnityEngine;

namespace InhabitantChess.BoardGame
{
    public class BoardController : MonoBehaviour

    {
        public GameObject SpacePrefab;
        public GameObject BlockerPrefab;
        public GameObject AntlerPrefab;
        public GameObject EyePrefab;
        public Synchronizer Synchronizer;
        public Shader HighlightShader;
        public Material[] HighlightMaterials;

        public List<(GameObject g, (int up, int across) pos, PieceType type)> Players { get; private set; }
        // this may change in future bc it depends on world, not local space
        public Dictionary<(int up, int across), GameObject> SpaceDict { get; private set; }
        public bool IsInitialized { get; private set; }

        private static float s_triSize = 0.19346f;
        private static float s_triHeight = Mathf.Sqrt(3) / 2 * s_triSize;
        private static float[] s_boardLevels = { 0.051f, 0.083f, 0.115f };
        private static Vector3 s_wOffset = new Vector3(-0.05584711f, 0, 0.09673002f);
        private static Vector3 s_startingPos = new Vector3(0.3350971f, s_boardLevels[0], -0.58038f);
        private static int s_Rows = 7;

        private List<(int, int)> _beamSpaces;
        private Transform _spcParent, _pieceParent;

        private void Start()
        {
            // unused - BoardGameController.Start() runs Init() after finding component
        }

        private void Update()
        {

        }

        public void Init()
        {
            SpaceDict = GenerateBoard();
            Players = SetupBoard();
            _beamSpaces = new List<(int, int)>();
            IsInitialized = true;
        }

        private Dictionary<(int, int), GameObject> GenerateBoard()
        {
            if (SpaceDict != null) return SpaceDict;

            var resDict = new Dictionary<(int up, int across), GameObject>();
            if (_spcParent == null)
            {
                _spcParent = new GameObject("BoardGame_Spaces").transform;
                _spcParent.SetParent(transform.parent);
                _spcParent.localPosition = Vector3.zero;
                _spcParent.localRotation = Quaternion.identity;
            }

            for (int i = s_Rows; i > 0; i--)
            {
                // j keep track of # of B spaces per row,
                // i will store real count in dict
                int idx = s_Rows - i;
                float rowOffset = idx * s_triSize / 2;

                for (int j = 0; j < i; j++)
                {
                    // default to lowest height level
                    float newHeight = s_boardLevels[0];
                    GameObject hSpace;

                    // find B positions relative to starting pos
                    Vector3 newPos = new Vector3(
                        s_startingPos.x - s_triHeight * (s_Rows - i),
                        newHeight,
                        s_startingPos.z + rowOffset + j * s_triSize);

                    // don't add W to left corner
                    if (s_Rows > i && j == 0)
                    {
                        // add W spaces to left edge 
                        Vector3 newPosW = new Vector3(
                            newPos.x - s_wOffset.x,
                            newHeight,
                            newPos.z - s_wOffset.z);

                        hSpace = Instantiate(SpacePrefab, _spcParent);
                        hSpace.transform.localPosition = newPosW;
                        hSpace.transform.localRotation = Quaternion.identity;
                        resDict[(s_Rows - i, idx)] = hSpace;

                        idx++;
                    }

                    // determine height based on position
                    if (s_Rows - 1 > i && i > 4 && 1 < j && j < i - 2) newHeight = s_boardLevels[2];
                    else if (s_Rows > i && i > 1 && 0 < j && j < i - 1) newHeight = s_boardLevels[1];

                    // only update y (x/z already defined for left edge W, which can never be elevated)
                    newPos.y = newHeight;

                    // add B spaces
                    hSpace = Instantiate(SpacePrefab, _spcParent);
                    hSpace.transform.localPosition = newPos;
                    hSpace.transform.localRotation = Quaternion.identity;
                    resDict[(s_Rows - i, idx)] = hSpace;

                    idx++;

                    // don't add W to right corner
                    if (s_Rows > i || j < i - 1)
                    {
                        // add W spaces to right of every other B piece
                        if (s_Rows - 1 > i && i > 3 && 0 < j && j < i - 2) newHeight = s_boardLevels[2];
                        else if (s_Rows > i && i > 1 && 0 <= j && j < i - 1) newHeight = s_boardLevels[1];
                        else newHeight = s_boardLevels[0];

                        // need to update all three components (right/down of B and also diff height)
                        Vector3 newPosW = new Vector3(
                            newPos.x - s_wOffset.x,
                            newHeight,
                            newPos.z + s_wOffset.z);

                        hSpace = Instantiate(SpacePrefab, _spcParent);
                        hSpace.transform.localPosition = newPosW;
                        hSpace.transform.localRotation = Quaternion.identity;
                        resDict[(s_Rows - i, idx)] = hSpace;

                        idx++;
                    }
                }
            }
            // final edits, set to invisible until further notice
            foreach ((int u, int a) k in resDict.Keys)
            {
                GameObject spc = resDict[k];
                SpaceController spcController = spc.AddComponent<SpaceController>();
                spcController.SetSpace(k.u, k.a);
                spcController.SetMaterials(HighlightMaterials[0]);
                if (!IsBlack(k)) spc.transform.localRotation = Quaternion.AngleAxis(-180, Vector3.up);
                spc.SetActive(false);
                Synchronizer.OnLerpComplete.AddListener(spcController.FlipHighlightLerp);
            }
            return resDict;
        }

        private List<(GameObject, (int, int), PieceType)> SetupBoard()
        {
            var pieces = new List<(GameObject g, (int up, int across) pos, PieceType type)>();
            // place players
            if (_pieceParent == null)
            {
                _pieceParent = new GameObject("BoardGame_Pieces").transform;
                _pieceParent.SetParent(transform.parent);
                _pieceParent.localPosition = Vector3.zero;
                _pieceParent.localRotation = Quaternion.identity;
            }

            pieces.Add(CreateAndPlacePiece(_pieceParent, (0, 0), PieceType.Blocker));
            pieces.Add(CreateAndPlacePiece(_pieceParent, (0, 1), PieceType.Antler));
            pieces.Add(CreateAndPlacePiece(_pieceParent, (0, 12), PieceType.Antler));
            pieces.Add(CreateAndPlacePiece(_pieceParent, (6, 7), PieceType.Eye));
            return pieces;
        }

        public void ResetBoard()
        {
            IsInitialized = false;
            // delete old game pieces before we lose track of them
            foreach (var p in Players)
            {
                Destroy(p.g);
            }
            Init();
        }

        private (GameObject, (int, int), PieceType) CreateAndPlacePiece(Transform parent, (int up, int across) pos, PieceType type)
        {
            GameObject piece = null;
            // instantiate prefab
            switch (type)
            {
                case PieceType.Blocker:
                    piece = Instantiate(BlockerPrefab, parent);
                    break;
                case PieceType.Antler:
                    piece = Instantiate(AntlerPrefab, parent);
                    break;
                case PieceType.Eye:
                    piece = Instantiate(EyePrefab, parent);
                    break;
                default:
                    Debug.LogWarning($"Invalid piece type {type}!");
                    break;
            }
            piece.SetActive(true);

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

            // set highlight materials - maybe move up near instantiating pieces? or if materials depend on type..
            GameObject highlight = piece.transform.Find("Highlighted").gameObject;
            for (int i = 0; i < highlight.transform.childCount; i++)
            {
                MeshRenderer highlightRenderer = highlight.transform.GetChild(i).GetComponent<MeshRenderer>();
                highlightRenderer.material.shader = HighlightShader;
                // each object is split up in bundle - prefabs contain partial meshes w one material per
                // whereas ingame (PieceHighlights) the pieces are single meshes w 1-4 materials
                if (type == PieceType.Antler)
                {
                    // antler's meshes are reordered for some reason so there isn't a fancy index-based way
                    // to make them look right (0,1 - grey 2,3 - glowy)
                    if (i >= 2) highlightRenderer.materials = new Material[] { HighlightMaterials[0] };
                    else highlightRenderer.materials = new Material[] { HighlightMaterials[1] };
                }
                else
                    highlightRenderer.materials = new Material[] { HighlightMaterials[(i + 1) % 2] };
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
            bool firstRow = pos.up == 0 && pos.up <= pos.across && pos.across < 2 * s_Rows - 1;
            return firstRow || 0 < pos.up && pos.up < s_Rows && pos.up <= pos.across && pos.across <= 2 * s_Rows - pos.up;
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
                for (int j = 0; j < Players.Count && !foundOccupied; j++)
                {
                    if (Players[j].pos == adj[i])
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
        public void ToggleSpaces(List<(int up, int across)> spaces, bool? setBeam = null)
        {
            foreach (var s in spaces)
            {
                SpaceController spc = SpaceDict[(s.up, s.across)].GetComponent<SpaceController>();
                // don't toggle beam spaces if we're not updating the beam!
                if (!spc.InBeam || setBeam != null)
                    spc.gameObject.SetActive(!spc.gameObject.activeSelf);
                // allow beam spaces to be toggled when parameter supplied
                if (setBeam != null)
                    spc.SetBeam((bool)setBeam);
            }
        }

        public void ToggleHighlight(GameObject piece)
        {
            // toggle parent transforms of normal/highlighted piece
            GameObject normal = piece.transform.Find("Normal").gameObject;
            GameObject highlight = piece.transform.Find("Highlighted").gameObject;
            normal.SetActive(!normal.activeSelf);
            highlight.SetActive(!highlight.activeSelf);
        }

        private bool MoveToSpace((GameObject obj, (int up, int across)? oldPos, PieceType type) piece, (int up, int across) newPos)
        {
            (int, int) BadPos = (99, 99);
            (int up, int across) oldPosNN = piece.oldPos ?? BadPos;
            // nullable arg for oldPos = first-time setup
            bool settingUp = oldPosNN.Item1 == BadPos.Item1;

            GameObject newSpc = SpaceDict[(newPos.up, newPos.across)];
            // check for valid position
            if (newSpc != null)
            {
                SpaceController newSpcController = newSpc.GetComponent<SpaceController>();
                // move/rotate piece, set controller occupants
                if (!settingUp)
                {
                    GameObject oldSpc = SpaceDict[(oldPosNN.up, oldPosNN.across)];
                    SpaceController oldSpcController = oldSpc.GetComponent<SpaceController>();
                    oldSpcController.SetOccupant(null);

                    Vector3 lookPos = newSpc.transform.localPosition - oldSpc.transform.localPosition;
                    // remove y component - only rotating in X/Z plane
                    lookPos.y = 0.0f;
                    Debug.DrawLine(newSpc.transform.localPosition, oldSpc.transform.localPosition, Color.white, 10);
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
            bool succeeded = MoveToSpace(Players[pIdx], newPos);
            if (succeeded)
            {
                Players[pIdx] = (Players[pIdx].g, newPos, Players[pIdx].type);
            }
        }

        public void UpdateBeam(bool clear = false)
        {
            if (clear)
            {
                ToggleSpaces(_beamSpaces, false);
            }
            else
            {
                // see who's been hit and remove
                var newBeamSpaces = new List<(int, int)>();
                (int u, int a) eyePos = Players.Where(p => p.type == PieceType.Eye).FirstOrDefault().pos;
                // list of flags to keep track of blocked beams
                bool[] blocked = { false, false, false };

                for (int i = 1; i < s_Rows; i++)
                {
                    var currDepthSpaces = new List<(int, int)>();
                    // check first row conditions
                    int lowerOffset() => eyePos.u - i == 0 ? 1 : 0;
                    int upperOffset() => eyePos.u + i == 1 ? 1 : 0;
                    // add spaces to list along 3 lines stretching from triangle vertices
                    if (IsBlack(eyePos))
                    {
                        // below
                        (int, int) below = (eyePos.u - i, eyePos.a - lowerOffset());
                        blocked[0] = IsBlocked(below, blocked[0]);
                        if (!blocked[0]) currDepthSpaces.Add(below);

                        // upper R diagonal
                        (int, int) upperR1 = (eyePos.u + i, eyePos.a + 3 * i - 1 + upperOffset());
                        (int, int) upperR2 = (eyePos.u + i, eyePos.a + 3 * i + upperOffset());
                        blocked[1] = IsBlocked(upperR1, blocked[1]);
                        if (!blocked[1])
                        {
                            currDepthSpaces.Add(upperR1);
                            blocked[1] = IsBlocked(upperR2, blocked[1]);
                            if (!blocked[1]) currDepthSpaces.Add(upperR2);
                        }

                        // upper L diagonal
                        (int, int) upperL1 = (eyePos.u + i, eyePos.a - 3 * i + 1 + upperOffset());
                        (int, int) upperL2 = (eyePos.u + i, eyePos.a - 3 * i + upperOffset());
                        blocked[2] = IsBlocked(upperL1, blocked[2]);
                        if (!blocked[2])
                        {
                            currDepthSpaces.Add(upperL1);
                            blocked[2] = IsBlocked(upperL2, blocked[2]);
                            if (!blocked[2]) currDepthSpaces.Add(upperL2);
                        }
                    }
                    else
                    {
                        // above
                        (int, int) above = (eyePos.u + i, eyePos.a + upperOffset());
                        blocked[0] = IsBlocked(above, blocked[0]);
                        if (!blocked[0]) currDepthSpaces.Add(above);

                        // lower R diagonal
                        (int, int) lowerR1 = (eyePos.u - i, eyePos.a + 3 * i - 1 - lowerOffset());
                        (int, int) lowerR2 = (eyePos.u - i, eyePos.a + 3 * i - lowerOffset());
                        blocked[1] = IsBlocked(lowerR1, blocked[1]);
                        if (!blocked[1])
                        {
                            currDepthSpaces.Add(lowerR1);
                            blocked[1] = IsBlocked(lowerR2, blocked[1]);
                            if (!blocked[1]) currDepthSpaces.Add(lowerR2);
                        }

                        // lower L diagonal
                        (int, int) lowerL1 = (eyePos.u - i, eyePos.a - 3 * i + 1 - lowerOffset());
                        (int, int) lowerL2 = (eyePos.u - i, eyePos.a - 3 * i - lowerOffset());
                        blocked[2] = IsBlocked(lowerL1, blocked[2]);
                        if (!blocked[2])
                        {
                            currDepthSpaces.Add(lowerL1);
                            blocked[2] = IsBlocked(lowerL2, blocked[2]);
                            if (!blocked[2]) currDepthSpaces.Add(lowerL2);
                        }
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
        }

        public List<int> CheckBeam()
        {
            // return a list of players by index to be removed
            var result = new List<int>();
            for (int i = 0; i < Players.Count; i++)
            {
                foreach ((int, int) spc in _beamSpaces)
                {
                    if (Players[i].pos == spc && Players[i].type != PieceType.Blocker)
                    {
                        Debug.Log($"player {Players[i].g.name} hit at {Players[i].pos}");
                        result.Add(i);
                    }
                }
            }
            return result;
        }

        public bool IsBlocked((int u, int a) pos, bool wasBlocked)
        {
            // basically just OR-ing any past blocks in so that
            // everything beyond the blocker piece is also shielded
            bool blocked = wasBlocked;
            foreach (var p in Players)
            {
                if (p.pos == pos && p.type == PieceType.Blocker)
                    blocked = true;
            }

            return blocked;
        }
    }
}
