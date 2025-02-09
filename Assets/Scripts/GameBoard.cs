﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class GameBoard : MonoBehaviour
{
    public uint boardWidth = 10, boardHeight = 10;

    [SerializeField] BoardTile boardTilePrefab;
    [SerializeField] SpriteRenderer SelectionIcon;
    [SerializeField] int enviromentalObjects = 3;
    [SerializeField] List<EnviromentalObject> envirornment = new List<EnviromentalObject>();
    [SerializeField] GameObject DustEffect;

    [SerializeField] Slider miniMaxDepthSlider;
    [SerializeField] Toggle miniMaxToggle;
    public bool useMiniMax => miniMaxToggle.isOn;
    public int miniMaxDepth => Mathf.RoundToInt(miniMaxDepthSlider.value);

    public BoardTile[,] Board { get; private set; }
#pragma warning disable CS0108 // Member hides inherited member; missing new keyword
    Camera camera;
#pragma warning restore CS0108 // Member hides inherited member; missing new keyword
    //public FACTION CurrentPlayer { get; private set; }
    [SerializeField] Player[] players = new Player[2];
    int currentPlayerIndex = 0;
    public Player CurrentPlayer => players[currentPlayerIndex];
    public Player NotCurrentPlayer => players[(currentPlayerIndex + 1) % 2];
    Vector2Int lastMouseOverTile = new Vector2Int(-1, -1);
    BoardStateMachine boardStateMachine;


    Vector2Int[] pushCheckOrder = new Vector2Int[]
    {
        new Vector2Int(0,1),
        new Vector2Int(1,1),
        new Vector2Int(1,0),
        new Vector2Int(1,-1),
        new Vector2Int(0,-1),
        new Vector2Int(-1,-1),
        new Vector2Int(-1,0),
        new Vector2Int(-1,1),
    };

    BoardTile GetFreeTile(Vector2Int startPos, Vector2Int direction)
    {
        int startIndex = 0; //This loop should be replaced with a constant equation
        for (startIndex = 0; startIndex < pushCheckOrder.Length; startIndex++)
        {
            if (pushCheckOrder[startIndex] == direction) break;
        }

        for (int i = 0; i < pushCheckOrder.Length; i++)
        {
            BoardTile tile = GetBoardTile
                (startPos + pushCheckOrder[(startIndex + i) % pushCheckOrder.Length]);
            if (!tile.Occupied())
            {
                return tile;
            }
        }
        return null;
    }


    public static GameBoard Instance;
    private void Start()
    {
        Instance = this;
        camera = Camera.main;
        UI_PlayerIcons.Instance.SetPlayers(players[0], players[1]);
        UI_PlayerIcons.Instance.SetActivePlayer(players[0]);
        CreateBoard(boardWidth, boardHeight);
        boardStateMachine = new BoardStateMachine(
            new BoardState_PlaceUnits(players[0], true,
            new BoardState_ComputerPlaceUnits(players[1], false,
            new BoardState_UnSelected(players[0]))));

        //consider moving this to its own class
        SelectionIcon = Instantiate(SelectionIcon);
        SelectionIcon.gameObject.SetActive(false);
        ResizeMesh((int)boardWidth, (int)boardHeight);
        if (envirornment.Count > 0)
        {
            for (int i = 0; i < enviromentalObjects; i++)
            {
                int x;
                int y;
                do
                {
                    x = UnityEngine.Random.Range(1, (int)boardWidth - 2);
                    y = UnityEngine.Random.Range(0, (int)boardHeight - 1);

                } while (GetBoardTile(new Vector2Int(x, y)).Occupied());
                int unitIndex = UnityEngine.Random.Range(0, envirornment.Count);
                BoardTile tile = GetBoardTile(x, y);
                CreateBoardUnit(envirornment[unitIndex]).Move(tile);
            }
        }
        boardStateMachine.EnterState(this);
    }

    private void Update()
    {
        RaycastHit hit;
        Vector3 mousePos = new Vector3();
        Ray ray = camera.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out hit) &&
            hit.point.x >= 0 && hit.point.x < boardWidth && hit.point.y >= 0 && hit.point.y < boardHeight)
        {
            mousePos = hit.point;
            Vector2Int boardPos = new Vector2Int((int)mousePos.x, (int)mousePos.y);
            boardStateMachine.Update(boardPos, this);
            if (boardPos != lastMouseOverTile)
            {
                lastMouseOverTile = boardPos;
            }
            if (Input.GetKeyDown(KeyCode.Mouse0))
            {
                InteractWithTile(boardPos);
            }
        }
        else
        {
            boardStateMachine.Update(new Vector2Int(-1, -1), this);
        }

        if (Input.GetKeyDown(KeyCode.P))
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

    }
    void ResizeMesh(int width, int height)
    {
        Mesh mesh = GetComponent<MeshFilter>().mesh;

        int[] tri = mesh.triangles;
        Vector3[] verts = new Vector3[]
        {
            new Vector3(-width*0.5f,-height*0.5f),
            new Vector3(width*0.5f,-height*0.5f),
            new Vector3(-width*0.5f,height*0.5f),
            new Vector3(width*0.5f,height*0.5f)
        };
        mesh.Clear();
        mesh.vertices = verts;
        mesh.triangles = tri;
        GetComponent<MeshCollider>().sharedMesh = mesh;
    }

    void CreateBoard(uint width, uint height)
    {
        transform.position = new Vector3(width / 2, height / 2, 0);
        Board = new BoardTile[width, height];
        for (int x = 0; x < Board.GetLength(0); x++)
        {
            for (int y = 0; y < Board.GetLength(1); y++)
            {
                Board[x, y] = Instantiate(boardTilePrefab, this.transform);
                Board[x, y].gameObject.name = $"{x},{y}";
                Board[x, y].SetBoardPosition(new Vector2Int(x, y));
                Board[x, y].gameObject.transform.position = new Vector2(x + 0.5f, y + 0.5f);
                Board[x, y].SetState(TILE_MODE.Unselected);
            }
        }
    }



    public void InteractWithTile(Vector2Int boardPos)
    {
        boardStateMachine.Interact(this, boardPos);
    }
    public void EndTurn()
    {
        int[] unitsAlive = CountUnits(players);
        if (unitsAlive[0] > 0 && unitsAlive[1] > 0)
        {
            ChangePlayer();
        }
        else
        {
            if (unitsAlive[1] == 0)
            {

            }
            else
            {

            }

        }
    }



    public void MoveUnit(BoardUnitBaseClass unit, Vector2Int pos, bool useMoveAction = false) =>
        unit.Move(GetBoardTile(pos), useMoveAction);


    /// <summary>
    /// Attempts to move a unit to target tile, deals damage to both target and target tile if target tile is already occupied
    /// </summary>
    /// <param name="casterTile"></param>
    /// <param name="targetTile"></param>
    public void PushUnit(BoardTile casterTile, BoardTile targetTile, bool forcePush = false)
    {
        if (targetTile?.GetUnit != null && targetTile.GetUnit.Pushable)
        {
            Vector2Int direction = (targetTile.BoardPosition - casterTile.BoardPosition);
            direction.Clamp(new Vector2Int(-1, -1), new Vector2Int(1, 1));
            if (direction.magnitude > 1)
            {
                Debug.LogError("Yeah, you need to fix this direction bullshit, buddy");
            }
            Vector2Int endPosition = targetTile.BoardPosition + direction;
            BoardTile endTile = GetBoardTile(endPosition);
            //if target empty
            if (endTile != null && endTile.Occupied() && !forcePush)
            {
                endTile.Attack(1, direction);
                targetTile.Attack(1, direction);
            }
            //if target occupied and push is forced
            else if (
                (endTile == null || endTile.Occupied())
                && forcePush)
            {
                BoardTile freeTile = GetFreeTile(targetTile.BoardPosition, direction);
                if (freeTile == null)
                {
                    targetTile.Attack(100, direction);
                }
                else
                {
                    endTile?.Attack(1, direction);
                    targetTile?.Attack(1, direction);
                    MoveUnit(targetTile.GetUnit, freeTile.BoardPosition);
                }
            }
            else if (endTile != null)
            {
                MoveUnit(targetTile.GetUnit, endPosition);
            }
            Instantiate(DustEffect, targetTile.Position, Quaternion.identity)
                .transform.LookAt(casterTile.transform.position);
        }
    }

    /// <summary>
    /// returns the board tile of the boad[,] array. returns null if pos is outside said array
    /// </summary>
    /// <param name="pos"></param>
    /// <returns></returns>
    public BoardTile GetBoardTile(Vector2Int pos)
    {
        if (pos.x < 0 || pos.x >= Board.GetLength(0)
            || pos.y < 0 || pos.y >= Board.GetLength(1))
        {
            return null;
        }
        return Board[pos.x, pos.y];
    }
    public BoardTile GetBoardTile(int x, int y) => GetBoardTile(new Vector2Int(x, y));


    public void ChangeState(BoardState state)
    {
        boardStateMachine.ChangeState(state, this);
    }
    public void ChangeAllBoardTilesState(TILE_MODE mode)
    {
        foreach (BoardTile tile in Board)
        {
            tile.SetState(mode);
        }
    }

    public void ChangePlayer()
    {
        foreach (BoardUnitBaseClass unit in GetAllUnitsOfFaction(CurrentPlayer))
        {
            unit.ResetActions();
        }
        foreach (BoardTile tile in Board)
        {
            tile.OnEndOfTurn(CurrentPlayer);
        }
        currentPlayerIndex = (currentPlayerIndex + 1) % 2;
        boardStateMachine.ChangeState(CurrentPlayer.ControllState, this);

        UI_PlayerIcons.Instance.SetActivePlayer(CurrentPlayer);
    }

    int[] CountUnits(Player[] playerss)
    {
        int[] count = new int[playerss.Length];
        foreach (BoardTile tile in Board)
        {
            for (int i = 0; i < playerss.Length; i++)
            {
                if (tile.GetUnit?.OwningPlayer == playerss[i])
                {
                    count[i]++;
                }
            }
        }
        return count;
    }

    public List<BoardUnitBaseClass> GetAllUnitsOfFaction(Player player)
    {
        List<BoardUnitBaseClass> list = new List<BoardUnitBaseClass>();
        foreach (BoardTile tile in Board)
        {
            if (tile.GetUnit?.OwningPlayer == player)
            {
                list.Add(tile.GetUnit);
            }

        }
        return list;
    }


    public int[,] GetAccessibleTiles()
    {
        int X = Board.GetLength(0);
        int Y = Board.GetLength(1);
        int[,] accessableTiles = new int[X, Y];
        for (int x = 0; x < X; x++)
        {
            for (int y = 0; y < Y; y++)
            {
                accessableTiles[x, y] = Board[x, y].Occupied() ? 99999 : 1;
            }
        }
        return accessableTiles;
    }
    public void SetSelectionMarker(BoardTile tile)
    {
        SelectionIcon.gameObject.SetActive(true);
        SelectionIcon.transform.position = tile.Position;
    }
    public void HideSelectionMarker() => SelectionIcon.gameObject.SetActive(false);

    public BoardUnitBaseClass CreateBoardUnit(BoardUnitBaseClass creature, Player player = null)
    {
        BoardUnitBaseClass unit = Instantiate(creature);
        unit.SetPlayer(player);
        return unit;
    }

    public BoardUnit TESTTEST(BoardUnit creature, Player player)
    {
        BoardUnit unit = Instantiate(creature);
        unit.SetPlayer(player);
        return unit;
    }
}
