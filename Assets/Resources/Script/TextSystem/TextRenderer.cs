using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TextRenderer : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI tmpText;
    public TextMeshProUGUI TmpText => tmpText;

    [Header("Scene Auto Binding")]
    [SerializeField] private bool useSceneBinding = false;
    [SerializeField] private string targetSceneName = "";
    [SerializeField] private string dialogueWindowName = "DialogueWindow";
    [SerializeField] private string dialogueTextPath = "DialogueText";

    public bool IsRichTextEnabled => tmpText != null && tmpText.richText;

    private readonly Stack<string> _openRichTags = new Stack<string>();
    private readonly Dictionary<string, string> _closingTags = new Dictionary<string, string>()
    {
        { "<color", "</color>" },
        { "<size", "</size>" },
        { "<b>", "</b>" },
        { "<i>", "</i>" },
        { "<u>", "</u>" },
        { "<mark", "</mark>" },
        { "<style", "</style>" }
    };

    private void Awake()
    {
        if (tmpText == null && useSceneBinding)
        {
            TryBindTmpTextFromScene();
        }

        if (tmpText == null)
        {
            tmpText = GetComponent<TextMeshProUGUI>();

            if (tmpText == null)
            {
                Transform child = FindChildRecursive(transform, dialogueTextPath);
                if (child != null)
                {
                    tmpText = child.GetComponent<TextMeshProUGUI>();
                }
            }

            if (tmpText == null)
            {
                Debug.LogError($"[{gameObject.name}] TextRenderer could not find TextMeshProUGUI.", this);
            }
        }
    }

    private void TryBindTmpTextFromScene()
    {
        Scene targetScene = string.IsNullOrWhiteSpace(targetSceneName)
            ? SceneManager.GetActiveScene()
            : SceneManager.GetSceneByName(targetSceneName);

        if (!targetScene.isLoaded)
        {
            Debug.LogWarning($"[{gameObject.name}] target scene not loaded: {targetSceneName}", this);
            return;
        }

        foreach (GameObject root in targetScene.GetRootGameObjects())
        {
            Transform windowTransform = root.name == dialogueWindowName
                ? root.transform
                : FindChildRecursive(root.transform, dialogueWindowName);

            if (windowTransform == null)
            {
                continue;
            }

            Transform dialogueTextTransform = FindChildRecursive(windowTransform, dialogueTextPath);
            if (dialogueTextTransform == null)
            {
                continue;
            }

            tmpText = dialogueTextTransform.GetComponent<TextMeshProUGUI>();
            if (tmpText != null)
            {
                return;
            }
        }

        Debug.LogWarning($"[{gameObject.name}] Could not bind TMP from scene '{targetScene.name}' via {dialogueWindowName}/{dialogueTextPath}.", this);
    }

    private Transform FindChildRecursive(Transform parent, string childName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == childName)
            {
                return child;
            }

            Transform nested = FindChildRecursive(child, childName);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    private string ParseAndTrackRichTags(string text, bool trackOpenTags)
    {
        if (tmpText == null || !tmpText.richText) return text;

        StringBuilder processedText = new StringBuilder();
        int index = 0;

        while (index < text.Length)
        {
            if (text[index] == '<')
            {
                int endIndex = text.IndexOf('>', index + 1);
                if (endIndex == -1) break;

                string tag = text.Substring(index, endIndex - index + 1);
                processedText.Append(tag);

                if (!tag.StartsWith("</") && trackOpenTags)
                {
                    string tagType = tag.Split('=')[0];
                    if (_closingTags.ContainsKey(tagType))
                    {
                        _openRichTags.Push(tagType);
                    }
                }
                else if (tag.StartsWith("</") && trackOpenTags)
                {
                    foreach (var kvp in _closingTags)
                    {
                        if (tag == kvp.Value && _openRichTags.Count > 0 && _openRichTags.Peek() == kvp.Key)
                        {
                            _openRichTags.Pop();
                            break;
                        }
                    }
                }

                index = endIndex + 1;
            }
            else
            {
                processedText.Append(text[index]);
                index++;
            }
        }

        return processedText.ToString();
    }

    private string CloseOpenRichTags()
    {
        StringBuilder closeTags = new StringBuilder();
        while (_openRichTags.Count > 0)
        {
            string openTag = _openRichTags.Pop();
            if (_closingTags.TryGetValue(openTag, out string closeTag))
            {
                closeTags.Append(closeTag);
            }
        }
        return closeTags.ToString();
    }

    public void SetText(string processedText)
    {
        if (tmpText == null)
        {
            Debug.LogWarning($"[{gameObject.name}] tmpText is null, cannot set text.", this);
            return;
        }

        _openRichTags.Clear();
        tmpText.text = processedText;
        tmpText.ForceMeshUpdate();
    }

    public void UpdateVisibleRange(int charCount, ParsedText parsedText)
    {
        if (tmpText == null)
        {
            Debug.LogWarning($"[{gameObject.name}] tmpText is null, cannot update visible range.", this);
            return;
        }

        if (charCount <= 0)
        {
            _openRichTags.Clear();
            tmpText.text = "";
            tmpText.ForceMeshUpdate();
            return;
        }

        if (parsedText == null)
        {
            Debug.LogWarning($"[{gameObject.name}] ParsedText is null, cannot update visible range.", this);
            return;
        }

        int displayIndex = parsedText.plainText.Length;
        if (charCount > 0 && parsedText.charMapping.ContainsKey(charCount - 1))
        {
            displayIndex = parsedText.charMapping[charCount - 1] + 1;
        }

        if (displayIndex > 0 && displayIndex <= parsedText.plainText.Length)
        {
            string visibleText = parsedText.plainText.Substring(0, displayIndex);
            visibleText = ParseAndTrackRichTags(visibleText, true);
            visibleText += CloseOpenRichTags();

            tmpText.text = visibleText;
        }
        tmpText.ForceMeshUpdate();
    }

    public void ShowInstant(string fullText)
    {
        if (tmpText == null)
        {
            Debug.LogWarning($"[{gameObject.name}] tmpText is null, cannot show text instantly.", this);
            return;
        }

        _openRichTags.Clear();
        tmpText.text = fullText;
        tmpText.ForceMeshUpdate();
    }

    public void Clear()
    {
        if (tmpText == null)
        {
            Debug.LogWarning($"[{gameObject.name}] tmpText is null, cannot clear text.", this);
            return;
        }

        _openRichTags.Clear();
        tmpText.text = "";
        tmpText.ForceMeshUpdate();
    }
}