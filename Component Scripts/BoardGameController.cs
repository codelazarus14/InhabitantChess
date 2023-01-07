using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class BoardGameController : MonoBehaviour
{
    public Camera gameCamera;
    public string boardGameTag = "BoardGame";
    public bool playing { get; private set; }

    private float CPUTurnTime = 1.0f;
    private BoardController _board;
    private BoardState _boardState = BoardState.Idle;
    private SpaceController _selectedSpace;

    private enum BoardState
    {
        WaitingForInput,
        InputReceived,
        Moving,
        Idle
    }

    IEnumerator Start()
    {
        // wait for board's Start() to finish
        _board = transform.Find("BoardGame_Board").gameObject.GetComponent<BoardController>();
        yield return new WaitUntil(() => _board.isInitialized);
        playing = true;
        StartCoroutine(Play());
    }

    void Update()
    {
        // check for user mouse input
        if (_boardState == BoardState.WaitingForInput && Mouse.current.leftButton.wasPressedThisFrame)
        {
            CastRay();
        }

        void CastRay()
        {
            Vector3 mousePos = Mouse.current.position.ReadValue();
            Ray ray = gameCamera.ScreenPointToRay(mousePos);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit)) {
               if (hit.collider.tag == boardGameTag)
                {
                    SpaceController hitSpc = hit.collider.gameObject.GetComponent<SpaceController>();
                    // allow PlayerTurn to proceed
                    _boardState = BoardState.InputReceived;
                    _selectedSpace = hitSpc;
                }
            }
        }
    }

    private IEnumerator Play()
    {
        int turnCount = 0;
        while (playing)
        {
            for (int i = 0; i < _board.players.Count; i++)
            {
                if (_board.players[i].type == PieceType.Eye)
                {
                    StartCoroutine(CPUTurn(i));
                }
                else
                {
                    StartCoroutine(PlayerTurn(i));
                }
                // wait until turn finishes
                yield return new WaitUntil(() => _boardState == BoardState.Idle);
                _board.UpdateBeam();
            }
            Debug.Log($"Turn {turnCount++} complete");
        }
    }

    private IEnumerator PlayerTurn(int pIdx) 
    {
        (GameObject g, (int up, int across) pos, PieceType type) player = _board.players[pIdx];
        List<(int, int)> adj = _board.GetAdjacent(player.pos.up, player.pos.across);
        _board.ToggleSpaces(adj);
        _board.ToggleHighlight(player.g);
        // wait for input, then move
        _boardState = BoardState.WaitingForInput;
        yield return new WaitUntil(() => _boardState == BoardState.InputReceived);
        // we're ready to move
        // might use moving to check animation status later idk
        _boardState = BoardState.Moving;
        _board.TryMove(pIdx, _selectedSpace.space);
        // reset highlighting/visibility and finish
        _board.ToggleHighlight(player.g);
        _board.ToggleSpaces(adj);
        _boardState = BoardState.Idle;
    }

    private IEnumerator CPUTurn(int pIdx)
    {
        (GameObject g, (int up, int across) pos, PieceType type) player = _board.players[pIdx];
        List<(int, int)> adj = _board.GetAdjacent(player.pos.up, player.pos.across);
        _board.ToggleSpaces(adj);
        // add artificial wait
        _boardState = BoardState.WaitingForInput;
        yield return new WaitForSecondsRealtime(CPUTurnTime);
        _boardState = BoardState.InputReceived;
        // randomly choose an adjacent space
        // in future - could replace this w a call to a function that uses AI rules
        (int, int) randPos = adj[Random.Range(0, adj.Count)];
        _selectedSpace = _board.spaceDict[randPos].GetComponent<SpaceController>();
        // move to space
        _boardState = BoardState.Moving;
        _board.TryMove(pIdx, _selectedSpace.space);
        // reset
        _board.ToggleSpaces(adj);
        _boardState = BoardState.Idle;
    }
}
