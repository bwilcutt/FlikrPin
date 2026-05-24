using UnityEngine;
using System;

public class GalleryPicker : MonoBehaviour
{
    public static GalleryPicker Instance;
    public Action<Texture2D> OnImagePicked;

    void Awake()
    {
        Instance = this;
    }

	public void OpenGallery(Action<Texture2D> onPicked)
	{
	    NativeGallery.GetImageFromGallery((path) =>
	    {
		if (path == null) return;
		Texture2D texture = NativeGallery.LoadImageAtPath(path, 1024);
		if (texture == null) return;
		onPicked?.Invoke(texture);
	    }, "Select Picture");
	}
}
