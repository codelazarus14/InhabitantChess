using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Logger = InhabitantChess.Util.Logger;

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

        public List<(GameObject g, (int up, int across) pos, PieceType type)> Pieces { get; private set; }
        // this may change in future bc it depends on world, not local space
        public Dictionary<(int up, int across), GameObject> SpaceDict { get; private set; }
        public bool IsInitialized { get; private set; }
        public bool Moving { get; private set; }

        public delegate void BoardAudioEvent(int idx);
        public BoardAudioEvent OnPieceFinishedMoving;
        public delegate void BoardEvent();
        public BoardEvent OnBoardInitialized;

        private static float s_triSize = 0.19346f;
        private static float s_triHeight = Mathf.Sqrt(3) / 2 * s_triSize;
        private static float[] s_boardLevels = { 0.051f, 0.083f, 0.115f };
        private static Vector3 s_wOffset = new Vector3(-0.05584711f, 0, 0.09673002f);
        private static Vector3 s_startingPos = new Vector3(0.3350971f, s_boardLevels[0], -0.58038f);
        private static int s_Rows = 7;
        private static ((int, int) pos, PieceType type)[] _startingPieces;

        private float _travelTime = 0.75f, _initMoveTime, _curveHeight = 0.33f;
        private GameObject _movingPiece;
        private Vector3 _startMovePos, _destMovePos;
        private Quaternion _startLookRot, _destLookRot;

        private int _deadwoodIdx, _movingPieceIdx;
        private Vector3 _deadwoodOffset = new Vector3(s_startingPos.x, 0, -s_startingPos.z + 0.25f);
        private GameObject[] _deadwood;
        private List<(int, int)> _beamSpaces;
        private Transform _spcParent, _pieceParent, _deadwoodParent;

        private void Update()
        {
            if (Moving)
            {
                float progress = (Time.time - _initMoveTime) / _travelTime;
                if (progress > 1)
                {
                    Moving = false;
                    OnPieceFinishedMoving?.Invoke(_movingPieceIdx);
                }
                else
                {
                    // from https://gamedev.stackexchange.com/questions/157642/moving-a-2d-object-along-circular-arc-between-two-points
                    Vector3 c = _startMovePos + (_destMovePos - _startMovePos) / 2 + Vector3.up * _curveHeight;

                    Vector3 m1 = Vector3.Lerp(_startMovePos, c, progress);
                    Vector3 m2 = Vector3.Lerp(c, _destMovePos, progress);
                    _movingPiece.transform.localPosition = Vector3.Lerp(m1, m2, progress);
                    _movingPiece.transform.localRotation = Quaternion.Slerp(_startLookRot, _destLookRot, progress);
                }
            }
        }

        public void Init()
        {
            _startingPieces = new[]
            {
                ((0, 0), PieceType.Blocker),
                ((0, 12), PieceType.Blocker),
                ((2, 4), PieceType.Antler),
                ((2, 10), PieceType.Antler),
                ((6, 7), PieceType.Eye)
            };

            GenerateBoard();
            SetupPieces(_pieceParent == null);

            _beamSpaces = new List<(int, int)>();
            _deadwood = new GameObject[Pieces.Count];
            _deadwoodIdx = 0;
            IsInitialized = true;
            OnBoardInitialized?.Invoke();
        }

        private void GenerateBoard()
        {
            if (SpaceDict == null)
            {
                SpaceDict = new Dictionary<(int up, int across), GameObject>();

                _spcParent = new GameObject("BoardGame_Spaces").transform;
                _spcParent.SetParent(transform.parent);
                _spcParent.localPosition = Vector3.zero;
                _spcParent.localRotation = Quaternion.identity;

                for (int i = s_Rows; i > 0; i--)
                {
                    // i over # of rows, j over # of B spaces per row
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
                            SpaceDict[(s_Rows - i, idx)] = hSpace;

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
                        SpaceDict[(s_Rows - i, idx)] = hSpace;

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
                            SpaceDict[(s_Rows - i, idx)] = hSpace;

                            idx++;
                        }
                    }
                }
                // finish init, default to inactive
                foreach ((int u, int a) k in SpaceDict.Keys)
                {
                    GameObject spc = SpaceDict[k];
                    SpaceController spcController = spc.AddComponent<SpaceController>();
                    spcController.SetSpace(k.u, k.a);
                    spcController.SetMaterials(HighlightMaterials[0]);
                    if (!IsBlack(k)) spc.transform.localRotation = Quaternion.AngleAxis(-180, Vector3.up);
                    spc.SetActive(false);
                    Synchronizer.OnLerpComplete.AddListener(spcController.FlipHighlightLerp);
                }
            }
        }

        private void SetupPieces(bool newParents = false)
        {
            Pieces = new List<(GameObject g, (int up, int across) pos, PieceType type)>();
            if (newParents)
            {
                _pieceParent = new GameObject("BoardGame_Pieces").transform;
                _pieceParent.SetParent(transform.parent);
                _pieceParent.localPosition = Vector3.zero;
                _pieceParent.localRotation = Quaternion.identity;

                _deadwoodParent = new GameObject("BoardGame_Deadwood").transform;
                _deadwoodParent.SetParent(transform.parent);
                _deadwoodParent.localPosition = _deadwoodOffset;
                _deadwoodParent.localRotation = Quaternion.identity;
            }

            foreach (var (pos, type) in _startingPieces)
            {
                CreateAndPlacePiece(_pieceParent, pos, type);
            }
        }

        private GameObject InstantiatePiece(PieceType type, Transform parent)
        {
            return type switch
            {
                PieceType.Blocker =>
                    Instantiate(BlockerPrefab, parent),
                PieceType.Antler =>
                    Instantiate(AntlerPrefab, parent),
                PieceType.Eye =>
                    Instantiate(EyePrefab, parent),
                _ => throw new System.ArgumentOutOfRangeException(nameof(type), "invalid PieceType")
            };
        }

        private void CreateAndPlacePiece(Transform parent, (int up, int across) pos, PieceType type)
        {
            GameObject pieceObj = InstantiatePiece(type, parent);
            pieceObj.SetActive(true);

            // fix rotation from prefab
            ChildRotationFix(pieceObj, type);

            // create placeholder pos
            (int, int) tempPos = (99, 99);
            (GameObject, (int, int), PieceType) pieceTemp = (pieceObj, tempPos, type);
            Pieces.Add(pieceTemp);
            // update w starting pos
            DoMove(Pieces.Count - 1, pos, true);

            // set highlight materials depending on type
            GameObject highlight = pieceObj.transform.Find("Highlighted").gameObject;
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

        public void ResetBoard()
        {
            IsInitialized = false;
            UpdateBeam(true, true);
            // delete old game pieces before we lose track of them
            foreach (var p in Pieces)
            {
                Destroy(p.g);
            }
            foreach (var d in _deadwood)
            {
                Destroy(d);
            }
            Init();
        }

        public List<(int, int)> LegalMoves((int u, int a) pos, PieceType type, bool ignoreOccupied = false)
        {
            return GetAdjacent(pos.u, pos.a, ignoreOccupied);
        }

        // return list of adjacent positions to (up, across)
        private List<(int, int)> GetAdjacent(int up, int across, bool ignoreOccupied)
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

            // filter out occupied spaces
            if (!ignoreOccupied)
            {
                for (int i = 0; i < adj.Count; i++)
                {
                    bool foundOccupied = false;
                    for (int j = 0; j < Pieces.Count && !foundOccupied; j++)
                    {
                        if (Pieces[j].pos == adj[i])
                        {
                            adj.RemoveAt(i);
                            i--;
                            foundOccupied = true;
                        }
                    }
                }
            }
            return adj;
        }

        // determine B/W based on coords
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

        // set space clickability/highlight visibility
        public void ToggleSpaces(List<(int up, int across)> spaces, bool visible, bool? setBeam = null)
        {
            foreach (var s in spaces)
            {
                SpaceController spc = SpaceDict[(s.up, s.across)].GetComponent<SpaceController>();
                // don't toggle beam spaces if we're not updating the beam!
                if (!spc.InBeam || setBeam != null)
                {
                    spc.SetVisible(visible);
                    spc.gameObject.SetActive(!spc.gameObject.activeSelf);
                }
                // allow beam spaces to be toggled when parameter supplied
                if (setBeam != null)
                    spc.SetBeam((bool)setBeam);
            }
        }

        public void ToggleHighlight(GameObject piece, bool highlightEnabled)
        {
            // toggle parent transforms of normal/highlighted piece
            GameObject normal = piece.transform.Find("Normal").gameObject;
            GameObject highlight = piece.transform.Find("Highlighted").gameObject;
            if (highlightEnabled)
            {
                normal.SetActive(!normal.activeSelf);
                highlight.SetActive(!highlight.activeSelf);
            }
            else
            {
                normal.SetActive(true);
                highlight.SetActive(false);
            }
        }

        public void DoMove(int pIdx, (int up, int across) newPos, bool settingUp = false)
        {
            var piece = Pieces[pIdx];
            Pieces[pIdx] = (piece.g, newPos, piece.type);
            GameObject newSpc = SpaceDict[(newPos.up, newPos.across)];
            // move/rotate piece
            if (!settingUp)
            {
                Moving = true;
                _movingPiece = piece.g;
                _movingPieceIdx = pIdx;

                GameObject oldSpc = SpaceDict[(piece.pos.up, piece.pos.across)];

                // set up values to lerp between in Update()
                _startMovePos = oldSpc.transform.localPosition;
                _destMovePos = newSpc.transform.localPosition;
                _startLookRot = piece.g.transform.localRotation;
                Vector3 lookPos = newSpc.transform.localPosition - oldSpc.transform.localPosition;
                // remove y component - only rotating in X/Z plane
                lookPos.y = 0.0f;
                _destLookRot = Quaternion.LookRotation(lookPos);
                _initMoveTime = Time.time;
            }
            else
            {
                piece.g.transform.localPosition = newSpc.transform.localPosition;
                if (IsBlack(newPos))
                {
                    piece.g.transform.localRotation = Quaternion.AngleAxis(-30, Vector3.up);
                }
                else
                {
                    piece.g.transform.localRotation = Quaternion.AngleAxis(-90, Vector3.up);
                }
            }
        }

        public void UpdateBeam(bool visible, bool clear = false)
        {
            // reset (turn off) old spaces
            ToggleSpaces(_beamSpaces, visible, false);

            if (!clear)
            {
                // see who's been hit and remove
                var newBeamSpaces = new List<(int, int)>();
                (int u, int a) eyePos = Pieces.Where(p => p.type == PieceType.Eye).FirstOrDefault().pos;
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
                    var currInBounds = currDepthSpaces.Where(InBounds);
                    newBeamSpaces.AddRange(currInBounds.ToList());
                }

                // show new ones
                _beamSpaces = newBeamSpaces;
                ToggleSpaces(_beamSpaces, visible, true);
            }
        }

        public List<int> CheckBeam()
        {
            // return a list of players by index to be removed
            var result = new List<int>();
            for (int i = 0; i < Pieces.Count; i++)
            {
                foreach ((int, int) spc in _beamSpaces)
                {
                    if (Pieces[i].pos == spc && Pieces[i].type != PieceType.Blocker)
                    {
                        Logger.Log($"Piece {Pieces[i].g.name} hit at {Pieces[i].pos}");
                        result.Add(i);
                    }
                }
            }
            return result;
        }

        private bool IsBlocked((int u, int a) pos, bool wasBlocked)
        {
            // basically just check any pieces past blocker so that
            // everything behind it is shielded
            bool blocked = wasBlocked;
            foreach (var p in Pieces)
            {
                if (p.pos == pos && p.type == PieceType.Blocker)
                    blocked = true;
            }

            return blocked;
        }

        public void AddDeadwood(PieceType type)
        {
            GameObject newDeadwood = InstantiatePiece(type, _deadwoodParent);

            // create deadwood, update array
            Vector3 deadwoodPos = new Vector3(s_triHeight, 0, s_triSize / 2) * 0.8f;
            newDeadwood.transform.localPosition -= _deadwoodIdx * deadwoodPos;
            newDeadwood.SetActive(true);
            _deadwood[_deadwoodIdx] = newDeadwood;
            _deadwoodIdx++;
        }
    }
}
