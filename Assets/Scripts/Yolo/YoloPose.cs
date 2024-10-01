using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Sentis;
using UnityEngine;
using UnityEngine.UIElements;

public class YoloPose
{
    private float confidenceThreshold;
    private float iouThreshold;
    private ComputeShader postProcessingShader;
    public float ConfidenceThreshold { get => confidenceThreshold; set => confidenceThreshold = value; }
    public float IouThreshold { get => iouThreshold; set => iouThreshold = value; }

    public YoloPose(float confidenceThreshold = 0.5f, float iouThreshold = 0.4f)
    {
        this.confidenceThreshold = confidenceThreshold;
        this.iouThreshold = iouThreshold;
        PrepareShaders();
    }

    public List<YoloPosePrediction> Predict(Tensor<float> outputTensor, int imageWidth, int imageHeight)
    {
        ComputeTensorData computeTensorData = ComputeTensorData.Pin(outputTensor);
        if (computeTensorData == null)
        {
            Debug.LogError("Output Tensor not found!");
            return null;
        }

        int numDetections = outputTensor.shape[2];
        int numLandmarks = 3 * 17;  // OpenPose landmarks (x, y, score) * 17
        // Initialize the valid detection counter to 0
        uint[] zeroArray = { 0 };

        // Create compute buffers for input/output
        ComputeBuffer outputBoxesBuffer = new ComputeBuffer(numDetections, sizeof(float) * 4);  // Bounding boxes buffer
        ComputeBuffer outputLandmarksBuffer = new ComputeBuffer(numDetections, sizeof(float) * numLandmarks);  // Class index buffer
        ComputeBuffer outputScoresBuffer = new ComputeBuffer(numDetections, sizeof(float));  // Scores buffer
        ComputeBuffer validDetectionCounterBuffer = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.Raw);  // Counter for valid detections
        ComputeBuffer outputTensorBuffer = computeTensorData.buffer;
        // Set compute shader parameters
        int kernelHandle = postProcessingShader.FindKernel("CSMain");
        postProcessingShader.SetFloat("confidenceThreshold", confidenceThreshold);
        postProcessingShader.SetInt("numDetections", numDetections);
        postProcessingShader.SetInt("numLandmarks", numLandmarks);
        postProcessingShader.SetInt("imageWidth", imageWidth);
        postProcessingShader.SetInt("imageHeight", imageHeight);
        validDetectionCounterBuffer.SetData(zeroArray);

        // Bind buffers to the compute shader
        postProcessingShader.SetBuffer(kernelHandle, "outputTensor", outputTensorBuffer);
        postProcessingShader.SetBuffer(kernelHandle, "outputBoxes", outputBoxesBuffer);
        postProcessingShader.SetBuffer(kernelHandle, "outputLandmarks", outputLandmarksBuffer);
        postProcessingShader.SetBuffer(kernelHandle, "outputScores", outputScoresBuffer);
        postProcessingShader.SetBuffer(kernelHandle, "validDetectionCounter", validDetectionCounterBuffer);

        // Dispatch compute shader (numDetections / thread group size)
        int threadGroups = Mathf.CeilToInt((float)numDetections / 256);
        postProcessingShader.Dispatch(kernelHandle, threadGroups, 1, 1);

        // Retrieve the valid detection count from the counter buffer
        uint[] validDetectionCountArray = new uint[1];
        validDetectionCounterBuffer.GetData(validDetectionCountArray);
        int validDetectionCount = (int)validDetectionCountArray[0];

        // Retrieve results from the GPU
        float[] boxes = new float[validDetectionCount * 4];
        float[] landmarks = new float[validDetectionCount * numLandmarks]; // TODO
        float[] scores = new float[validDetectionCount];

        outputBoxesBuffer.GetData(boxes, 0, 0, validDetectionCount * 4);
        outputLandmarksBuffer.GetData(landmarks, 0, 0, validDetectionCount * numLandmarks);
        outputScoresBuffer.GetData(scores, 0, 0, validDetectionCount);

        // Convert output to YoloPosePrediction list
        List<YoloPosePrediction> predictions = new List<YoloPosePrediction>();
        for (int i = 0; i < validDetectionCount; i++)
        {
            float xMin = boxes[i * 4 + 0];
            float yMin = boxes[i * 4 + 1];
            float width = boxes[i * 4 + 2];
            float height = boxes[i * 4 + 3];

            int x = i * 17;
            int length = 17;
            float[] newArray = new float[numLandmarks];
            Array.Copy(landmarks, x, newArray, 0, length);

            YoloPosePrediction prediction = new YoloPosePrediction
            {
                Landmarks = newArray,
                Score = scores[i],
                BoundingBox = new Rect(xMin, yMin, width, height)
            };
            predictions.Add(prediction);
        }

        // Release compute buffers
        outputTensorBuffer.Release();
        outputBoxesBuffer.Release();
        outputLandmarksBuffer.Release();
        outputScoresBuffer.Release();
        validDetectionCounterBuffer.Release();
        computeTensorData.Dispose();

        // Apply class-wise Non-Max Suppression
        return ApplyNonMaxSuppression(predictions, iouThreshold);
    }

    //// Class-wise Non-Max Suppression
    //private List<YoloPosePrediction> ApplyClassWiseNonMaxSuppression(List<YoloPosePrediction> predictions, float iouThreshold)
    //{
    //    // Group the predictions by class
    //    var groupedByClass = predictions.GroupBy(p => p.ClassIndex);
    //
    //    List<YoloPosePrediction> finalPredictions = new List<YoloPosePrediction>();
    //
    //    // Apply Non-Max Suppression for each class group
    //    foreach (var classGroup in groupedByClass)
    //    {
    //        List<YoloPosePrediction> classPredictions = classGroup.ToList();
    //
    //        List<YoloPosePrediction> nmsResult = ApplyNonMaxSuppression(classPredictions, iouThreshold);
    //        finalPredictions.AddRange(nmsResult);
    //    }
    //
    //    return finalPredictions;
    //}

    // Non-Max Suppression (NMS) for a single class
    private List<YoloPosePrediction> ApplyNonMaxSuppression(List<YoloPosePrediction> predictions, float iouThreshold)
    {
        List<YoloPosePrediction> result = new List<YoloPosePrediction>();

        // Sort the predictions by confidence score (descending)
        foreach (var prediction in predictions.OrderByDescending(p => p.Score))
        {
            bool shouldSelect = true;

            // Check for overlap with already selected predictions
            foreach (var selectedPrediction in result)
            {
                float iou = CalculateIoU(prediction.BoundingBox, selectedPrediction.BoundingBox);
                if (iou > iouThreshold)
                {
                    shouldSelect = false;
                    break;
                }
            }

            if (shouldSelect)
            {
                result.Add(prediction);
            }
        }

        return result;
    }

    private float CalculateIoU(Rect boxA, Rect boxB)
    {
        float intersectionWidth = Mathf.Max(0, Mathf.Min(boxA.xMax, boxB.xMax) - Mathf.Max(boxA.xMin, boxB.xMin));
        float intersectionHeight = Mathf.Max(0, Mathf.Min(boxA.yMax, boxB.yMax) - Mathf.Max(boxA.yMin, boxB.yMin));
        float intersectionArea = intersectionWidth * intersectionHeight;

        float unionArea = boxA.width * boxA.height + boxB.width * boxB.height - intersectionArea;
        return intersectionArea / unionArea;
    }

    private void PrepareShaders()
    {
        postProcessingShader = Resources.Load<ComputeShader>("Shaders/PostProcessOutputPose");
        if (postProcessingShader == null)
        {
            Debug.LogError("Compute Shader not found!");
        }
        else
        {
            Debug.Log($"Post Processing Shader is ready.");
        }
    }
}