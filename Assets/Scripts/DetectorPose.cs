using UnityEngine;
using Unity.Sentis;
using System.Collections.Generic;

public class DetectorPose : MonoBehaviour
{
    private const int TARGET_WIDTH = 640;
    private const int TARGET_HEIGHT = 640;

    public FileLoader fileLoader;

    // Object Detection
    private ModelAsset modelAsset;
    private Drawable screen;
    private Model runtimeModel;
    private Worker worker;
    private YoloPose yolo;
    private Source source = null;

    void Start()
    {
        // Initialise Classes
        yolo = new YoloPose();
        modelAsset = Resources.Load<ModelAsset>("Models/yolo11n-pose");
        runtimeModel = ModelLoader.Load(modelAsset);
        worker = new Worker(runtimeModel, BackendType.GPUCompute);

        fileLoader.OnSourceDetected += OnSourceChanged;
    }

    void Update()
    {
        if (source == null || source.IsProcessedOnce())
            return;

        if (source.IsFrameReady())
        {
            DetectFrame();
        }
    }

    private void OnDisable()
    {
        worker.Dispose();
    }

    public void StartDetection(float cTh, float iouTh)
    {
        yolo.IouThreshold = iouTh;
        yolo.ConfidenceThreshold = cTh;

        screen = new Drawable();
        

        if (source.IsProcessedOnce())
        {
            DetectFrame();
        } else
        {
            source.Play();
        }
    }
    void OnSourceChanged(SourceType sourceType, string path)
    {
        if (sourceType == SourceType.ImageSource) {
            source = new ImageSource(path);
        }
        else if (sourceType == SourceType.VideoSource)
        {
            source = new VideoSource(path);
        } else
        {
            source = new CameraSource();
        }
    }
    private void DetectFrame()
    {
        // Get the newly generated texture
        Texture texture = source.GetTexture();

        // Remove the old bounding boxes
        screen.ResetBoundingBoxes();

        // Display the texture
        screen.SetTexture(texture);

        // Prepare the input tensor
        Tensor<float> inputTensor = TextureConverter.ToTensor(texture, TARGET_WIDTH, TARGET_HEIGHT, 3);

        // Run the model on the input
        worker.Schedule(inputTensor);

        // Get output tensor
        Tensor<float> outputTensor = worker.PeekOutput() as Tensor<float>;

        // Process Model Output
        List<YoloPosePrediction> predictions = yolo.Predict(outputTensor, TARGET_WIDTH, TARGET_HEIGHT);

        // Draw the new bounding boxes 
        screen.DrawBoundingBoxes(predictions);

        if (predictions.Count != 0)
        {
            Debug.Log("Landmark 1 x: " + predictions[0].Landmarks[0]);
            Debug.Log("Landmark 1 y: " + predictions[0].Landmarks[1]);
            Debug.Log("Landmark 1 score: " + predictions[0].Landmarks[2]);
        }

        // Dispose tensors
        outputTensor.Dispose();
        inputTensor.Dispose();
    }
}


