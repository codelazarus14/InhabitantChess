using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace InhabitantChess.BoardGame
{
    public class BoardGameController : MonoBehaviour
    {
        /**
         * TODO:
         * - replace piece-teleporting with animated slerp or smth
         * - inscryption-style deadwood piece dropping
         * - add screen prompts for controls (interact, enter/exit overhead, lean forward)
         * - separate rules from board, game state (for supporting AI nodes/different rulesets)
         */

        public FirstPersonManipulator PlayerManip;
        public GameObject StartText;
        public bool Playing { get; private set; }

        private float _CPUTurnTime = 1.0f;
        private int _currPlyrIdx;
        private BoardController _board;
        private BoardState _boardState = BoardState.Idle;
        private SpaceController _selectedSpace;

        private enum BoardState
        {
            WaitingForInput,
            InputReceived,
            DoneMoving,
            Idle,
            GameOver
        }

        private void Start()
        {
            _board = transform.Find("BoardGame_Board").gameObject.GetComponent<BoardController>();
            _board.Init();
            _currPlyrIdx = -1;
        }

        private void Update()
        {
            if (!_board.IsInitialized) return;

            // check for user input - should probably add a prompt to show space under cursor
            if (_boardState == BoardState.WaitingForInput && OWInput.IsNewlyPressed(InputLibrary.interact, InputMode.All))
            {
                CastRay();
            }
            else if (_boardState == BoardState.GameOver)
            {
                // resume if player selects prompt to start new game
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

        public void EnterGame()
        {
            if (Playing) return;

            StartText.SetActive(false);
            _board.ResetBoard();
            Playing = true;
            StartCoroutine(Play());
        }

        public void ExitGame()
        {
            Playing = false;
            if (_currPlyrIdx != -1)
            {
                var currPlayer = _board.Pieces[_currPlyrIdx];
                _board.ToggleHighlight(currPlayer.g);
                _board.ToggleSpaces(_board.LegalMoves(currPlayer.pos, currPlayer.type));
                _board.UpdateBeam(true);
            }
            StartText.SetActive(true);
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
                    i = RemovePieces(removed, i);
                    Playing = _board.Pieces.Count > 1;
                }
                Debug.Log($"Turn {turnCount++} complete");
            }
            Debug.Log("Game Over!");
            _boardState = BoardState.GameOver;
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
            _board.TryMove(pIdx, _selectedSpace.Space);
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
            yield return new WaitForSecondsRealtime(_CPUTurnTime);
            _boardState = BoardState.InputReceived;
            // randomly choose an adjacent space
            // in future - could replace this w a call to a function that uses AI rules
            (int, int) randPos = adj[Random.Range(0, adj.Count)];
            _selectedSpace = _board.SpaceDict[randPos].GetComponent<SpaceController>();
            // move to space
            _board.TryMove(pIdx, _selectedSpace.Space);
            yield return new WaitUntil(() => !_board.Moving);
            _boardState = BoardState.DoneMoving;
            _board.UpdateBeam();
            // reset
            _board.ToggleHighlight(player.g);
            _boardState = BoardState.Idle;
        }

        private int RemovePieces(List<int> Pieces, int currTurn)
        {
            int i = currTurn;
            foreach (int r in Pieces)
            {
                // replace piece w new deadwood
                var plyr = _board.Pieces[r];
                _board.Pieces.RemoveAt(r);
                _board.AddDeadwood(plyr.type);
                Destroy(plyr.g);
                // dec currTurn if removed piece would shift piece list index up 1
                // so we don't skip the next one in Play() loop
                if (r <= i) i--;
                Debug.Log($"Removed {plyr.g.name}, i = {i}, list length {_board.Pieces.Count}");
            }
            return i;
        }
    }
}
