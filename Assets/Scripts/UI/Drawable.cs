using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Drawable
{
    // Object Pooling
    private ObjectPool<BoundingBox> boundingBoxPool;
    private RawImage screen;
    private RectTransform screenRectTransform;
    private List<BoundingBox> activeBoundingBoxes;
    private GameObject boundingBoxPrefab;
    private float screenWidth;
    private float screenHeight;

    public Drawable()
    {
        screen = GameObject.Find("Display").GetComponent<RawImage>();
        PrepareBoundingBoxPool();
    }

    public void SetTexture(Texture texture)
    {
        if (texture == null)
        {
            Debug.LogError("The given texture is null");
            return;
        }

        screen.texture = texture;
    }
    
    public void DrawBoundingBoxes(List<YoloPrediction> yoloPredictions)
    {
        // Calculate the offset for center-middle anchoring
        float offsetX = screenWidth / 2;
        float offsetY = screenHeight / 2;

        foreach (YoloPrediction prediction in yoloPredictions)
        {
            // Get a bounding box from the pool
            BoundingBox boundingBox = boundingBoxPool.Get();
            activeBoundingBoxes.Add(boundingBox);

            // Get the RectTransform of the bounding box
            RectTransform boxRectTransform = boundingBox.GetComponent<RectTransform>();

            var predBoundingBox = prediction.BoundingBox;

            // Calculate the box's position relative to the center of the image (center-middle anchoring)
            float xMin = predBoundingBox.xMin - offsetX;
            float yMin = predBoundingBox.yMin - offsetY;
            float width = predBoundingBox.width;
            float height = predBoundingBox.height;

            // Set the size and position of the bounding box
            boxRectTransform.anchoredPosition = new Vector2(xMin + width / 2, yMin + height / 2); // Center the box
            boxRectTransform.sizeDelta = new Vector2(width, height);

            // Update the bounding box appearance
            boundingBox.SetColor(prediction.ClassColor);
            boundingBox.SetLabel($"{prediction.ClassName} ({prediction.Score:F2})");
        }
    }

    public void DrawBoundingBoxes(List<YoloPosePrediction> yoloPosePredictions)
    {
        // Calculate the offset for center-middle anchoring
        float offsetX = screenWidth / 2;
        float offsetY = screenHeight / 2;

        foreach (YoloPosePrediction prediction in yoloPosePredictions)
        {
            // Get a bounding box from the pool
            BoundingBox boundingBox = boundingBoxPool.Get();
            activeBoundingBoxes.Add(boundingBox);

            // Get the RectTransform of the bounding box
            RectTransform boxRectTransform = boundingBox.GetComponent<RectTransform>();

            var predBoundingBox = prediction.BoundingBox;

            // Calculate the box's position relative to the center of the image (center-middle anchoring)
            float xMin = predBoundingBox.xMin - offsetX;
            float yMin = predBoundingBox.yMin - offsetY;
            float width = predBoundingBox.width;
            float height = predBoundingBox.height;

            // Set the size and position of the bounding box
            boxRectTransform.anchoredPosition = new Vector2(xMin + width / 2, yMin + height / 2); // Center the box
            boxRectTransform.sizeDelta = new Vector2(width, height);

            // Update the bounding box appearance
            boundingBox.SetColor(new Color(0.0f, 1.0f, 0.0f, 0.25f));
            boundingBox.SetLabel($"Human ({prediction.Score:F2})");
        }
    }

    public void ResetBoundingBoxes()
    {
        foreach (BoundingBox box in activeBoundingBoxes)
        {
            boundingBoxPool.ReturnToPool(box);
        }
        activeBoundingBoxes.Clear();
    }

    private void PrepareBoundingBoxPool()
    {
        activeBoundingBoxes = new List<BoundingBox>();
        screenRectTransform = screen.GetComponent<RectTransform>();
        // TODO: Try folder path 
        boundingBoxPrefab = Resources.Load<GameObject>("Prefabs/BBox");

        if (boundingBoxPrefab != null)
        {
            screenWidth = screenRectTransform.rect.width;
            screenHeight = screenRectTransform.rect.height;
            boundingBoxPool = new ObjectPool<BoundingBox>(
                boundingBoxPrefab.GetComponent<BoundingBox>(),
                initialSize: 10,
                parent: screenRectTransform
            );
        }
    }
}
