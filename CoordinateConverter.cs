using UnityEngine;

public static class CoordinateConverter
{
    public static bool TryWorldToPixel(Transform canvasTransform, int width, int height, int pixelsPerUnit, Vector2 worldPos, out Vector2Int pixel)
    {
        Vector2 local = canvasTransform.InverseTransformPoint(worldPos);

        int px = Mathf.FloorToInt(local.x * pixelsPerUnit) + width / 2;
        int py = Mathf.FloorToInt(local.y * pixelsPerUnit) + height / 2;

        pixel = new Vector2Int(px, py);
        return px >= 0 && px < width && py >= 0 && py < height;
    }
}
