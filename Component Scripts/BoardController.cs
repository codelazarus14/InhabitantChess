using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoardController : MonoBehaviour

{
    public GameObject prefab;

    private static float TriSize = 0.19346f;
    private static float TriHeight = Mathf.Sqrt(3) / 2 * TriSize;
    private static Vector3 WOffset = new Vector3(-0.05584711f, 0, 0.09673002f);
    private static float[] BoardLevels = { 0.05f, 0.0815f, 0.1142f };
    private static int Rows = 7;

    private Vector3 _startingPos = new Vector3(0.3350971f, BoardLevels[0], -0.58038f);
    private Dictionary<(int,int), GameObject> _highlightDict = new Dictionary<(int, int), GameObject>();

    void Start()
    {
        GenerateBoard();
        
    }

    void Update()
    {

    }

    private void GenerateBoard()
    {
        // try to remove some magic numbers and redundant if conditions from board gen pls
        for (int i = Rows; i > 0; i--)
        {
            // j keep track of # of B spaces per row,
            // i will store real count in dict
            int idx = Rows - i;
            float rowOffset = idx * TriSize / 2;

            for (int j = 0; j < i; j++)
            {
                // default to lowest height level
                float newHeight = BoardLevels[0];
                GameObject hSpace;

                // find B positions relative to starting pos
                Vector3 newPos = new Vector3(
                    _startingPos.x - TriHeight * (Rows - i),
                    newHeight,
                    _startingPos.z + rowOffset + j * TriSize);

                // don't add W to left corner
                if (Rows > i && j == 0)
                {
                    // add W spaces to left edge 
                    Vector3 newPosW = new Vector3(
                        newPos.x - WOffset.x,
                        newHeight,
                        newPos.z - WOffset.z);

                    hSpace = GameObject.Instantiate(prefab, newPosW, Quaternion.identity);
                    _highlightDict[(Rows - i, idx)] = hSpace;
                    Debug.Log("W " + hSpace.transform.position.y + ", coord" + (Rows - i, idx) + " i,j:" + (i, j));

                    idx++;
                }

                // determine height based on position
                if (Rows - 1 > i && i > 4 && 1 < j && j < i - 2) newHeight = BoardLevels[2];
                else if (Rows > i && i > 1 && 0 < j && j < i - 1) newHeight = BoardLevels[1];

                // only update y (x/z already defined for left edge W, which can never be elevated)
                newPos.y = newHeight;

                // add B spaces
                hSpace = GameObject.Instantiate(prefab, newPos, Quaternion.identity);
                _highlightDict[(Rows - i, idx)] = hSpace;
                Debug.Log("B " + hSpace.transform.position.y + ", coord" + (Rows - i, idx) + " i,j:" + (i, j));

                idx++;

                // don't add W to right corner
                if (Rows > i || j < i - 1)
                {
                    // add W spaces to right of every other B piece
                    if (Rows - 1 > i && i > 3 && 0 < j && j < i - 2) newHeight = BoardLevels[2];
                    else if (Rows > i && i > 1 && 0 <= j && j < i - 1) newHeight = BoardLevels[1];
                    else newHeight = BoardLevels[0];

                    // need to update all three components (right/down of B and also diff height)
                    Vector3 newPosW = new Vector3(
                        newPos.x - WOffset.x,
                        newHeight,
                        newPos.z + WOffset.z);

                    hSpace = GameObject.Instantiate(prefab, newPosW, Quaternion.identity);
                    _highlightDict[(Rows - i, idx)] = hSpace;
                    Debug.Log("W " + hSpace.transform.position.y + ", coord" + (Rows - i, idx) + " i,j:" + (i, j));

                    idx++;
                }
            }
        }
    }
}
