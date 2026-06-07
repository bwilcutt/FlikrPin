// =============================================================================
// File:        StickerCopier.cs
// Author:      Bryan Wilcutt
// Date Started: 06/06/2026
// Description: Runs at app startup and copies all stickers listed in a manifest
//              file from StreamingAssets/Stickers/ to persistentDataPath/Stickers/,
//              overwriting existing files. Supports subdirectories for categories.
//
//              Manifest format (one entry per line):
//                ChaChing.png           <- copied to Stickers/
//                Money/Dollar.png       <- copied to Stickers/Money/
//                Fun/StarBurst.png      <- copied to Stickers/Fun/
//
//              StickerPickerWindow polls CopyComplete before showing stickers.
// =============================================================================

using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

public class StickerCopier : MonoBehaviour
{
    private const string ManifestFile = "manifest.txt";

    // Set to true once all sticker files have been copied — polled by StickerPickerWindow
    public static bool CopyComplete { get; private set; } = false;

    // -------------------------------------------------------------------------
    // Function:    Start
    // Inputs:      None
    // Outputs:     None
    // Description: Resets CopyComplete and kicks off the sticker copy coroutine.
    // -------------------------------------------------------------------------
    void Start()
    {
        CopyComplete = false;
        StartCoroutine(CopyStickers());
    }

    // -------------------------------------------------------------------------
    // Function:    CopyStickers
    // Inputs:      None
    // Outputs:     None (sets CopyComplete = true on finish or failure)
    // Description: Reads the sticker manifest from StreamingAssets, then
    //              downloads and writes each listed file to persistent storage.
    //              Uses UnityWebRequest to support Android StreamingAssets access.
    //              Each file request times out after 10 seconds. Sets CopyComplete
    //              when all files are processed or on any fatal error.
    // -------------------------------------------------------------------------
    IEnumerator CopyStickers()
    {
        // Destination root directory in persistent storage
        string destRoot = Path.Combine(Application.persistentDataPath, "Stickers");
        if (!Directory.Exists(destRoot))
            Directory.CreateDirectory(destRoot);

        // ── Read manifest ─────────────────────────────────────────────────
        string manifestPath = Application.streamingAssetsPath + "/Stickers/" + ManifestFile;

        UnityWebRequest mReq = UnityWebRequest.Get(manifestPath);
        mReq.SendWebRequest();

        // Wait for manifest with a 10-second timeout
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

        // Split manifest into individual file entries, stripping blank lines
        string[] lines = mReq.downloadHandler.text.Split(
            new char[] { '\n', '\r' },
            System.StringSplitOptions.RemoveEmptyEntries);

        // ── Copy each sticker file ────────────────────────────────────────
        foreach (string raw in lines)
        {
            string entry = raw.Trim();
            if (string.IsNullOrEmpty(entry)) continue;

            // Build the full source URL and destination file path
            // Entry may be flat ("ChaChing.png") or nested ("Money/Dollar.png")
            string src      = Application.streamingAssetsPath + "/Stickers/" + entry;
            string destFile = Path.Combine(destRoot, entry.Replace('/', Path.DirectorySeparatorChar));
            string destDir  = Path.GetDirectoryName(destFile);

            // Create the category subdirectory if it doesn't exist yet
            if (!Directory.Exists(destDir))
                Directory.CreateDirectory(destDir);

            // Fetch the sticker file via UnityWebRequest (required for Android StreamingAssets)
            UnityWebRequest req = UnityWebRequest.Get(src);
            req.SendWebRequest();

            // Wait for this file with a 10-second timeout
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
                // Write the raw bytes to the destination file
                File.WriteAllBytes(destFile, req.downloadHandler.data);
            }
            else
            {
                Debug.LogWarning("StickerCopier: Failed to copy " + entry + " — " + req.error);
            }
        }

        // Signal to StickerPickerWindow that all stickers are ready
        CopyComplete = true;
    }
}
