using System;
using UnityEngine;

[Serializable]
public class Cell
{
    public Cell(int x, int y, Color color)
    {
        coordinates = new Vector2(x, y);
        this.color = color;
    }

    public Vector2 coordinates;
    public Color color;

    public Action<Cell> onClick;

    public void OnInteract()
    {
        onClick?.Invoke(this);
    }
}
