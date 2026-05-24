using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;

/// <summary>
/// Large panel that loads PNG stickers from
/// Application.persistentDataPath/Stickers/Default/
/// On first run, copies stickers from StreamingAssets/Stickers/Default/ to persistentDataPath.
/// Tapping a sticker fires OnStickerSelected and closes the panel.
/// Tapping outside or Android back button closes without selecting.
/// </summary>
public class StickerPickerWindow : MonoBehaviour
{
    [Header("References")]
    public ScrollRect scrollRect;
    public RectTransform contentRect;
    public GameObject cellPrefab;

    [Header("Grid Settings")]
    public int columns = 4;
    public float cellSize = 160f;
    public float spacing = 10f;

    [Header("Panel")]
    public GameObject panel;

    public System.Action<Texture2D> OnStickerSelected;

    private List<Texture2D> loadedTextures = new List<Texture2D>();
    private bool loaded = false;
    private bool copying = false;

    void Start()
    {
        StartCoroutine(CopyStickersFromStreamingAssets());
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            Hide();
    }

    public void Show()
    {
        panel.SetActive(true);

        if (!loaded && !copying)
            StartCoroutine(LoadAndBuildGrid());
        else if (!loaded && copying)
            StartCoroutine(WaitThenLoad());
    }

    public void Hide()
    {
        panel.SetActive(false);
    }

    IEnumerator WaitThenLoad()
    {
        while (copying)
            yield return null;
        yield return StartCoroutine(LoadAndBuildGrid());
    }

    IEnumerator CopyStickersFromStreamingAssets()
    {
        copying = true;

        string destDir = Path.Combine(Application.persistentDataPath, "Stickers", "Default");
        if (!Directory.Exists(destDir))
            Directory.CreateDirectory(destDir);

        string manifestPath = Path.Combine(Application.streamingAssetsPath, "Stickers", "Default", "manifest.txt");
        UnityWebRequest manifestRequest = UnityWebRequest.Get(manifestPath);
        yield return manifestRequest.SendWebRequest();

        if (manifestRequest.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning("StickerPickerWindow: Could not load sticker manifest: " + manifestRequest.error);
            copying = false;
            yield break;
        }

        string[] fileNames = manifestRequest.downloadHandler.text.Split('\n');

        foreach (string rawName in fileNames)
        {
            string fileName = rawName.Trim();
            if (string.IsNullOrEmpty(fileName)) continue;

            string destFile = Path.Combine(destDir, fileName);

                string srcPath = Path.Combine(Application.streamingAssetsPath, "Stickers", "Default", fileName);
                UnityWebRequest request = UnityWebRequest.Get(srcPath);
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    File.WriteAllBytes(destFile, request.downloadHandler.data);
                    Debug.Log("StickerPickerWindow: Copied " + fileName);
                }
                else
                {
                    Debug.LogWarning("StickerPickerWindow: Failed to copy " + fileName + ": " + request.error);
                }
        }

        copying = false;
        Debug.Log("StickerPickerWindow: Sticker copy complete.");
    }

    IEnumerator LoadAndBuildGrid()
    {
        string dir = Path.Combine(Application.persistentDataPath, "Stickers", "Default");

        if (!Directory.Exists(dir))
        {
            Debug.LogWarning("StickerPickerWindow: Sticker directory not found: " + dir);
            yield break;
        }

        string[] files = Directory.GetFiles(dir, "*.png");

        if (files.Length == 0)
        {
            Debug.LogWarning("StickerPickerWindow: No PNG files found in " + dir);
            yield break;
        }

        foreach (Transform child in contentRect)
            Destroy(child.gameObject);
        loadedTextures.Clear();

        GridLayoutGroup grid = contentRect.GetComponent<GridLayoutGroup>();
        if (grid == null)
            grid = contentRect.gameObject.AddComponent<GridLayoutGroup>();

        grid.cellSize        = new Vector2(cellSize, cellSize);
        grid.spacing         = new Vector2(spacing, spacing);
        grid.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columns;
        grid.padding         = new RectOffset(10, 10, 10, 10);

        ContentSizeFitter fitter = contentRect.GetComponent<ContentSizeFitter>();
        if (fitter == null)
            fitter = contentRect.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        foreach (string filePath in files)
        {
            byte[] bytes = File.ReadAllBytes(filePath);
            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(bytes);
            tex.name = Path.GetFileNameWithoutExtension(filePath);
            loadedTextures.Add(tex);

            BuildCell(tex);

            if (loadedTextures.Count % 4 == 0)
                yield return null;
        }

        scrollRect.verticalNormalizedPosition = 1f;
        loaded = true;
    }

    void BuildCell(Texture2D tex)
    {
        GameObject cell = Instantiate(cellPrefab, contentRect);

        RawImage img = cell.GetComponent<RawImage>();
        if (img == null)
            img = cell.GetComponentInChildren<RawImage>();
        if (img != null)
            img.texture = tex;

        Button btn = cell.GetComponent<Button>();
        if (btn == null)
            btn = cell.GetComponentInChildren<Button>();
        if (btn != null)
        {
            Texture2D captured = tex;
            btn.onClick.AddListener(() =>
            {
                OnStickerSelected?.Invoke(captured);
                Hide();
            });
        }
    }
}
