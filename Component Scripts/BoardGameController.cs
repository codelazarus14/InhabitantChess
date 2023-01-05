using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class BoardGameController : MonoBehaviour
{
    public Camera gameCamera;
    public string boardGameTag = "BoardGame";
    public bool playing { get; private set; }

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

    // Start is called before the first frame update
    IEnumerator Start()
    {
        // wait for board's Start() to finish
        _board = transform.Find("BoardGame_Board").gameObject.GetComponent<BoardController>();
        yield return new WaitUntil(() => _board.isInitialized);
        playing = true;
        StartCoroutine(Play());
    }

    // Update is called once per frame
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
                StartCoroutine(PlayerTurn(i));
                // wait until player finishes
                yield return new WaitUntil(() => _boardState == BoardState.Idle);
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
}
