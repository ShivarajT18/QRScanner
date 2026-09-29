using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class CameraController : MonoBehaviour
{
    public RawImage cameraPreview;

    private WebCamTexture webCamTexture;

    IEnumerator Start()
    {
        // Request camera permission on Android
        if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
        {
            yield return Application.RequestUserAuthorization(
                UserAuthorization.WebCam
            );
        }

        if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
        {
            Debug.LogError("Camera permission was denied.");
            yield break;
        }

        // Find the rear-facing camera
        WebCamDevice[] devices = WebCamTexture.devices;

        if (devices.Length == 0)
        {
            Debug.LogError("No camera found.");
            yield break;
        }

        string cameraName = devices[0].name;

        foreach (WebCamDevice device in devices)
        {
            if (!device.isFrontFacing)
            {
                cameraName = device.name;
                break;
            }
        }

        // Start camera
        webCamTexture = new WebCamTexture(cameraName, 1280, 720, 30);

        cameraPreview.texture = webCamTexture;

        webCamTexture.Play();

        Debug.Log("Camera started: " + cameraName);
    }

    private void OnDestroy()
    {
        if (webCamTexture != null && webCamTexture.isPlaying)
        {
            webCamTexture.Stop();
        }
    }
}