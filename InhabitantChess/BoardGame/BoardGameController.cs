using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Logger = InhabitantChess.Util.Logger;

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

        private static float s_CPUTurnTime = 1.0f, s_DestroyDelay = 2.0f;
        private float _destroyTime;
        private int _antlerCount, _gamesWon, _totalGames;
        private bool _reachedEye, _noLegalMoves, _movesHighlightEnabled, _pieceHighlightEnabled, _beamHighlightEnabled;
        private (GameObject g, (int up, int across) pos, PieceType type) _currentPlayer;
        private List<GameObject> _toDestroy;
        private List<(int, int)> _legalMoves;
        private (int u, int a) _currCPUPos;
        private BoardController _board;
        private BoardState _boardState = BoardState.Idle;
        private SpaceController _selectedSpace;

        private enum BoardState
        {
            WaitingForInput,
            InputReceived,
            DoneMoving,
            Idle,
        }

        private void Start()
        {
            _board = transform.Find("BoardGame_Board").gameObject.GetComponent<BoardController>();
            _board.Init();
            _toDestroy = new();
            OnHighlightConfigure(InhabitantChess.Instance.Highlighting);
        }

        public void OnHighlightConfigure((bool moves, bool pieces, bool beam) hConfig)
        {
            _movesHighlightEnabled = hConfig.moves;
            _pieceHighlightEnabled = hConfig.pieces;
            _beamHighlightEnabled = hConfig.beam;

            if (_board != null && _board.IsInitialized) RefreshHighlighting();
        }

        private void RefreshHighlighting()
        {
            if (_boardState == BoardState.WaitingForInput)
            {
                _board.ToggleSpaces(_legalMoves, _movesHighlightEnabled);
                _board.ToggleHighlight(_currentPlayer.g, _pieceHighlightEnabled);
            }
            _board.UpdateBeam(_beamHighlightEnabled);
        }

        private void Update()
        {
            if (!_board.IsInitialized) return;

            // delay destruction to let AudioSources finish playing
            if (_toDestroy.Count > 0 && Time.time >= _destroyTime + s_DestroyDelay)
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

        public void OnInteract()
        {
            // does nothing if mid-game
            if (Playing) return;

            _board.ResetBoard();
            _antlerCount = _board.Pieces.Where(piece => piece.type == PieceType.Antler).Count();
            _noLegalMoves = _reachedEye = false;
            _currCPUPos = (99, 99);
            Playing = true;
            StartCoroutine(Play());
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
            Logger.Log("Game Over!");
            _totalGames++;
            if (PlayerWon()) _gamesWon++;
            Logger.Log($"Game finished, win ratio {_gamesWon} : {_totalGames - _gamesWon}");
        }

        private IEnumerator PlayerTurn(int pIdx)
        {
            _currentPlayer = _board.Pieces[pIdx];
            _legalMoves = _board.LegalMoves(_currentPlayer.pos, _currentPlayer.type);
            if (_legalMoves.Count == 0)
            {
                _noLegalMoves = true;
                _boardState = BoardState.Idle;
                yield break;
            }

            _board.ToggleSpaces(_legalMoves, _movesHighlightEnabled);
            _board.ToggleHighlight(_currentPlayer.g, _pieceHighlightEnabled);
            // wait for input, then move
            while (_selectedSpace == null || !_legalMoves.Contains(_selectedSpace.Space))
            {
                _boardState = BoardState.WaitingForInput;
                yield return new WaitUntil(() => _boardState == BoardState.InputReceived);
            }
            // we're ready to move
            _board.DoMove(pIdx, _selectedSpace.Space);
            yield return new WaitUntil(() => !_board.Moving);
            _boardState = BoardState.DoneMoving;
            // reset highlighting/visibility and finish
            _board.ToggleHighlight(_currentPlayer.g, false);
            _board.ToggleSpaces(_legalMoves, _movesHighlightEnabled);
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
            _legalMoves = _board.LegalMoves(_currentPlayer.pos, _currentPlayer.type);
            if (_legalMoves.Count == 0)
            {
                _noLegalMoves = true;
                _boardState = BoardState.Idle;
                yield break;
            }
            _board.ToggleHighlight(_currentPlayer.g, _pieceHighlightEnabled);
            // add artificial wait
            _boardState = BoardState.WaitingForInput;
            yield return new WaitForSecondsRealtime(s_CPUTurnTime);
            _boardState = BoardState.InputReceived;
            (int, int) randPos = ChooseCPUMove(_legalMoves);
            _selectedSpace = _board.SpaceDict[randPos].GetComponent<SpaceController>();
            // move to space
            _board.DoMove(pIdx, _selectedSpace.Space);
            yield return new WaitUntil(() => !_board.Moving);
            _boardState = BoardState.DoneMoving;
            _board.UpdateBeam(_beamHighlightEnabled);
            // reset
            _board.ToggleHighlight(_currentPlayer.g, false);
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
            var cpuAdjPositions = _board.LegalMoves(_currCPUPos, PieceType.Eye, true);
            bool antlerAtEye = false;
            foreach (var pos in cpuAdjPositions)
            {
                antlerAtEye |= _board.Pieces.Any(piece => piece.pos == pos && piece.type == PieceType.Antler);
            }
            // completely blocked including at least one antler
            _reachedEye = _board.LegalMoves(_currCPUPos, PieceType.Eye).Count == 0 && antlerAtEye;

            return _reachedEye || _noLegalMoves || _antlerCount < 1;
        }

        private bool PlayerWon()
        {
            return _reachedEye;
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
                plyr.g.transform.DestroyAllChildren();
                _toDestroy.Add(plyr.g);
                OnPieceRemoved?.Invoke(r);
                Debug.Log($"Removed {plyr.g.name}, i = {i}, list length {_board.Pieces.Count}");
            }
            _destroyTime = Time.time;
            return i;
        }
    }
}
