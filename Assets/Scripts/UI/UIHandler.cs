using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIHandler : MonoBehaviour
{
    [SerializeField] private Button startDetectionButton;
    [SerializeField] private Button openFileSelectonButton;
    [SerializeField] private TMP_InputField confidenceThreshold;
    [SerializeField] private TMP_InputField iouThreshold;
    [SerializeField] private TMP_Dropdown sourceTypeSelector;
    [SerializeField] private DetectorPose detector;
    [SerializeField] private FileLoader fileLoader;
    [SerializeField] private GameObject userInterface;
    [SerializeField] private GameObject display;
    [SerializeField] private GameObject fileSelectorPrefab;

    private List<SourceType> sourceTypes = new List<SourceType> { SourceType.ImageSource, SourceType.CameraSource, SourceType.VideoSource };

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Ensure the button is assigned
        if (startDetectionButton != null)
        {
            // Add a listener to the button
            startDetectionButton.onClick.AddListener(OnStartDetectionButtonClick);
        }

        // Ensure the button is assigned
        if (openFileSelectonButton != null)
        {
            // Add a listener to the button
            openFileSelectonButton.onClick.AddListener(OnOpenFileSelectonButton);
        }

        if (sourceTypeSelector != null)
        {
            sourceTypeSelector.onValueChanged.AddListener(OnSourceTypeChanged);
        }
    }

    private void OnStartDetectionButtonClick()
    {
        float cTh = float.Parse(confidenceThreshold.text);
        float iouTh = float.Parse(iouThreshold.text);
        detector.StartDetection(cTh, iouTh);
        userInterface.SetActive(false);
        display.SetActive(true);
    }

    private void OnSourceTypeChanged(int value)
    {
        var sourceType = sourceTypes[value];
        if ((sourceType == SourceType.CameraSource) && (openFileSelectonButton.gameObject.activeSelf))
        {
            openFileSelectonButton.gameObject.SetActive(false);
        } else if ( ((sourceType == SourceType.VideoSource) || (sourceType == SourceType.ImageSource)) && (!openFileSelectonButton.gameObject.activeSelf) )
        {
            openFileSelectonButton.gameObject.SetActive(true);
        }
        fileLoader.SetDefaultFilter(sourceType);
    }

    private void OnOpenFileSelectonButton()
    {
        fileSelectorPrefab.SetActive(true);
        fileLoader.OpenFileBrowser();
    }

}
