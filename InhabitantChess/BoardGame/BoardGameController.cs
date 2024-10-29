using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace InhabitantChess.BoardGame
{
    public class BoardGameController : MonoBehaviour
    {
        public FirstPersonManipulator PlayerManip;
        public bool Playing { get; private set; }

        public delegate void PieceAudioEvent(int idx);
        public PieceAudioEvent OnPieceRemoved;
        public delegate void BoardGameEvent();
        public BoardGameEvent OnStartGame;
        public BoardGameEvent OnStopGame;

        private const float CPUTurnTime = 1.0f, DestroyDelay = 2.0f;

        private List<GameObject> _toDestroy;
        private List<(int, int)> _legalMoves;
        private BoardController _board;
        private BoardController.ChessPiece _currentPlayer;
        private SpaceController _focusedSpace;
        private float _destroyTime;
        private int _cpuIndex;
        private int _antlerCount, _gamesWon, _totalGames;
        private bool _reachedEye, _noLegalMoves, _movesHighlightEnabled, _pieceHighlightEnabled, _beamHighlightEnabled;

        private enum BoardState
        {
            WaitingForInput,
            InputReceived,
            DoneMoving,
            Idle,
        }
        private BoardState _boardState;

        private InhabitantChess InhabitantChess => InhabitantChess.Instance;

        private void Start()
        {
            _toDestroy = new();
            _boardState = BoardState.Idle;
            _board = transform.Find("BoardGame_Board").gameObject.GetComponent<BoardController>();

            InhabitantChess.OnConfigure += OnConfigure;
            OnConfigure();
        }

        private void OnDestroy()
        {
            InhabitantChess.OnConfigure -= OnConfigure;
        }

        private void Update()
        {
            if (!Playing) return;

            // delay destruction to let AudioSources finish playing
            if (_toDestroy.Count > 0 && Time.time >= _destroyTime + DestroyDelay)
            {
                foreach (var obj in _toDestroy) Destroy(obj);
                _toDestroy.Clear();
            }

            // check for user input - should probably add a prompt to show space under cursor
            if (_boardState == BoardState.WaitingForInput)
            {
                CastRay(OWInput.IsNewlyPressed(Controls.BoardMove, InputMode.All));
            }

            void CastRay(bool isInteract)
            {
                _focusedSpace = null;
                Transform manipTrans = PlayerManip.transform;
                if (Physics.Raycast(manipTrans.position, manipTrans.forward, out RaycastHit hit, 75f, OWLayerMask.blockableInteractMask))
                {
                    _focusedSpace = hit.collider.gameObject.GetComponent<SpaceController>();
                    if (_focusedSpace != null && _legalMoves.Contains(_focusedSpace.Position) && isInteract)
                    {
                        // allow PlayerTurn to proceed
                        _boardState = BoardState.InputReceived;
                    }
                }
            }
        }

        private void RefreshHighlighting()
        {
            if (_boardState == BoardState.WaitingForInput)
            {
                _board.SetSpaces(_legalMoves, new SpaceController.SpaceInfo(_movesHighlightEnabled, true, true));
                _board.SetPieceHighlight(_currentPlayer.gameObject, _pieceHighlightEnabled);
            }
            _board.UpdateBeam(_beamHighlightEnabled);
        }

        // loop controlling turns, game state
        private IEnumerator Play()
        {
            //int turnCount = 0;
            // turn on beam at start
            _board.UpdateBeam(_beamHighlightEnabled);
            OnStartGame?.Invoke();

            while (Playing)
            {
                for (int i = 0; i < _board.Pieces.Count && Playing; i++)
                {
                    if (_board.Pieces[i].type == PieceType.Eye)
                    {
                        StartCoroutine(CPUTurn(i));
                    }
                    else
                    {
                        StartCoroutine(PlayerTurn(i));
                    }
                    // wait until turn finishes
                    yield return new WaitUntil(() => _boardState == BoardState.Idle);
                    // delete pieces
                    var removed = _board.CheckBeam();
                    i = RemovePieces(removed, i);
                    Playing = !IsGameOver();
                }
                //Logger.Log($"Turn {++turnCount} complete");
            }

            _totalGames++;
            if (PlayerWon()) _gamesWon++;
            Util.Logger.Log($"Game finished, win ratio {GetScore().Item1} - {GetScore().Item2}");
            OnStopGame?.Invoke();
        }

        private IEnumerator PlayerTurn(int pIdx)
        {
            _currentPlayer = _board.Pieces[pIdx];
            _legalMoves = _board.LegalMoves(_currentPlayer);
            if (_legalMoves.Count == 0)
            {
                _noLegalMoves = true;
                _boardState = BoardState.Idle;
                yield break;
            }

            bool isBlocker = _currentPlayer.type == PieceType.Blocker;
            _board.SetSpaces(_legalMoves, new SpaceController.SpaceInfo(_movesHighlightEnabled, true, true, isBlocker));
            _board.SetPieceHighlight(_currentPlayer.gameObject, _pieceHighlightEnabled);
            // wait for input, then move
            _boardState = BoardState.WaitingForInput;
            yield return new WaitUntil(() => _boardState == BoardState.InputReceived);
            // move and wait for animation to finish
            _board.DoMove(pIdx, _focusedSpace.Position);
            yield return new WaitUntil(() => !_board.Moving);
            _boardState = BoardState.DoneMoving;
            // reset highlighting/visibility and finish
            _board.SetSpaces(_legalMoves, new SpaceController.SpaceInfo(false, false, false, false));
            _board.SetPieceHighlight(_currentPlayer.gameObject, false);
            _board.UpdateBeam(_beamHighlightEnabled);
            _boardState = BoardState.Idle;
        }

        private IEnumerator CPUTurn(int pIdx)
        {
            _currentPlayer = _board.Pieces[pIdx];
            _legalMoves = _board.LegalMoves(_currentPlayer);
            if (_legalMoves.Count == 0)
            {
                _noLegalMoves = true;
                _boardState = BoardState.Idle;
                yield break;
            }
            _board.SetPieceHighlight(_currentPlayer.gameObject, _pieceHighlightEnabled);
            // add artificial wait
            _boardState = BoardState.WaitingForInput;
            yield return new WaitForSeconds(CPUTurnTime);
            _boardState = BoardState.InputReceived;
            (int randU, int randA) = ChooseCPUMove(_legalMoves);
            SpaceController targetSpace = _board.Spaces[randU][randA];
            // move to space
            _board.DoMove(pIdx, targetSpace.Position);
            yield return new WaitUntil(() => !_board.Moving);
            _boardState = BoardState.DoneMoving;
            _board.UpdateBeam(_beamHighlightEnabled);
            // reset
            _board.SetPieceHighlight(_currentPlayer.gameObject, false);
            _boardState = BoardState.Idle;
        }

        private (int, int) ChooseCPUMove(List<(int, int)> legalMoves)
        {
            BoardController.ChessPiece cpuPiece = _board.Pieces[_cpuIndex];
            (int, int) newPos;
            // randomly choose a space
            do
                newPos = legalMoves[Random.Range(0, legalMoves.Count)];
            while ((cpuPiece.up, cpuPiece.across) == newPos);
            return newPos;
        }

        private bool IsGameOver()
        {
            var cpuAdjPositions = _board.LegalMoves(_board.Pieces[_cpuIndex], true);
            bool antlerAtEye = false;
            foreach ((int, int) adjPos in cpuAdjPositions)
            {
                antlerAtEye |= _board.Pieces.Any(piece => (piece.up, piece.across) == adjPos && piece.type == PieceType.Antler);
            }
            // completely blocked including at least one antler
            _reachedEye = _board.LegalMoves(_board.Pieces[_cpuIndex]).Count == 0 && antlerAtEye;

            return _reachedEye || _noLegalMoves || _antlerCount < 1;
        }

        public bool PlayerWon()
        {
            return _reachedEye;
        }

        public (int, int) GetScore()
        {
            return (_gamesWon, _totalGames - _gamesWon);
        }

        public BoardController.ChessPiece? GetCurrentPlayer()
        {
            return _currentPlayer.up != BoardController.ChessPiece.BadPosValue ? _currentPlayer : null;
        }

        public SpaceController GetPlayerFocusedSpace()
        {
            bool isFocusedLegal = _focusedSpace != null && _board.LegalMoves(_currentPlayer).Contains(_focusedSpace.Position);
            return _boardState == BoardState.WaitingForInput && isFocusedLegal ? _focusedSpace : null;
        }

        private int RemovePieces(List<int> Pieces, int currTurn)
        {
            int i = currTurn;
            foreach (int r in Pieces)
            {
                // replace piece w new deadwood
                var plyr = _board.Pieces[r];
                _board.Pieces.RemoveAt(r);
                if (plyr.type == PieceType.Antler) _antlerCount--;
                _board.AddDeadwood(plyr.type);
                // dec currTurn if removed piece would shift piece list index up 1
                // so we don't skip the next one in Play() loop
                if (r <= i) i--;
                // same for cpu index
                if (r <= _cpuIndex) _cpuIndex--;
                plyr.gameObject.transform.DestroyAllChildren();
                _toDestroy.Add(plyr.gameObject);
                OnPieceRemoved?.Invoke(r);
                //Debug.Log($"Removed {plyr.g.name}, i = {i}, list length {_board.Pieces.Count}");
            }
            _destroyTime = Time.time;
            return i;
        }

        public void OnPressInteract()
        {
            // does nothing if mid-game
            if (Playing) return;

            _board.ResetBoard();
            _antlerCount = _board.Pieces.Where(piece => piece.type == PieceType.Antler).Count();
            _noLegalMoves = _reachedEye = false;
            _cpuIndex = _board.Pieces.FindIndex(piece => piece.type == PieceType.Eye);
            Playing = true;
            StartCoroutine(Play());
        }

        public void OnConfigure()
        {
            _movesHighlightEnabled = InhabitantChess.HighlightSettings.moves;
            _pieceHighlightEnabled = InhabitantChess.HighlightSettings.pieces;
            _beamHighlightEnabled = InhabitantChess.HighlightSettings.beam;

            if (Playing) RefreshHighlighting();
        }
    }
}
