using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Runs at app startup and copies all stickers from StreamingAssets/Stickers/
/// to persistentDataPath/Stickers/, overwriting existing files.
/// Supports subdirectories for categories.
///
/// Manifest format (one entry per line):
///   ChaChing.png              <- goes to Stickers/
///   Money/Dollar.png          <- goes to Stickers/Money/
///   Fun/StarBurst.png         <- goes to Stickers/Fun/
/// </summary>
public class StickerCopier : MonoBehaviour
{
    private const string ManifestFile = "manifest.txt";
    public static bool CopyComplete { get; private set; } = false;

    void Start()
    {
        CopyComplete = false;
        StartCoroutine(CopyStickers());
    }

    IEnumerator CopyStickers()
    {
        string destRoot = Path.Combine(Application.persistentDataPath, "Stickers");
        if (!Directory.Exists(destRoot))
            Directory.CreateDirectory(destRoot);

        // Read manifest
        string manifestPath = Application.streamingAssetsPath + "/Stickers/" + ManifestFile;
        Debug.Log("StickerCopier: Reading manifest from " + manifestPath);

        UnityWebRequest mReq = UnityWebRequest.Get(manifestPath);
        mReq.SendWebRequest();
        float elapsed = 0f;
        while (!mReq.isDone)
        {
            elapsed += Time.deltaTime;
            if (elapsed > 10f)
            {
                mReq.Abort();
                Debug.LogWarning("StickerCopier: Manifest request timed out.");
                CopyComplete = true;
                yield break;
            }
            yield return null;
        }

        if (mReq.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning("StickerCopier: Could not read manifest — " + mReq.error);
            CopyComplete = true;
            yield break;
        }

        string[] lines = mReq.downloadHandler.text.Split(
            new char[] { '\n', '\r' },
            System.StringSplitOptions.RemoveEmptyEntries);

        Debug.Log("StickerCopier: Found " + lines.Length + " stickers to copy.");

        foreach (string raw in lines)
        {
            string entry    = raw.Trim();
            if (string.IsNullOrEmpty(entry)) continue;

            // entry may be "ChaChing.png" or "Money/Dollar.png"
            string src      = Application.streamingAssetsPath + "/Stickers/" + entry;
            string destFile = Path.Combine(destRoot, entry.Replace('/', Path.DirectorySeparatorChar));
            string destDir  = Path.GetDirectoryName(destFile);

            // Create subdirectory if needed
            if (!Directory.Exists(destDir))
            {
                Directory.CreateDirectory(destDir);
                Debug.Log("StickerCopier: Created directory " + destDir);
            }

            Debug.Log("StickerCopier: Copying " + entry);

            UnityWebRequest req = UnityWebRequest.Get(src);
            req.SendWebRequest();
            elapsed = 0f;
            while (!req.isDone)
            {
                elapsed += Time.deltaTime;
                if (elapsed > 10f)
                {
                    req.Abort();
                    Debug.LogWarning("StickerCopier: Timed out copying " + entry);
                    break;
                }
                yield return null;
            }

            if (req.result == UnityWebRequest.Result.Success)
            {
                File.WriteAllBytes(destFile, req.downloadHandler.data);
                Debug.Log("StickerCopier: Copied " + entry + " OK.");
            }
            else
                Debug.LogWarning("StickerCopier: Failed to copy " + entry + " — " + req.error);
        }

        Debug.Log("StickerCopier: All done.");
        CopyComplete = true;
    }
}
