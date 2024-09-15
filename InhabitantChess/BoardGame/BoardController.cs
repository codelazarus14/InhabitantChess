using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace InhabitantChess.BoardGame
{
    public class BoardController : MonoBehaviour
    {
        public struct ChessPiece(GameObject g, PieceType p, int u = ChessPiece.BadPosValue, int a = ChessPiece.BadPosValue)
        {
            public const int BadPosValue = 99;
            public GameObject gameObject = g;
            public PieceType type = p;
            public int up = u;
            public int across = a;
        }

        public List<ChessPiece> Pieces { get; private set; }
        public SpaceController[][] Spaces { get; private set; }
        public bool Moving { get; private set; }

        public delegate void BoardAudioEvent(int idx);
        public BoardAudioEvent OnPieceFinishedMoving;
        public delegate void BoardEvent();
        public BoardEvent OnBoardReset;

        private static float s_triSize = 0.19346f;
        private static float s_triHeight = Mathf.Sqrt(3) / 2 * s_triSize;
        private static float[] s_boardLevels = { 0.051f, 0.083f, 0.115f };
        private static Vector3 s_wOffset = new Vector3(-0.05584711f, 0, 0.09673002f);
        private static Vector3 s_startingPos = new Vector3(0.3350971f, 0, -0.58038f);
        private static int s_Rows = 7;

        private static (PieceType type, (int up, int across))[] s_startingPieces =
        [
            (PieceType.Blocker, (0, 0)),
            (PieceType.Blocker, (0, 12)),
            (PieceType.Antler,  (2, 2)),
            (PieceType.Antler,  (2, 8)),
            (PieceType.Eye,     (6, 1))
        ];

        private List<(int, int)> _beamPositions;
        private Material[] _highlightMaterials;
        private GameObject _spacePrefab;
        private GameObject _blockerPrefab;
        private GameObject _antlerPrefab;
        private GameObject _eyePrefab;
        private Transform _spcParent, _pieceParent, _deadwoodParent;

        private GameObject _movingPiece;
        private Vector3 _startMovePos, _destMovePos;
        private Quaternion _startLookRot, _destLookRot;
        private float _travelTime = 0.75f, _initMoveTime, _curveHeight = 0.33f;

        private GameObject[] _deadwood;
        private Vector3 _deadwoodOffset = new Vector3(s_startingPos.x, 0, -s_startingPos.z + 0.25f);
        private int _deadwoodIdx, _movingPieceIdx;

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

        public void Init(GameObject spacePrefab, GameObject blockerPrefab, GameObject antlerPrefab, GameObject eyePrefab, Material[] highlightMaterials)
        {
            _spacePrefab = spacePrefab;
            _blockerPrefab = blockerPrefab;
            _antlerPrefab = antlerPrefab;
            _eyePrefab = eyePrefab;
            _highlightMaterials = highlightMaterials;
        }

        public void ResetBoard()
        {
            // clear beam spaces
            UpdateBeam(false, true);

            GenerateBoard();
            SetupPieces();

            OnBoardReset?.Invoke();
        }

        private void GenerateBoard()
        {
            void CreateAndSetSpace(Vector3 localPos, int up, int across, ref List<(int, int)> posnsList)
            {
                SpaceController space = Instantiate(_spacePrefab, _spcParent).AddComponent<SpaceController>();
                space.transform.localPosition = localPos;
                space.transform.localRotation = IsBlack(up, across) ? Quaternion.identity : Quaternion.AngleAxis(180, Vector3.up);
                space.gameObject.SetActive(true);
                space.SetPosition(up, across);
                space.SetMaterials(_highlightMaterials[0]);

                Spaces[up][across] = space;
                posnsList.Add((up, across));
            }

            // skip if board already exists
            if (Spaces != null) return;

            Spaces = new SpaceController[s_Rows][];
            List<(int, int)> spacePosns = [];

            _spcParent = new GameObject("BoardGame_Spaces").transform;
            _spcParent.SetParent(transform.parent);
            _spcParent.localPosition = Vector3.zero;
            _spcParent.localRotation = Quaternion.identity;

            // i over # of rows, j over # of B spaces per row
            for (int i = 0; i < s_Rows; i++)
            {
                // first row is missing corners - unique row size
                Spaces[i] = new SpaceController[(s_Rows - i) * 2 + (i == 0 ? -1 : 1)];
                // init world position for prefab with only vertical (row) offset added
                Vector3 worldPos = s_startingPos + Vector3.right * (-s_triHeight * i);
                float rowOffset = i * s_triSize / 2;
                int spaceCount = 0;

                for (int j = 0; j < s_Rows - i; j++)
                {
                    // add horizontal offset to get center of new B space
                    worldPos.z = s_startingPos.z + rowOffset + j * s_triSize;

                    // add W space to left edge, except for the first row's left corner
                    if (i > 0 && j == 0)
                    {
                        // add negative position offsets for white space to current pos
                        // edge spaces are always at lowest elevation
                        Vector3 wLeftEdgePos = worldPos + new Vector3(-s_wOffset.x, s_boardLevels[0], -s_wOffset.z);
                        CreateAndSetSpace(wLeftEdgePos, i, spaceCount++, ref spacePosns);
                    }

                    // default to lowest elevation/height above the board's surface
                    float elevation = s_boardLevels[0];
                    // determine elevation based on position
                    // for i [0, 6], j [0, s_Rows - i - 1]:
                    // i = 2, j = 2 (halfway of s_Rows - 2 - 1 = [0, 4]) - level 2
                    // i [1, 5], j [1, 2-4] - level 1
                    if (1 < i && i < s_Rows - 4 && 1 < j && j < s_Rows - i - 2) elevation = s_boardLevels[2];
                    else if (0 < i && i < s_Rows - 1 && 0 < j && j < s_Rows - i - 1) elevation = s_boardLevels[1];

                    Vector3 bSpacePos = worldPos + Vector3.up * elevation;
                    // add B space
                    CreateAndSetSpace(bSpacePos, i, spaceCount++, ref spacePosns);

                    // add W space to right of every B space, except for first row's right corner
                    if (i > 0 || j < s_Rows - i - 1)
                    {
                        // for i [0, 6], j[0, s_Rows - i - 1]
                        // i [2, 3], j [1, 2] - level 2
                        // i [1, 5], j [0, 1-4] - level 1
                        if (1 < i && i < s_Rows - 3 && 0 < j && j < s_Rows - i - 2) elevation = s_boardLevels[2];
                        else if (0 < i && i < s_Rows - 1 && 0 <= j && j < s_Rows - i - 1) elevation = s_boardLevels[1];
                        else elevation = s_boardLevels[0];

                        // add white position offset and elevation
                        Vector3 wSpacePos = worldPos + new Vector3(-s_wOffset.x, elevation, s_wOffset.z);
                        CreateAndSetSpace(wSpacePos, i, spaceCount++, ref spacePosns);
                    }
                }
            }
            // finish init, default to inactive
            SetSpaces(spacePosns, false, false);
        }

        private void SetupPieces()
        {
            // delete old game pieces/deadwood
            if (Pieces != null)
                foreach (var p in Pieces) Destroy(p.gameObject);
            if (_deadwood != null)
                foreach (var d in _deadwood) Destroy(d);

            Pieces = new List<ChessPiece>();

            if (_pieceParent == null)
            {
                _pieceParent = new GameObject("BoardGame_Pieces").transform;
                _pieceParent.SetParent(transform.parent);
                _pieceParent.localPosition = Vector3.zero;
                _pieceParent.localRotation = Quaternion.identity;
            }

            if (_deadwoodParent == null)
            {
                _deadwoodParent = new GameObject("BoardGame_Deadwood").transform;
                _deadwoodParent.SetParent(transform.parent);
                _deadwoodParent.localPosition = _deadwoodOffset;
                _deadwoodParent.localRotation = Quaternion.identity;
            }

            foreach (var (type, pos) in s_startingPieces)
                CreateAndPlacePiece(_pieceParent, type, pos);

            _deadwood = new GameObject[Pieces.Count];
            _deadwoodIdx = 0;
        }

        private GameObject InstantiatePiece(PieceType type, Transform parent)
        {
            return type switch
            {
                PieceType.Blocker =>
                    Instantiate(_blockerPrefab, parent),
                PieceType.Antler =>
                    Instantiate(_antlerPrefab, parent),
                PieceType.Eye =>
                    Instantiate(_eyePrefab, parent),
                _ => throw new System.ArgumentOutOfRangeException(nameof(type), "invalid PieceType")
            };
        }

        private void CreateAndPlacePiece(Transform parent, PieceType type, (int up, int across) pos)
        {
            void ChildRotationFix(GameObject g, PieceType type)
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

            GameObject pieceObj = InstantiatePiece(type, parent);
            pieceObj.SetActive(true);

            // fix rotation from prefab
            ChildRotationFix(pieceObj, type);

            ChessPiece pieceTemp = new ChessPiece(pieceObj, type);
            Pieces.Add(pieceTemp);
            // update w starting pos
            DoMove(Pieces.Count - 1, pos, true);

            // set highlight materials depending on type
            GameObject highlight = pieceObj.transform.Find("Highlighted").gameObject;
            for (int i = 0; i < highlight.transform.childCount; i++)
            {
                MeshRenderer highlightRenderer = highlight.transform.GetChild(i).GetComponent<MeshRenderer>();
                // each object is split up in bundle - prefabs contain partial meshes w one material per
                // whereas ingame (PieceHighlights) the pieces are single meshes w 1-4 materials
                if (type == PieceType.Antler)
                {
                    // antler's meshes are reordered for some reason so there isn't a fancy index-based way
                    // to make them look right (0,1 - grey 2,3 - glowy)
                    if (i >= 2) highlightRenderer.sharedMaterials = [_highlightMaterials[0]];
                    else highlightRenderer.sharedMaterials = [_highlightMaterials[1]];
                }
                else
                    highlightRenderer.sharedMaterials = [_highlightMaterials[(i + 1) % 2]];
            }
        }

        public List<(int, int)> LegalMoves(ChessPiece piece, bool ignoreOccupied = false)
        {
            return GetAdjacent(piece.up, piece.across, ignoreOccupied);
        }

        // return list of adjacent positions to (up, across)
        private List<(int, int)> GetAdjacent(int up, int across, bool ignoreOccupied)
        {
            var adj = new List<(int, int)>();

            (int u, int a) left = (up, across - 1);
            if (InBounds(left)) adj.Add(left);

            (int u, int a) right = (up, across + 1);
            if (InBounds(right)) adj.Add(right);

            // B has upper face, W has lower face
            (int u, int a) upOrDown;
            if (IsBlack(up, across))
            {
                if (up == 0) upOrDown = (up + 1, across);
                else upOrDown = (up + 1, across - 1);
            }
            else
            {
                if (up == 1) upOrDown = (up - 1, across);
                else upOrDown = (up - 1, across + 1);
            }
            if (InBounds(upOrDown)) adj.Add(upOrDown);

            // filter out occupied spaces
            if (!ignoreOccupied)
            {
                for (int i = 0; i < adj.Count; i++)
                {
                    for (int j = 0; j < Pieces.Count; j++)
                    {
                        if ((Pieces[j].up, Pieces[j].across) == adj[i])
                        {
                            adj.RemoveAt(i--);
                            break;
                        }
                    }
                }
            }
            return adj;
        }

        // determine B/W based on coords
        private bool IsBlack(int up, int across)
        {
            bool isOdd = across % 2 == 1;
            bool firstRow = up == 0;
            // black is only even on first row
            return (firstRow && !isOdd) || (!firstRow && isOdd);
        }

        private bool IsBlack((int up, int across) pos)
        {
            return IsBlack(pos.up, pos.across);
        }

        // check if coord pos is in bounds
        private bool InBounds((int up, int across) pos)
        {
            // check if up/row in bounds, and across in bounds within row
            if (pos.up < 0 || Spaces.Length <= pos.up) return false;
            return 0 <= pos.across && pos.across < Spaces[pos.up].Length;
        }

        // set properties across multiple spaces (clickable, visible, is in beam)
        public void SetSpaces(IEnumerable<(int up, int across)> posns, bool isVisible, bool isInteractive, bool? isHighlightedMove = null, bool? inBeam = null)
        {
            foreach (var pos in posns)
            {
                SpaceController spc = Spaces[pos.up][pos.across];

                spc.SetVisible(isVisible);
                spc.SetInteractive(isInteractive);
                if (inBeam.HasValue)
                    spc.SetInBeam(inBeam.Value);
                if (isHighlightedMove.HasValue)
                    spc.SetHighlightedMove(isHighlightedMove.Value);
            }
        }

        public void SetPieceHighlight(GameObject piece, bool highlighted)
        {
            // toggle parent transforms of normal/highlighted piece
            GameObject normal = piece.transform.Find("Normal").gameObject;
            GameObject highlight = piece.transform.Find("Highlighted").gameObject;
            normal.SetActive(!highlighted);
            highlight.SetActive(highlighted);
        }

        public void DoMove(int pIdx, (int up, int across) newPos, bool settingUp = false)
        {
            var piece = Pieces[pIdx];
            Pieces[pIdx] = new ChessPiece(piece.gameObject, piece.type, newPos.up, newPos.across);
            GameObject newSpc = Spaces[newPos.up][newPos.across].gameObject;
            // move/rotate piece
            if (!settingUp)
            {
                Moving = true;
                _movingPiece = piece.gameObject;
                _movingPieceIdx = pIdx;

                GameObject oldSpc = Spaces[piece.up][piece.across].gameObject;

                // set up values to lerp between in Update()
                _startMovePos = oldSpc.transform.localPosition;
                _destMovePos = newSpc.transform.localPosition;
                _startLookRot = piece.gameObject.transform.localRotation;
                Vector3 lookPos = newSpc.transform.localPosition - oldSpc.transform.localPosition;
                // remove y component - only rotating in X/Z plane
                lookPos.y = 0.0f;
                _destLookRot = Quaternion.LookRotation(lookPos);
                _initMoveTime = Time.time;
            }
            else
            {
                piece.gameObject.transform.localPosition = newSpc.transform.localPosition;
                piece.gameObject.transform.localRotation = IsBlack(newPos) ? Quaternion.AngleAxis(-30, Vector3.up) : Quaternion.AngleAxis(-90, Vector3.up);
            }
        }

        public void UpdateBeam(bool visible, bool clearBeam = false)
        {
            void UpdateBlockedFlag((int u, int a) pos, ref bool blocked)
            {
                // update flag if potential beam position matches that of a blocker
                foreach (var p in Pieces)
                {
                    if (p.up == pos.u && p.across == pos.a && p.type == PieceType.Blocker)
                        blocked = true;
                }
            }

            List<(int, int)> newBeamPosns = [];

            // reset (turn off) old spaces
            if (_beamPositions != null)
                SetSpaces(_beamPositions, false, false, false, false);

            // trace beam outward from eye piece
            if (!clearBeam)
            {
                ChessPiece eye = Pieces.Where(p => p.type == PieceType.Eye).FirstOrDefault();
                // list of flags to keep track of blocked beams
                bool[] blocked = [false, false, false];
                bool isBlack = IsBlack(eye.up, eye.across);

                for (int i = 1; i < s_Rows; i++)
                {
                    var currDepthSpaces = new List<(int, int)>();
                    // check first row conditions
                    int upperOffset() => eye.up + i == 1 ? 1 : 0;
                    int lowerOffset() => eye.up - i == 0 ? -1 : 0;

                    // add spaces to list along 3 lines stretching from triangle vertices
                    if (isBlack)
                    {
                        // below
                        (int, int) below = (eye.up - i, eye.across + i + lowerOffset());
                        UpdateBlockedFlag(below, ref blocked[0]);
                        if (!blocked[0]) currDepthSpaces.Add(below);

                        // upper R diagonal
                        (int, int) upperR1 = (eye.up + i, eye.across + 2 * i - 1 + upperOffset());
                        (int, int) upperR2 = (eye.up + i, eye.across + 2 * i + upperOffset());
                        UpdateBlockedFlag(upperR1, ref blocked[1]);
                        if (!blocked[1])
                        {
                            currDepthSpaces.Add(upperR1);
                            UpdateBlockedFlag(upperR2, ref blocked[1]);
                            if (!blocked[1]) currDepthSpaces.Add(upperR2);
                        }

                        // upper L diagonal
                        (int, int) upperL1 = (eye.up + i, eye.across - 4 * i + 1 + upperOffset());
                        (int, int) upperL2 = (eye.up + i, eye.across - 4 * i + upperOffset());
                        UpdateBlockedFlag(upperL1, ref blocked[2]);
                        if (!blocked[2])
                        {
                            currDepthSpaces.Add(upperL1);
                            UpdateBlockedFlag(upperL2, ref blocked[2]);
                            if (!blocked[2]) currDepthSpaces.Add(upperL2);
                        }
                    }
                    else
                    {
                        // above
                        (int, int) above = (eye.up + i, eye.across - i + upperOffset());
                        UpdateBlockedFlag(above, ref blocked[0]);
                        if (!blocked[0]) currDepthSpaces.Add(above);

                        // lower R diagonal
                        (int, int) lowerR1 = (eye.up - i, eye.across + 4 * i - 1 + lowerOffset());
                        (int, int) lowerR2 = (eye.up - i, eye.across + 4 * i + lowerOffset());
                        UpdateBlockedFlag(lowerR1, ref blocked[1]);
                        if (!blocked[1])
                        {
                            currDepthSpaces.Add(lowerR1);
                            UpdateBlockedFlag(lowerR2, ref blocked[1]);
                            if (!blocked[1]) currDepthSpaces.Add(lowerR2);
                        }

                        // lower L diagonal
                        (int, int) lowerL1 = (eye.up - i, eye.across - 2 * i + 1 + lowerOffset());
                        (int, int) lowerL2 = (eye.up - i, eye.across - 2 * i + lowerOffset());
                        UpdateBlockedFlag(lowerL1, ref blocked[2]);
                        if (!blocked[2])
                        {
                            currDepthSpaces.Add(lowerL1);
                            UpdateBlockedFlag(lowerL2, ref blocked[2]);
                            if (!blocked[2]) currDepthSpaces.Add(lowerL2);
                        }
                    }
                    // filter out-of-bounds
                    var currInBounds = currDepthSpaces.Where(InBounds);
                    newBeamPosns.AddRange(currInBounds.ToList());
                }
            }

            // show new ones
            _beamPositions = newBeamPosns;
            SetSpaces(_beamPositions, visible, true, false, true);
        }

        public List<int> CheckBeam()
        {
            // return a list of players by index to be removed
            var result = new List<int>();
            for (int i = 0; i < Pieces.Count; i++)
            {
                foreach ((int, int) pos in _beamPositions)
                {
                    if ((Pieces[i].up, Pieces[i].across) == pos && Pieces[i].type != PieceType.Blocker)
                    {
                        //Logger.Log($"Piece {Pieces[i].g.name} hit at {Pieces[i].pos}");
                        result.Add(i);
                    }
                }
            }
            return result;
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
