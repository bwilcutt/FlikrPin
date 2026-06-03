using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

public class CameraCapture : MonoBehaviour
{
    // Callbacks assigned by PostTypeWindow before calling TakeVideo() or TakePhoto()
    public System.Action<string>    OnVideoReady;
    public System.Action<Texture2D> OnPhotoReady;
    public System.Action            OnCancelled;

    private const string MediaDir    = "VidPicPost";
    private const int    MaxVideSecs = 15;

    private bool _cameraBusy = false;

    public void TakeVideo()
    {
        if (_cameraBusy) { Debug.LogWarning("CameraCapture: Camera already in use, ignoring."); return; }
        if (!NativeCamera.DeviceHasCamera())
        {
            Debug.LogWarning("CameraCapture: No camera found on device.");
            return;
        }

        _cameraBusy = true;
        Debug.Log("CameraCapture: Opening camera for video (max " + MaxVideSecs + "s).");

        NativeCamera.RecordVideo((path) =>
        {
            _cameraBusy = false;
            if (path == null) { Debug.Log("CameraCapture: Video cancelled."); OnCancelled?.Invoke(); return; }
            Debug.Log("CameraCapture: Video captured at: " + path);

            // Guard against the NativeCamera session already being closed by ARCore pause.
            if (!File.Exists(path))
            {
                Debug.LogWarning("CameraCapture: Video path does not exist, aborting: " + path);
                return;
            }

            string localPath = CopyToLocalStorage(path, "video");
            OnVideoReady?.Invoke(localPath);
        }, NativeCamera.Quality.High, MaxVideSecs);
    }

    public void TakePhoto()
    {
        if (_cameraBusy) { Debug.LogWarning("CameraCapture: Camera already in use, ignoring."); return; }
        if (!NativeCamera.DeviceHasCamera())
        {
            Debug.LogWarning("CameraCapture: No camera found on device.");
            return;
        }

        _cameraBusy = true;
        Debug.Log("CameraCapture: Opening camera for photo.");

        NativeCamera.TakePicture((path) =>
        {
            _cameraBusy = false;
            if (path == null) { Debug.Log("CameraCapture: Photo cancelled."); OnCancelled?.Invoke(); return; }
            Debug.Log("CameraCapture: Photo captured at: " + path);
            string localPath = CopyToLocalStorage(path, "photo");
            StartCoroutine(LoadTexture(localPath));
        }, maxSize: 2048);
    }

    IEnumerator LoadTexture(string path)
    {
        using (UnityWebRequest req = UnityWebRequestTexture.GetTexture("file://" + path))
        {
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning("CameraCapture: Failed to load photo texture: " + req.error);
                yield break;
            }

            Texture2D tex = DownloadHandlerTexture.GetContent(req);

            // Rotate landscape photos to portrait
            if (tex.width > tex.height)
            {
                Debug.Log("CameraCapture: Rotating landscape photo 90 degrees.");
                tex = RotateTexture90(tex);
            }

            Debug.Log("CameraCapture: Photo texture loaded OK.");
            OnPhotoReady?.Invoke(tex);
        }
    }

    Texture2D RotateTexture90(Texture2D src)
    {
        int w = src.width;
        int h = src.height;

        Texture2D rotated  = new Texture2D(h, w, src.format, false);
        Color32[] srcPixels = src.GetPixels32();
        Color32[] dstPixels = new Color32[srcPixels.Length];

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                dstPixels[(x + 1) * h - y - 1] = srcPixels[y * w + x];

        rotated.SetPixels32(dstPixels);
        rotated.Apply();
        return rotated;
    }

    string CopyToLocalStorage(string sourcePath, string type)
    {
        string dir = Path.Combine(Application.persistentDataPath, MediaDir);

        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
            Debug.Log("CameraCapture: Created directory: " + dir);
        }

        string ext      = (type == "video") ? ".mp4" : ".jpg";
        string fileName = type + "_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ext;
        string destPath = Path.Combine(dir, fileName);

        try
        {
            File.Copy(sourcePath, destPath, overwrite: true);
            Debug.Log("CameraCapture: Copied to: " + destPath);
            return destPath;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("CameraCapture: Copy failed, using original path. Error: " + e.Message);
            return sourcePath;
        }
    }
}
