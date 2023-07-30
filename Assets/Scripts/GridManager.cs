using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UniRx;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using Random = System.Random;

public class GridManager : MonoBehaviour
{
    [Header("Game Config")]
    [SerializeField, Tooltip("If the resulting swap does not generate a match, reverse")] private bool disallowLooseSwap;
    [SerializeField] private int rowCount;
    [SerializeField] private int columnCount;
    [SerializeField] private List<Color> colorsToUse;
    [SerializeField] private GridView gridView;
    [SerializeField] private TextMeshProUGUI actionsMadeLabel;
    [SerializeField] private Button simulateButton;
    [SerializeField] private Button stopButton;
    [SerializeField] private TMP_InputField inputField;

    private readonly ISubject<bool> _onGridInitialized = new Subject<bool>();
    private readonly ISubject<(List<Cell>, bool)> _onCreateCells = new Subject<(List<Cell>, bool)>();
    private readonly ISubject<List<Cell>> _onRequestPulseCells = new Subject<List<Cell>>();
    private readonly ISubject<(Cell cellA, Cell cellB)> _onRequestSwapCells = new Subject<(Cell cellA, Cell cellB)>();
    private readonly ISubject<List<Cell>> _onRequestDestroyCells = new Subject<List<Cell>>();
    private readonly ISubject<List<Cell>> _onRequestMoveCells = new Subject<List<Cell>>();
    public IObservable<bool> OnGridInitialized => _onGridInitialized;
    public IObservable<(List<Cell>, bool)> OnCreateCells => _onCreateCells;
    public IObservable<List<Cell>> OnRequestPulseCells => _onRequestPulseCells;
    public IObservable<List<Cell>> OnRequestDestroyCells => _onRequestDestroyCells;
    public IObservable<List<Cell>> OnRequestMoveCells => _onRequestMoveCells;
    public IObservable<(Cell cellA, Cell cellB)> OnRequestSwapCells => _onRequestSwapCells;
    
    private Cell[,] _grid;
    public Cell[,] CurrentGrid => _grid;

    private Cell _selectedCell;
    private Cell _targetCell;

    private const int DEFAULT_MAX_ACTIONS = 1_000_000; 
    private int _maxActions = 1_000_000;
    private int _actionCount;
    private bool _simulateActions;
    private CancellationTokenSource _cts = new();

    private void Awake()
    {
        inputField.onValueChanged.AsObservable().Subscribe(_ =>
        {
            if (int.TryParse(inputField.text, out var parsedValue))
            {
                _maxActions = parsedValue;
            }
            else
            {
                _maxActions = DEFAULT_MAX_ACTIONS;
                inputField.text = $"{DEFAULT_MAX_ACTIONS}";
            }
        }).AddTo(this);
        
        simulateButton.onClick.AsObservable().Subscribe(async _ =>
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            await RunSimulation(_cts.Token);

        }).AddTo(this);
        stopButton.onClick.AsObservable().Subscribe(_ => StopSimulation()).AddTo(this);
    }
    
    private void Start()
    {
        _grid = new Cell[columnCount, rowCount];
        FillGrid(true);
    }

    private void StopSimulation()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        _simulateActions = false;
         _actionCount = 0;
        actionsMadeLabel.text = _actionCount.ToString();
    }

    private async UniTask RunSimulation(CancellationToken cancellationToken)
    {
        while (true)
        {
            var r = new Random();
            var cell = _grid[r.Next(_grid.GetLength(0)), r.Next(_grid.GetLength(1))];
            await UniTask.WaitUntil(() => !gridView.IsAnimating);
            await OnClick(cell);

            if (cancellationToken.IsCancellationRequested)
            {
                await OnClick(cell); // deselects the cell
                return;
            }

            // to make stuff happen, let's have the simulation always click a neighbor.
            var getNeighbors = GetAdjacentCells(cell);
            await UniTask.WaitUntil(() => !gridView.IsAnimating);
            
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            
            await OnClick(getNeighbors[r.Next(getNeighbors.Count)]);
            
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            _actionCount++;
            actionsMadeLabel.text = _actionCount.ToString();

            if (_actionCount >= _maxActions)
            {
                StopSimulation();
                return;
            }
        }
    }

    private void FillGrid(bool initial = false)
    {
        var createdCells = new HashSet<Cell>();
        for (var x = 0; x < _grid.GetLength(0); x++)
        {
            for (var y = 0; y < _grid.GetLength(1); y++)
            {
                if (_grid[x, y] is null)
                {
                    var colorPool = colorsToUse.ToList();

                    foreach (var v in GetAdjacentCells(new Cell(x, y, new Color())))
                    {
                        if (FindMatches(v, 1).Count > 0)
                        {
                            if (colorPool.Contains(v.color))
                            {
                                colorPool.Remove(v.color);
                            }
                        }
                    }
                    
                    var index = new Random().Next(colorPool.Count);
                    _grid[x, y] = new Cell(x, y, colorPool[index])
                    {
                        onClick = async cell =>
                        {
                            if (_simulateActions)
                            {
                                return;
                            }
                            
                            await OnClick(cell);
                        }
                    };

                    createdCells.Add(_grid[x, y]);
                }
            }
        }

        if (initial)
        {
            _onGridInitialized.OnNext(true);
        }
        _onCreateCells.OnNext((createdCells.ToList(), initial));
    }

    private async UniTask OnClick(Cell cell)
    {
        if (_selectedCell is null)
        {
            _selectedCell = cell;
            _onRequestPulseCells.OnNext(null);
            _onRequestPulseCells.OnNext(new List<Cell> {_selectedCell});
        }
        else
        {
            var neighbors = GetAdjacentCells(_selectedCell);
            if (neighbors.Contains(cell))
            {
                _targetCell = cell;
                _onRequestPulseCells.OnNext(null);
                
                // Swap
                SwapCells(_selectedCell, _targetCell);
                await UniTask.WaitUntil(() => !gridView.IsAnimating);
                _onRequestSwapCells.OnNext((_selectedCell, _targetCell));
                
                // Check Matches
                var totalMatches = new HashSet<Cell>();
                totalMatches.AddRange(FindMatches(_selectedCell, 3));
                totalMatches.AddRange(FindMatches(_targetCell, 3));

                if (totalMatches.Count == 0)
                {
                    if (disallowLooseSwap)
                    {
                        SwapCells(_selectedCell, _targetCell);
                        await UniTask.WaitUntil(() => !gridView.IsAnimating);
                        _onRequestSwapCells.OnNext((_selectedCell, _targetCell));
                    }
                    
                    _selectedCell = null;
                    _targetCell = null;
                    
                    return;
                }
                
                foreach (var c in totalMatches.ToList())
                {
                    DestroyBlock(c);
                }
                await UniTask.WaitUntil(() => !gridView.IsAnimating);
                _onRequestDestroyCells.OnNext(totalMatches.ToList());
                
                await SettleGrid();
                FillGrid();

                _selectedCell = null;
                _targetCell = null;
            }
            else
            {
                _selectedCell = null;
                _onRequestPulseCells.OnNext(null);
            }
        }
    }

    private void DestroyBlock(Cell cell)
    {
        _grid[(int) cell.coordinates.x, (int) cell.coordinates.y] = null;
    }
    
    private void SwapCells(Cell cellA, Cell cellB)
    {
        // change internal coordinates of cells
        // update the grid
        
        var initA = cellA.coordinates;
        var initB = cellB.coordinates;
        cellA.coordinates = initB;
        cellB.coordinates = initA;
        _grid[(int)cellA.coordinates.x, (int)cellA.coordinates.y] = cellA;
        _grid[(int)cellB.coordinates.x, (int)cellB.coordinates.y] = cellB;
    }

    private HashSet<Cell> FindMatches(Cell anchor, int minimum)
    {
        var totalMatches = new HashSet<Cell>();
        
        // horizontal
        var horizontalMatches = new HashSet<Cell>();
        horizontalMatches.AddRange(GetMatchesInDirection(anchor, Direction.Left));
        horizontalMatches.AddRange(GetMatchesInDirection(anchor, Direction.Right));
        
        // vertical
        var verticalMatches = new HashSet<Cell>();
        verticalMatches.AddRange(GetMatchesInDirection(anchor, Direction.Up));
        verticalMatches.AddRange(GetMatchesInDirection(anchor, Direction.Down));
        
        if (horizontalMatches.Count >= minimum)
        {
            totalMatches.AddRange(horizontalMatches);
        }
        
        if (verticalMatches.Count >= minimum)
        {
            totalMatches.AddRange(verticalMatches);
        }
        
        return totalMatches;
    }

    /// <summary>
    /// Drops down cells and processes resulting matches
    /// </summary>
    private async UniTask SettleGrid()
    {
        while (true)
        {
            var cellsToMove = new HashSet<Cell>();

            void MoveDown(Cell cell)
            {
                if (cell == null)
                {
                    return;
                }

                while (true)
                {
                    var yPosOriginal = (int) cell.coordinates.y;
                    var xPos = (int) cell.coordinates.x;
                    if (GetCellNeighbor(cell, Direction.Down) != null || yPosOriginal <= 0)
                    {
                        return;
                    }

                    cell.coordinates = new Vector2(xPos, yPosOriginal - 1);
                    _grid[xPos, yPosOriginal - 1] = cell;
                    cellsToMove.Add(cell);
                    _grid[xPos, yPosOriginal] = null;
                }
            }

            for (var j = 0; j < _grid.GetLength(1); j++)
            {
                for (var i = 0; i < _grid.GetLength(0); i++)
                {
                    MoveDown(_grid[i, j]);
                }
            }

            await UniTask.WaitUntil(() => !gridView.IsAnimating);
            _onRequestMoveCells.OnNext(cellsToMove.ToList());

            if (cellsToMove.Count > 0)
            {
                var totalMatches = new HashSet<Cell>();
                foreach (var cell in cellsToMove)
                {
                    totalMatches.AddRange(FindMatches(cell, 3));
                }

                foreach (var c in totalMatches.ToList())
                {
                    DestroyBlock(c);
                }

                await UniTask.WaitUntil(() => !gridView.IsAnimating);
                _onRequestDestroyCells.OnNext(totalMatches.ToList());

                continue;
            }

            break;
        }
    }

    private List<Cell> GetAdjacentCells(Cell cell)
    {
        var values = Enum.GetValues(typeof(Direction)).Cast<Direction>();

        var neighbors = new List<Cell>();
        foreach (var direction in values)
        {
            var potentialCell = GetCellNeighbor(cell, direction);
            if (potentialCell is not null)
            {
                neighbors.Add(potentialCell);
            }
        }

        return neighbors;
    }
    
    private IEnumerable<Cell> GetMatchesInDirection(Cell anchor, Direction direction)
    {
        var matchingCells = new HashSet<Cell> {anchor};

        // Continue checking in the specified direction
        var nextCell = GetCellNeighbor(anchor, direction);
        while (nextCell != null && nextCell.color == anchor.color)
        {
            matchingCells.Add(nextCell);
            nextCell = GetCellNeighbor(nextCell, direction);
        }

        return matchingCells;
    }
    
    private enum Direction
    {
        Left,
        Right,
        Up, 
        Down
    }
    
    private Cell GetCellNeighbor(Cell anchor, Direction direction)
    {
        var x = (int) anchor.coordinates.x;
        var y = (int) anchor.coordinates.y;
        
        switch (direction)
        {
            case Direction.Left:
                x--;
                break;
            case Direction.Right:
                x++;
                break;
            case Direction.Up:
                y++;
                break;
            case Direction.Down:
                y--;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
        }

        return IsCoordinateValid(x, y) ? _grid[x, y] : null;
    }
    
    private bool IsCoordinateValid(int x, int y)
    {
        return x >= 0 && 
               x < _grid.GetLength(0) &&
               y >= 0 && 
               y < _grid.GetLength(1);
    }
}
