using UniRx;
using UniRx.Triggers;
using UnityEngine;
using UnityEngine.UI;

public class CellView : MonoBehaviour
{
    [SerializeField] private Image img;
    [SerializeField] private RectTransform rectTransform;

    public RectTransform RectTransform => rectTransform;
    private Cell CellReference { get; set; }

    public void Initialize(Cell cell)
    {
        CellReference = cell;
        img.color = cell.color;
    }

    private void Awake()
    {
        img.OnPointerClickAsObservable().Subscribe(_ => CellReference.OnInteract()).AddTo(this);
    }
}
