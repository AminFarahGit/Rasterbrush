using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public enum DrawTool
{
    EyeDropper,
    Brush,
    FloodFill,
    Eraser
}

[RequireComponent(typeof(SpriteRenderer))]
public class DrawingCanvas : MonoBehaviour
{
    [Header("Canvas Settings"), Space(5)]
    [SerializeField] private int width = 800;
    [SerializeField] private int height = 600;
    [SerializeField] private Color32 backgroundColor = Color.white;
    [SerializeField] private int pixelsPerUnit = 100;
    
    [Header("Drawing Settings"), Space(5)]
    [SerializeField] private DrawTool tool = DrawTool.Brush;
    [SerializeField, Range(0, 100)] private int toolSize = 3;
    [SerializeField] private Color32 toolColor = Color.red;
    
    private Color32[] buffer;
    Texture2D texture;
    private bool dirty;
    private bool strokingIt;
    private Vector2Int lastPixel;

    void Awake()
    {
        buffer = new Color32[width * height]; //Initialize Texture2D Buffer
        Clear();
        
        //Create Texture2D, tweak its settings and display it with the SpriteRenderer
        texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;   
        GetComponent<SpriteRenderer>().sprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), pixelsPerUnit);
    }

    void Update()
    {
        Vector2 mouseWorldPosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        bool onCanvas = CoordinateConverter.TryWorldToPixel(transform, width, height,
            pixelsPerUnit, mouseWorldPosition, out Vector2Int p);

        // Register start of strokes so that we can lerp between start and end stroke for missed pixels (mouse moving too fast)
        if (Input.GetMouseButtonDown(0))
        {
            if (tool == DrawTool.Brush || tool == DrawTool.Eraser)
            {
                StampBrush(p.x, p.y, toolSize, StrokeColor());   // clips itself if off-edge
                lastPixel = p;
                strokingIt = true;
            }
            else if (tool == DrawTool.EyeDropper && onCanvas)
            {
                toolColor = ReadPixel(p);
            }else if (tool == DrawTool.FloodFill && onCanvas)
            {
                FloodFill(p.x, p.y, toolColor);
            }
        }
        
        // Continue lerping stroke between previous pixel and current pixel
        else if (strokingIt && Input.GetMouseButton(0))
        {
            if (tool == DrawTool.Brush || tool == DrawTool.Eraser)
            {
                if (SegmentNearCanvas(lastPixel, p, toolSize))
                    StampLine(lastPixel, p, toolSize, StrokeColor());
                lastPixel = p;
            }
        }

        // Stroke ends
        if (strokingIt && Input.GetMouseButtonUp(0)) strokingIt = false;
    }
    
    void LateUpdate()
    {
        if (!dirty) return; //Check if buffer is dirty
        
        texture.SetPixels32(buffer); //Update Texture2D with buffer
        texture.Apply(); //Apply to GPU rendering
        dirty = false;
    }

    void Clear()
    {
        for (int i = 0; i < buffer.Length; i++)
        {
            buffer[i] = backgroundColor;
        }
        
        dirty = true;
    }
    
    
    //Draw pixels in a circular brush shape
    public void StampBrush(int cx, int cy, int radius, Color32 color)
    {
        int r2 = radius * radius;
        for (int dy = -radius; dy <= radius; dy++)
        for (int dx = -radius; dx <= radius; dx++)
        {
            if (dx * dx + dy * dy > r2) continue;
            WritePixel(cx + dx, cy + dy, color);
        }
    }
    
    
    //Lerps between previous pixels to create smooth lines with no cuts (good for low FPS)
    public void StampLine(Vector2Int from, Vector2Int to, int radius, Color32 color)
    {
        float dist = Vector2Int.Distance(from, to);
        int steps = Mathf.CeilToInt(dist);

        for (int i = 0; i <= steps; i++)
        {
            float t = steps == 0 ? 0f : (float)i / steps;
            Vector2 pixel = Vector2.Lerp((Vector2) from, (Vector2) to, t);
            StampBrush(Mathf.RoundToInt(pixel.x), Mathf.RoundToInt(pixel.y), radius, color);
        }
    }
    
    
    //Helper function to write to individual pixels
    void WritePixel(int x, int y, Color32 c)
    {
        if (x < 0 || x >= width || y < 0 || y >= height) return;
        buffer[y * width + x] = c;
        dirty = true;
    }
    
    
    //Helper function to read from individual pixels
    Color32 ReadPixel(int x, int y)
    {
        if (x < 0 || x >= width || y < 0 || y >= height) return backgroundColor;
        return buffer[y * width + x];
    }
    
    
    //Alternative version for my own brain
    Color32 ReadPixel(Vector2Int pixel)
    {
        if (pixel.x < 0 || pixel.x >= width || pixel.y < 0 || pixel.y >= height) return backgroundColor;
        return buffer[pixel.y * width + pixel.x];
    }
    
    
    //Detect if you are off canvas but close to lerp towards edge of canvas
    bool SegmentNearCanvas(Vector2Int a, Vector2Int b, int radius)
    {
        int minX = Mathf.Min(a.x, b.x) - radius;
        int maxX = Mathf.Max(a.x, b.x) + radius;
        int minY = Mathf.Min(a.y, b.y) - radius;
        int maxY = Mathf.Max(a.y, b.y) + radius;
        return minX < width && maxX >= 0 && minY < height && maxY >= 0;
    }
    
    
    //Flood fill algorithm basic graph traversal, wouldn't work recursive Untiy has limits on stack size and our canvas is pretty big by default
    public void FloodFill(int x, int y, Color32 fill)
    {
        if (x < 0 || x >= width || y < 0 || y >= height) return;

        Color32 target = buffer[y * width + x];
        if (Same(target, fill)) return;                 

        var queue = new Queue<int>();
        queue.Enqueue(y * width + x);

        while (queue.Count > 0)
        {
            int i = queue.Dequeue();
            if (!Same(buffer[i], target)) continue;      //already painted or boundary
            buffer[i] = fill;

            int px = i % width, py = i / width;
            if (px > 0)          queue.Enqueue(i - 1);
            if (px < width - 1)  queue.Enqueue(i + 1);
            if (py > 0)          queue.Enqueue(i - width);
            if (py < height - 1) queue.Enqueue(i + width);
        }
        dirty = true;
    }
    
    
    //Extra bools I use in some functions
    static bool Same(Color32 a, Color32 b) =>
        a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;
    
    Color32 StrokeColor() => tool == DrawTool.Eraser ? backgroundColor : toolColor;
    

    
}
