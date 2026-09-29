using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ZXing;
using ZXing.Common;

public class QRScanner : MonoBehaviour
{
    [Header("Camera")]
    public RawImage cameraPreview;

    [Header("QR Result")]
    public TMP_Text resultText;

    [Header("Buttons")]
    public Button scanAgainButton;
    public Button copyButton;
    public Button openLinkButton;

    [Header("Camera Settings")]
    public int requestedWidth = 1280;
    public int requestedHeight = 720;
    public int requestedFPS = 30;

    [Header("Mobile UI")]
    public bool arrangeButtonsAutomatically = true;

    private WebCamTexture webcamTexture;
    private BarcodeReaderGeneric barcodeReader;

    private bool scanning = false;

    private string scannedText = "";

    // =========================================================
    // START
    // =========================================================

    private void Start()
    {
        Debug.Log("=== QR SCANNER STARTING ===");

        SetupButtons();

        HideResultButtons();

        if (resultText != null)
        {
            resultText.text = "Waiting for QR code...";
        }

        StartCamera();
    }

    // =========================================================
    // SETUP BUTTONS
    // =========================================================

    private void SetupButtons()
    {
        if (scanAgainButton != null)
        {
            scanAgainButton.onClick.RemoveAllListeners();
            scanAgainButton.onClick.AddListener(StartScanning);
        }

        if (copyButton != null)
        {
            copyButton.onClick.RemoveAllListeners();
            copyButton.onClick.AddListener(CopyQRText);
        }

        if (openLinkButton != null)
        {
            openLinkButton.onClick.RemoveAllListeners();
            openLinkButton.onClick.AddListener(OpenQRLink);
        }
    }

    // =========================================================
    // HIDE BUTTONS
    // =========================================================

    private void HideResultButtons()
    {
        if (scanAgainButton != null)
        {
            scanAgainButton.gameObject.SetActive(false);
        }

        if (copyButton != null)
        {
            copyButton.gameObject.SetActive(false);
        }

        if (openLinkButton != null)
        {
            openLinkButton.gameObject.SetActive(false);
        }
    }

    // =========================================================
    // SHOW BUTTONS
    // =========================================================

    private void ShowResultButtons()
    {
        if (scanAgainButton != null)
        {
            scanAgainButton.gameObject.SetActive(true);
        }

        if (copyButton != null)
        {
            copyButton.gameObject.SetActive(true);
        }

        if (openLinkButton != null)
        {
            openLinkButton.gameObject.SetActive(IsUrl(scannedText));
        }

        if (arrangeButtonsAutomatically)
        {
            ArrangeButtonsForMobile();
        }
    }

    // =========================================================
    // CAMERA PERMISSION
    // =========================================================

    private void StartCamera()
    {
        if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
        {
            StartCoroutine(RequestCameraPermission());
            return;
        }

        InitializeCamera();
    }

    private IEnumerator RequestCameraPermission()
    {
        Debug.Log("Requesting camera permission...");

        yield return Application.RequestUserAuthorization(
            UserAuthorization.WebCam
        );

        if (Application.HasUserAuthorization(UserAuthorization.WebCam))
        {
            Debug.Log("Camera permission granted.");
            InitializeCamera();
        }
        else
        {
            Debug.LogError("Camera permission denied.");

            if (resultText != null)
            {
                resultText.text = "Camera permission denied.";
            }
        }
    }

    // =========================================================
    // INITIALIZE CAMERA
    // =========================================================

    private void InitializeCamera()
    {
        WebCamDevice[] devices = WebCamTexture.devices;

        Debug.Log("Number of cameras found: " + devices.Length);

        if (devices.Length == 0)
        {
            Debug.LogError("NO CAMERA FOUND.");

            if (resultText != null)
            {
                resultText.text = "No camera found.";
            }

            return;
        }

        string cameraName = devices[0].name;

        Debug.Log("Using camera: " + cameraName);

        webcamTexture = new WebCamTexture(
            cameraName,
            requestedWidth,
            requestedHeight,
            requestedFPS
        );

        if (cameraPreview == null)
        {
            Debug.LogError("Camera Preview is NOT assigned!");

            if (resultText != null)
            {
                resultText.text = "Camera Preview is not assigned.";
            }

            return;
        }

        cameraPreview.gameObject.SetActive(true);

        cameraPreview.texture = webcamTexture;
        cameraPreview.color = Color.white;

        webcamTexture.Play();

        barcodeReader = new BarcodeReaderGeneric();

        StartCoroutine(WaitForCamera());
    }

    // =========================================================
    // WAIT FOR CAMERA
    // =========================================================

    private IEnumerator WaitForCamera()
    {
        Debug.Log("Waiting for webcam...");

        float timeout = 15f;

        while (
            webcamTexture != null &&
            webcamTexture.width < 100 &&
            timeout > 0
        )
        {
            timeout -= Time.deltaTime;

            yield return null;
        }

        if (webcamTexture == null)
        {
            Debug.LogError("WebcamTexture is NULL.");
            yield break;
        }

        if (webcamTexture.width < 100)
        {
            Debug.LogError(
                "Camera failed to initialize. " +
                "Width = " + webcamTexture.width +
                ", Height = " + webcamTexture.height
            );

            if (resultText != null)
            {
                resultText.text = "Camera failed to initialize.";
            }

            yield break;
        }

        Debug.Log(
            "CAMERA READY: " +
            webcamTexture.width +
            " x " +
            webcamTexture.height
        );

        Debug.Log(
            "Rotation: " +
            webcamTexture.videoRotationAngle
        );

        Debug.Log(
            "Mirrored: " +
            webcamTexture.videoVerticallyMirrored
        );

        scanning = true;

        UpdateCameraPreview();

        if (resultText != null)
        {
            resultText.text =
                "Point camera at a QR code...";
        }

        Debug.Log("=== QR SCANNER READY ===");
    }

    // =========================================================
    // UPDATE
    // =========================================================

    private void Update()
    {
        if (!scanning)
            return;

        if (webcamTexture == null)
            return;

        if (!webcamTexture.isPlaying)
            return;

        if (webcamTexture.width < 100)
            return;

        UpdateCameraPreview();

        // Scan every 5 frames
        if (Time.frameCount % 5 != 0)
            return;

        ScanQRCode();
    }

    // =========================================================
    // QR SCANNING
    // =========================================================

    private void ScanQRCode()
    {
        try
        {
            if (barcodeReader == null)
            {
                barcodeReader = new BarcodeReaderGeneric();
            }

            Color32[] pixels = webcamTexture.GetPixels32();

            if (pixels == null || pixels.Length == 0)
                return;

            int width = webcamTexture.width;
            int height = webcamTexture.height;

            if (width <= 0 || height <= 0)
                return;

            // -------------------------------------------------
            // Convert Color32[] to RGB24
            // -------------------------------------------------

            byte[] rgbBytes = new byte[pixels.Length * 3];

            for (int i = 0; i < pixels.Length; i++)
            {
                rgbBytes[i * 3] = pixels[i].r;
                rgbBytes[i * 3 + 1] = pixels[i].g;
                rgbBytes[i * 3 + 2] = pixels[i].b;
            }

            // -------------------------------------------------
            // NEW ZXING API
            // -------------------------------------------------

            RGBLuminanceSource source =
                new RGBLuminanceSource(
                    rgbBytes,
                    width,
                    height,
                    RGBLuminanceSource.BitmapFormat.RGB24
                );

            // -------------------------------------------------
            // DECODE
            // -------------------------------------------------

            Result result = barcodeReader.Decode(source);

            if (result != null)
            {
                Debug.Log("==============================");
                Debug.Log("QR CODE FOUND!");
                Debug.Log("TEXT: " + result.Text);
                Debug.Log("FORMAT: " + result.BarcodeFormat);
                Debug.Log("==============================");

                scanning = false;

                OnQRCodeFound(result.Text);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning(
                "QR scan error: " + e.Message
            );
        }
    }

    // =========================================================
    // QR RESULT
    // =========================================================

    private void OnQRCodeFound(string text)
    {
        scannedText = text;

        Debug.Log(
            "SCANNED QR TEXT: " +
            scannedText
        );

        if (resultText != null)
        {
            resultText.text = scannedText;
        }

        ShowResultButtons();
    }

    // =========================================================
    // COPY QR TEXT
    // =========================================================

    public void CopyQRText()
    {
        if (string.IsNullOrEmpty(scannedText))
        {
            Debug.LogWarning("Nothing to copy.");
            return;
        }

        GUIUtility.systemCopyBuffer = scannedText;

        Debug.Log(
            "QR TEXT COPIED: " +
            scannedText
        );
    }

    // =========================================================
    // OPEN QR LINK
    // =========================================================

    public void OpenQRLink()
    {
        if (string.IsNullOrEmpty(scannedText))
        {
            Debug.LogWarning("No QR text available.");
            return;
        }

        if (!IsUrl(scannedText))
        {
            Debug.LogWarning(
                "QR result is not a valid URL: " +
                scannedText
            );

            return;
        }

        string url = scannedText.Trim();

        if (
            !url.StartsWith("http://") &&
            !url.StartsWith("https://")
        )
        {
            url = "https://" + url;
        }

        Debug.Log(
            "OPENING URL: " +
            url
        );

        Application.OpenURL(url);
    }

    // =========================================================
    // CHECK URL
    // =========================================================

    private bool IsUrl(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        return
            text.StartsWith("http://") ||
            text.StartsWith("https://") ||
            text.StartsWith("www.");
    }

    // =========================================================
    // START SCANNING AGAIN
    // =========================================================

    public void StartScanning()
    {
        if (
            webcamTexture != null &&
            webcamTexture.isPlaying
        )
        {
            scannedText = "";

            scanning = true;

            HideResultButtons();

            if (resultText != null)
            {
                resultText.text =
                    "Point camera at a QR code...";
            }

            Debug.Log("QR scanning restarted.");
        }
    }

    // =========================================================
    // CAMERA PREVIEW
    // =========================================================

    private void UpdateCameraPreview()
    {
        if (webcamTexture == null)
            return;

        if (cameraPreview == null)
            return;

        cameraPreview.rectTransform.localEulerAngles =
            new Vector3(
                0,
                0,
                -webcamTexture.videoRotationAngle
            );

        if (webcamTexture.videoVerticallyMirrored)
        {
            cameraPreview.uvRect =
                new Rect(
                    0,
                    1,
                    1,
                    -1
                );
        }
        else
        {
            cameraPreview.uvRect =
                new Rect(
                    0,
                    0,
                    1,
                    1
                );
        }
    }

    // =========================================================
    // MOBILE BUTTON LAYOUT
    // =========================================================

    private void ArrangeButtonsForMobile()
    {
        float screenWidth = Screen.width;

        float buttonWidth;

        if (screenWidth < 600)
        {
            buttonWidth = 220f;
        }
        else
        {
            buttonWidth = 260f;
        }

        SetButtonWidth(scanAgainButton, buttonWidth);
        SetButtonWidth(copyButton, buttonWidth);
        SetButtonWidth(openLinkButton, buttonWidth);
    }

    private void SetButtonWidth(
        Button button,
        float width
    )
    {
        if (button == null)
            return;

        RectTransform rect =
            button.GetComponent<RectTransform>();

        if (rect == null)
            return;

        Vector2 size = rect.sizeDelta;

        size.x = width;

        rect.sizeDelta = size;
    }

    // =========================================================
    // CLEANUP
    // =========================================================

    private void OnDestroy()
    {
        scanning = false;

        if (webcamTexture != null)
        {
            if (webcamTexture.isPlaying)
            {
                webcamTexture.Stop();
            }

            webcamTexture = null;
        }
    }
}