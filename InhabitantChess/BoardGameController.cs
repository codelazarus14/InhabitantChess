using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class BoardGameController : MonoBehaviour
{
    /**
     * TODO:
     * - update higlight to only occur after pause, blinking intervals
     * - replace highlight materials (space and beam) with SIM in-game
     * - replace piece-teleporting with animated slerp or smth
     */

    public Camera GameCamera;
    public GameObject StartText;
    public string BoardGameTag = "BoardGame";
    public bool Playing { get; private set; }

    private float _CPUTurnTime = 1.0f;
    private BoardController _board;
    private BoardState _boardState = BoardState.Idle;
    private SpaceController _selectedSpace;

    private enum BoardState
    {
        WaitingForInput,
        InputReceived,
        Moving,
        Idle,
        GameOver
    }

    void Start()
    {
        _board = transform.Find("BoardGame_Board").gameObject.GetComponent<BoardController>();
        _board.Init();
    }

    void Update()
    {
        if (!Playing && Keyboard.current.shiftKey.wasPressedThisFrame)
        {
            EnterGame();
        }
        // check for user input
        else if (_boardState == BoardState.WaitingForInput && Mouse.current.leftButton.wasPressedThisFrame)
        {
            CastRay();
        }
        else if (_boardState == BoardState.GameOver)
        {
            // resume if player selects prompt to start new game
        }

        void CastRay()
        {
            Vector3 mousePos = Mouse.current.position.ReadValue();
            Ray ray = GameCamera.ScreenPointToRay(mousePos);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit)) 
            {
                if (hit.collider.tag == BoardGameTag)
                {
                    SpaceController hitSpc = hit.collider.gameObject.GetComponent<SpaceController>();
                    // allow PlayerTurn to proceed
                    _boardState = BoardState.InputReceived;
                    _selectedSpace = hitSpc;
                }
            }
        }
    }

    public void EnterGame()
    {
        StartText.SetActive(false);
        _board.ResetBoard();
        Playing = true;
        StartCoroutine(Play());
    }

    public void ExitGame()
    {
        Playing = false;
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
            for (int i = 0; i < _board.Players.Count && Playing; i++)
            {
                if (_board.Players[i].type == PieceType.Eye)
                {
                    StartCoroutine(CPUTurn(i));
                }
                else
                {
                    StartCoroutine(PlayerTurn(i));
                }
                // wait until turn finishes
                yield return new WaitUntil(() => _boardState == BoardState.Idle);
                // deleted flagged players
                var removed = _board.CheckBeam();
                foreach (int r in removed)
                {
                    var temp = _board.Players[r];
                    _board.Players.RemoveAt(r);
                    Destroy(temp.g);
                    if (r <= i) i--;
                    Debug.Log($"Removed {temp.g.name}, i = {i}, list length {_board.Players.Count}");
                    Playing = _board.Players.Count > 1;
                }
            }
            Debug.Log($"Turn {turnCount++} complete");
        }
        Debug.Log("Game Over!");
        _boardState = BoardState.GameOver;
    }

    private IEnumerator PlayerTurn(int pIdx) 
    {
        (GameObject g, (int up, int across) pos, PieceType type) player = _board.Players[pIdx];
        List<(int, int)> adj = _board.GetAdjacent(player.pos.up, player.pos.across);
        _board.ToggleSpaces(adj);
        _board.ToggleHighlight(player.g);
        // wait for input, then move
        _boardState = BoardState.WaitingForInput;
        yield return new WaitUntil(() => _boardState == BoardState.InputReceived);
        // we're ready to move
        // might use moving to check animation status later idk
        _boardState = BoardState.Moving;
        _board.TryMove(pIdx, _selectedSpace.Space);
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
        (GameObject g, (int up, int across) pos, PieceType type) player = _board.Players[pIdx];
        List<(int, int)> adj = _board.GetAdjacent(player.pos.up, player.pos.across);
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
        _boardState = BoardState.Moving;
        _board.TryMove(pIdx, _selectedSpace.Space);
        _board.UpdateBeam();
        // reset
        _board.ToggleHighlight(player.g);
        _boardState = BoardState.Idle;
    }
}
