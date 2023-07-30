using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UniRx;
using UnityEngine;

public class GridView : MonoBehaviour
{
    [SerializeField] private RectTransform gridCanvas;
    [SerializeField] private float width;
    [SerializeField] private float height;
    [SerializeField] private GridManager gridManager;
    [SerializeField] private CellView cellPrefab;

    private readonly Dictionary<Cell, CellView> _cellMap = new();
    private List<Sequence> _createSequences = new();
    private List<Sequence> _pulseSequences = new();
    private List<Sequence> _destroySequences  = new();
    private List<Sequence> _moveSequences  = new();

    private float _dimension;

    public bool IsAnimating {
        get
        {
            var isAnimating = false;

            foreach (var seq in _createSequences.Where(seq => seq.IsActive() && seq.IsPlaying()))
            {
                isAnimating = true;
            }

            foreach (var seq in _destroySequences.Where(seq => seq.IsActive() && seq.IsPlaying()))
            {
                isAnimating = true;
            }
            
            foreach (var seq in _moveSequences.Where(seq => seq.IsActive() && seq.IsPlaying()))
            {
                isAnimating = true;
            }
            
            return isAnimating;
        }
    }

    private void Awake()
    {
        gridManager.OnGridInitialized.Subscribe(_ =>
        {
            InitializeDimensions(gridManager.CurrentGrid.GetLength(0), gridManager.CurrentGrid.GetLength(1));
        }).AddTo(this);

        gridManager.OnCreateCells.Subscribe(CreateCells).AddTo(this);
        
        gridManager.OnRequestPulseCells.Subscribe(PulseCells).AddTo(this);
        
        gridManager.OnRequestDestroyCells.Subscribe(DestroyCells).AddTo(this);
        
        gridManager.OnRequestMoveCells.Subscribe(MoveCells).AddTo(this);

        gridManager.OnRequestSwapCells.Subscribe(SwapCells).AddTo(this);
    }

    private void InitializeDimensions(int columns, int rows)
    {
        var rect = gridCanvas.rect;
        width = rect.width * 0.95f;
        height = rect.height * 0.95f;

        var cellWidth = width / columns;
        var cellHeight = height / rows;
        _dimension = Mathf.Min(cellWidth, cellHeight);
    }

    private void CreateCells((List<Cell> cells, bool initial) param)
    {
        foreach (var cell in param.cells)
        {
            var cellView = Instantiate(cellPrefab, transform, true);
            cellView.transform.localScale = Vector3.one;
            cellView.RectTransform.sizeDelta = Vector2.one * _dimension * 0.90f;

            if (param.initial)
            {
                cellView.RectTransform.anchoredPosition =
                    cell.coordinates * _dimension + (Vector2.one * _dimension * 0.10f);
            }
            else
            {
                cellView.RectTransform.anchoredPosition = new Vector2(cell.coordinates.x, 0) * _dimension + (Vector2.one * _dimension * 0.10f) + (Vector2.up * gridCanvas.rect.height);
                var seq = DOTween.Sequence();
                seq.Append(cellView.RectTransform.DOAnchorPos(cell.coordinates * _dimension + (Vector2.one * _dimension * 0.10f), 0.15f));
                _createSequences.Add(seq);
            }

            cellView.Initialize(cell);
            _cellMap[cell] = cellView;
        }
    }
    
    private void PulseCells(List<Cell> cells)
    {
        foreach (var seq in _pulseSequences)
        {
            seq?.Kill();
        }

        _pulseSequences = new List<Sequence>();

        if (cells is null)
        {
            return;
        }
            
        foreach (var cell in cells)
        {
            if (_cellMap.TryGetValue(cell, out var cellView))
            {
                var seq = DOTween.Sequence();
                seq.Append(cellView.transform.DOScale(0.90f, 0.15f)).Append(cellView.transform.DOScale(1f, 0.15f));
                seq.SetLoops(-1, LoopType.Restart);
                seq.OnKill(() => cellView.RectTransform.localScale = Vector3.one);
                _pulseSequences.Add(seq);
            }
        }
    }

    private void DestroyCells(List<Cell> cells)
    {
        foreach (var seq in _destroySequences)
        {
            seq?.Kill(true);
        }

        _destroySequences = new List<Sequence>();

        if (cells is null)
        {
            return;
        }
            
        foreach (var cell in cells)
        {
            if (_cellMap.TryGetValue(cell, out var cellView))
            {
                var seq = DOTween.Sequence();
                seq.Append(cellView.transform.DOScale(0.0f, 0.15f));

                void Completion()
                {
                    Destroy(cellView.gameObject);
                    _cellMap[cell] = null;
                }
                seq.OnKill(Completion);
                seq.OnComplete(Completion);
                _destroySequences.Add(seq);
            }
        }
    }

    private void MoveCells(List<Cell> cells)
    {
        foreach (var seq in _moveSequences)
        {
            seq?.Kill();
        }

        _moveSequences = new List<Sequence>();

        if (cells is null)
        {
            return;
        }
            
        foreach (var cell in cells)
        {
            if (_cellMap.TryGetValue(cell, out var cellView))
            {
                var seq = DOTween.Sequence();
                _cellMap[cell] = cellView;
                var target = cell.coordinates * _dimension + Vector2.one * _dimension * 0.10f;
                seq.Append(cellView.RectTransform.DOAnchorPos(target, 0.15f));
                seq.OnComplete(() => cellView.RectTransform.anchoredPosition = target);
                seq.OnKill(() => cellView.RectTransform.anchoredPosition = target);
                _moveSequences.Add(seq);
            }
        }
    }

    private void SwapCells((Cell cellA, Cell cellB) cells)
    {
        foreach (var s in _moveSequences)
        {
            s?.Kill();
        }
            
        _moveSequences = new List<Sequence>();

        var tempA = _cellMap[cells.cellA];
        var tempB = _cellMap[cells.cellB];
        var cellAPos = tempA.RectTransform.anchoredPosition;
        var cellBPos = tempB.RectTransform.anchoredPosition;
        var seq = DOTween.Sequence();
        seq.Append(tempA.RectTransform.DOAnchorPos(cellBPos, 0.25f));
        seq.OnKill(() => tempA.RectTransform.anchoredPosition = cellBPos);
        var seqB = DOTween.Sequence();
        seq.Append(tempB.RectTransform.DOAnchorPos(cellAPos, 0.25f));
        seqB.OnKill(() => tempB.RectTransform.anchoredPosition = cellAPos);
        _moveSequences.Add(seq);
        _moveSequences.Add(seqB);
        _cellMap[cells.cellA] = tempA;
        _cellMap[cells.cellB] = tempB;
    }
}
