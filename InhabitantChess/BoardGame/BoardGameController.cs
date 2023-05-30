using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Logger = InhabitantChess.Util.Logger;

namespace InhabitantChess.BoardGame
{
    public class BoardGameController : MonoBehaviour
    {
        /**
         * TODO:
         * - inscryption-style deadwood piece dropping
         * - record encounter completion the first time in saved data
         * - sfx for prisoner reactions (howl anim after repeated losses?)
         * - soundtrack ambience - fade in/out, woven between long periods of silence
         *   timber hearth, the museum, elegy, dream of home
         * - custom vision torch for delivering game rules
         * - separate rules (piece creation, legal moves, game over) from board (for supporting
         *   AI nodes/searching), maybe expose game rules thru interface? idk
         * - flickering piece highlight or more subtle effect
         * - options menu (toggle highlight, screen prompts, AI difficulty)
         * - localize error messages
         */

        public FirstPersonManipulator PlayerManip;
        public bool Playing { get; private set; }
        public delegate void BoardGameAudioEvent(int idx);
        public BoardGameAudioEvent OnPieceRemoved;

        private static float s_CPUTurnTime = 1.0f, s_DestroyDelay = 2.0f;
        private float _destroyTime;
        private int _currPlyrIdx, _antlerCount, _gamesWon, _totalGames;
        private List<GameObject> _toDestroy;
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
            _currPlyrIdx = -1;
            _toDestroy = new();
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
            Playing = true;
            StartCoroutine(Play());
        }

        // loop controlling turns, game state
        private IEnumerator Play()
        {
            int turnCount = 0;
            // turn on beam at start
            _board.UpdateBeam();

            while (Playing)
            {
                for (int i = 0; i < _board.Pieces.Count && Playing; i++)
                {
                    _currPlyrIdx = i;
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
                    // deleted flagged pieces
                    var removed = _board.CheckBeam();
                    _currPlyrIdx = i = RemovePieces(removed, i);
                    Playing = IsGameOver();
                }
                Logger.Log($"Turn {++turnCount} complete");
            }
            Logger.Log("Game Over!");
            _totalGames++;
            if (_antlerCount > 0) _gamesWon++;
            Logger.Log($"Game finished, win ratio: {_gamesWon} / {_totalGames - _gamesWon}");
        }

        private IEnumerator PlayerTurn(int pIdx)
        {
            (GameObject g, (int up, int across) pos, PieceType type) player = _board.Pieces[pIdx];
            List<(int, int)> adj = _board.LegalMoves(player.pos, player.type);

            _board.ToggleSpaces(adj);
            _board.ToggleHighlight(player.g);
            // wait for input, then move
            while (_selectedSpace == null || !adj.Contains(_selectedSpace.Space))
            {
                _boardState = BoardState.WaitingForInput;
                yield return new WaitUntil(() => _boardState == BoardState.InputReceived);
            }
            // we're ready to move
            _board.DoMove(pIdx, _selectedSpace.Space);
            yield return new WaitUntil(() => !_board.Moving);
            _boardState = BoardState.DoneMoving;
            // reset highlighting/visibility and finish
            _board.ToggleHighlight(player.g);
            _board.ToggleSpaces(adj);
            // blocker piece should update beam on move
            if (player.type == PieceType.Blocker)
            {
                _board.UpdateBeam();
            }
            _boardState = BoardState.Idle;
        }

        private IEnumerator CPUTurn(int pIdx)
        {
            (GameObject g, (int up, int across) pos, PieceType type) player = _board.Pieces[pIdx];
            List<(int, int)> adj = _board.LegalMoves(player.pos, player.type);
            _board.ToggleHighlight(player.g);
            // add artificial wait
            _boardState = BoardState.WaitingForInput;
            yield return new WaitForSecondsRealtime(s_CPUTurnTime);
            _boardState = BoardState.InputReceived;
            // randomly choose an adjacent space
            // in future - could replace this w a call to a function that uses AI rules
            (int, int) randPos = adj[Random.Range(0, adj.Count)];
            _selectedSpace = _board.SpaceDict[randPos].GetComponent<SpaceController>();
            // move to space
            _board.DoMove(pIdx, _selectedSpace.Space);
            yield return new WaitUntil(() => !_board.Moving);
            _boardState = BoardState.DoneMoving;
            _board.UpdateBeam();
            // reset
            _board.ToggleHighlight(player.g);
            _boardState = BoardState.Idle;
        }

        private bool IsGameOver()
        {
            // TODO - add condition for cpu loss
            // no more antler pieces left
            return _antlerCount > 0;
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
