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
        public delegate void BoardGameAudioEvent();
        public BoardGameAudioEvent OnStartGame;
        public BoardGameAudioEvent OnStopGame;

        private const float CPUTurnTime = 1.0f, DestroyDelay = 2.0f;
        private static (int, int) s_BadPos = (99, 99);

        private List<GameObject> _toDestroy;
        private List<(int, int)> _legalMoves;
        private BoardController _board;
        private BoardController.ChessPiece _currentPlayer;
        private SpaceController _selectedSpace;
        private (int u, int a) _currCPUPos;
        private float _destroyTime;
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
            if (_boardState == BoardState.WaitingForInput && OWInput.IsNewlyPressed(InputLibrary.interact, InputMode.All))
            {
                CastRay();
            }

            void CastRay()
            {
                Transform manipTrans = PlayerManip.transform;
                RaycastHit hit;
                if (Physics.Raycast(manipTrans.position, manipTrans.forward, out hit, 75f, OWLayerMask.blockableInteractMask))
                {
                    SpaceController hitSpc = hit.collider.gameObject.GetComponent<SpaceController>();
                    if (hitSpc != null)
                    {
                        // allow PlayerTurn to proceed
                        _boardState = BoardState.InputReceived;
                        _selectedSpace = hitSpc;
                    }
                }
            }
        }

        private void RefreshHighlighting()
        {
            if (_boardState == BoardState.WaitingForInput)
            {
                _board.SetSpaces(_legalMoves, _movesHighlightEnabled, true);
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

            OnStopGame?.Invoke();
            _totalGames++;
            if (PlayerWon()) _gamesWon++;
            Util.Logger.Log($"Game finished, win ratio {GetScore().Item1} - {GetScore().Item2}");
        }

        private IEnumerator PlayerTurn(int pIdx)
        {
            _currentPlayer = _board.Pieces[pIdx];
            // fixed bug - reusing this variable without clearing it causes waiting for input loop to be skipped
            // if the previous piece's selected space was also legal (adjacent pieces)
            _selectedSpace = null;
            _legalMoves = _board.LegalMoves(_currentPlayer.type, _currentPlayer.up, _currentPlayer.across);
            if (_legalMoves.Count == 0)
            {
                _noLegalMoves = true;
                _boardState = BoardState.Idle;
                yield break;
            }

            // TODO: visualize legal moves that are possibly dangerous (walking into beam) when beam visual is turned off?
            _board.SetSpaces(_legalMoves, _movesHighlightEnabled, true);
            _board.SetPieceHighlight(_currentPlayer.gameObject, _pieceHighlightEnabled);
            // wait for input, then move
            while (_selectedSpace == null || !_legalMoves.Contains(_selectedSpace.Position))
            {
                _boardState = BoardState.WaitingForInput;
                yield return new WaitUntil(() => _boardState == BoardState.InputReceived);
            }
            // we're ready to move
            _board.DoMove(pIdx, _selectedSpace.Position);
            yield return new WaitUntil(() => !_board.Moving);
            _boardState = BoardState.DoneMoving;
            // reset highlighting/visibility and finish
            _board.SetSpaces(_legalMoves, false, false);
            _board.SetPieceHighlight(_currentPlayer.gameObject, false);
            // blocker piece should update beam on move
            if (_currentPlayer.type == PieceType.Blocker)
            {
                _board.UpdateBeam(_beamHighlightEnabled);
            }
            _boardState = BoardState.Idle;
        }

        private IEnumerator CPUTurn(int pIdx)
        {
            _currentPlayer = _board.Pieces[pIdx];
            _legalMoves = _board.LegalMoves(_currentPlayer.type, _currentPlayer.up, _currentPlayer.across);
            if (_legalMoves.Count == 0)
            {
                _noLegalMoves = true;
                _boardState = BoardState.Idle;
                yield break;
            }
            _board.SetPieceHighlight(_currentPlayer.gameObject, _pieceHighlightEnabled);
            // add artificial wait
            _boardState = BoardState.WaitingForInput;
            yield return new WaitForSecondsRealtime(CPUTurnTime);
            _boardState = BoardState.InputReceived;
            (int randU, int randA) = ChooseCPUMove(_legalMoves);
            _selectedSpace = _board.Spaces[randU][randA];
            // move to space
            _board.DoMove(pIdx, _selectedSpace.Position);
            yield return new WaitUntil(() => !_board.Moving);
            _boardState = BoardState.DoneMoving;
            _board.UpdateBeam(_beamHighlightEnabled);
            // reset
            _board.SetPieceHighlight(_currentPlayer.gameObject, false);
            _boardState = BoardState.Idle;
        }

        private (int, int) ChooseCPUMove(List<(int, int)> legalMoves)
        {
            // randomly choose a space
            (int, int) newPos = legalMoves[Random.Range(0, legalMoves.Count)];
            // roll twice if we get a repeated position
            if (_currCPUPos == newPos) newPos = legalMoves[Random.Range(0, legalMoves.Count)];
            _currCPUPos = newPos;
            return newPos;
        }

        private bool IsGameOver()
        {
            var cpuAdjPositions = _board.LegalMoves(PieceType.Eye, _currCPUPos.u, _currCPUPos.a, true);
            bool antlerAtEye = false;
            foreach (var (up, across) in cpuAdjPositions)
            {
                antlerAtEye |= _board.Pieces.Any(piece => piece.up == up && piece.across == across && piece.type == PieceType.Antler);
            }
            // completely blocked including at least one antler
            _reachedEye = _board.LegalMoves(PieceType.Eye, _currCPUPos.u, _currCPUPos.a).Count == 0 && antlerAtEye;

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
            _currCPUPos = s_BadPos;
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
