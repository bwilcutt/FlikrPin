// =============================================================================
// File:        CameraCapture.cs
// Author:      Bryan Wilcutt
// Date Started: (original)
// Description: Wraps NativeCamera to provide video recording and photo capture
//              for FlikrPin. Copies captured media to persistent local storage
//              under VidPicPost/. Invokes caller-supplied callbacks on
//              completion or cancellation.
//
//              Callers assign OnVideoReady, OnPhotoReady, and OnCancelled
//              before calling TakeVideo() or TakePhoto().
// =============================================================================

using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

public class CameraCapture : MonoBehaviour
{
    // Callbacks assigned by the caller before invoking TakeVideo() or TakePhoto()
    public System.Action<string>    OnVideoReady;
    public System.Action<Texture2D> OnPhotoReady;
    public System.Action            OnCancelled;

    private const string MediaDir    = "VidPicPost";
    private const int    MaxVideoSecs = 15;

    private bool _cameraBusy = false;

    // -------------------------------------------------------------------------
    // Function:    TakeVideo
    // Inputs:      None
    // Outputs:     None (result delivered via OnVideoReady or OnCancelled)
    // Description: Opens the device camera for video recording up to MaxVideoSecs.
    //              Copies the result to persistent storage and invokes OnVideoReady
    //              with the local path. Invokes OnCancelled if the user cancels.
    // -------------------------------------------------------------------------
    public void TakeVideo()
    {
        if (_cameraBusy) return;
        if (!NativeCamera.DeviceHasCamera()) return;

        _cameraBusy = true;

        NativeCamera.RecordVideo((path) =>
        {
            _cameraBusy = false;

            if (path == null)
            {
                OnCancelled?.Invoke();
                return;
            }

            if (!File.Exists(path)) return;

            string localPath = CopyToLocalStorage(path, "video");
            OnVideoReady?.Invoke(localPath);

        }, NativeCamera.Quality.High, MaxVideoSecs);
    }

    // -------------------------------------------------------------------------
    // Function:    TakePhoto
    // Inputs:      None
    // Outputs:     None (result delivered via OnPhotoReady or OnCancelled)
    // Description: Opens the device camera for photo capture at up to 2048px.
    //              Copies the result to persistent storage, rotates landscape
    //              images to portrait, and invokes OnPhotoReady with the texture.
    //              Invokes OnCancelled if the user cancels.
    // -------------------------------------------------------------------------
    public void TakePhoto()
    {
        if (_cameraBusy) return;
        if (!NativeCamera.DeviceHasCamera()) return;

        _cameraBusy = true;

        NativeCamera.TakePicture((path) =>
        {
            _cameraBusy = false;

            if (path == null)
            {
                OnCancelled?.Invoke();
                return;
            }

            string localPath = CopyToLocalStorage(path, "photo");
            StartCoroutine(LoadTexture(localPath));

        }, maxSize: 2048);
    }

    // -------------------------------------------------------------------------
    // Function:    LoadTexture
    // Inputs:      path — local file path to the captured photo
    // Outputs:     None (result delivered via OnPhotoReady)
    // Description: Loads the photo at path as a Texture2D via UnityWebRequest.
    //              Rotates landscape images 90° to portrait before invoking
    //              OnPhotoReady.
    // -------------------------------------------------------------------------
    IEnumerator LoadTexture(string path)
    {
        using (UnityWebRequest req = UnityWebRequestTexture.GetTexture("file://" + path))
        {
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success) yield break;

            Texture2D tex = DownloadHandlerTexture.GetContent(req);

            if (tex.width > tex.height)
                tex = RotateTexture90(tex);

            OnPhotoReady?.Invoke(tex);
        }
    }

    // -------------------------------------------------------------------------
    // Function:    RotateTexture90
    // Inputs:      src — source Texture2D to rotate
    // Outputs:     Texture2D — new texture rotated 90° clockwise
    // Description: Creates a new texture with width and height swapped,
    //              remapping each pixel to its rotated position.
    // -------------------------------------------------------------------------
    Texture2D RotateTexture90(Texture2D src)
    {
        int w = src.width;
        int h = src.height;

        Texture2D rotated   = new Texture2D(h, w, src.format, false);
        Color32[] srcPixels = src.GetPixels32();
        Color32[] dstPixels = new Color32[srcPixels.Length];

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                dstPixels[(x + 1) * h - y - 1] = srcPixels[y * w + x];

        rotated.SetPixels32(dstPixels);
        rotated.Apply();
        return rotated;
    }

    // -------------------------------------------------------------------------
    // Function:    CopyToLocalStorage
    // Inputs:      sourcePath — path returned by NativeCamera
    //              type       — "video" or "photo" (determines extension)
    // Outputs:     string — destination path in persistent storage, or
    //                       sourcePath if the copy fails
    // Description: Creates the VidPicPost directory if needed, then copies the
    //              captured file to a timestamped filename in persistent storage.
    // -------------------------------------------------------------------------
    string CopyToLocalStorage(string sourcePath, string type)
    {
        string dir = Path.Combine(Application.persistentDataPath, MediaDir);

        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        string ext      = (type == "video") ? ".mp4" : ".jpg";
        string fileName = type + "_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ext;
        string destPath = Path.Combine(dir, fileName);

        try
        {
            File.Copy(sourcePath, destPath, overwrite: true);
            return destPath;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("CameraCapture: Copy failed — " + e.Message);
            return sourcePath;
        }
    }
}
