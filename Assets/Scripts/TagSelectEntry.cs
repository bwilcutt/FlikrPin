using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Optional component that can sit on a row prefab.
/// The new TagSelectPanel wires clicks directly, so this class
/// is kept only for backward compatibility and does nothing critical.
/// </summary>
public class TagSelectEntry : MonoBehaviour
{
    [HideInInspector] public PostTag postTag;
    [HideInInspector] public string  postId;
    [HideInInspector] public string  mediaType;
    [HideInInspector] public string  displayText;
}
