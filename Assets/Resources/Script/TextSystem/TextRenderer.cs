using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

public class TextRenderer : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI tmpText; // TMP文本组件
    public TextMeshProUGUI TmpText => tmpText; // 适配DialogueWindow的公开属性

    // 新增：公开富文本状态（便于上层判断）
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
        // 自动查找TMP组件（空值防护）
        if (tmpText == null)
        {
            tmpText = GetComponent<TextMeshProUGUI>();

            // 日志提示：便于调试未绑定的情况
            if (tmpText == null)
            {
                Debug.LogError($"[{gameObject.name}] TextRenderer未找到TextMeshProUGUI组件！", this);
            }
        }
    }

    // 解析富文本标签，跟踪未闭合的标签（新增空值防护）
    private string ParseAndTrackRichTags(string text, bool trackOpenTags)
    {
        // 空值防护：tmpText为null时直接返回原文本
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

                // 处理开始标签
                if (!tag.StartsWith("</") && trackOpenTags)
                {
                    // 提取标签类型（如 <color=#fff> -> <color）
                    string tagType = tag.Split('=')[0];
                    if (_closingTags.ContainsKey(tagType))
                    {
                        _openRichTags.Push(tagType);
                    }
                }
                // 处理结束标签
                else if (tag.StartsWith("</") && trackOpenTags)
                {
                    // 匹配对应的开始标签并弹出
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

    // 闭合所有未关闭的富文本标签
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

    // 直接设置文本（立即显示）- 新增空值防护
    public void SetText(string processedText)
    {
        if (tmpText == null)
        {
            Debug.LogWarning($"[{gameObject.name}] tmpText为null，无法设置文本", this);
            return;
        }

        _openRichTags.Clear();
        tmpText.text = processedText;
        tmpText.ForceMeshUpdate();
    }

    // 更新可见字符范围（显示前N个字符）- 修复富文本剪断问题（新增空值防护）
    public void UpdateVisibleRange(int charCount, ParsedText parsedText)
    {
        if (tmpText == null)
        {
            Debug.LogWarning($"[{gameObject.name}] tmpText为null，无法更新可见范围", this);
            return;
        }

        if (charCount <= 0)
        {
            _openRichTags.Clear();
            tmpText.text = "";
            tmpText.ForceMeshUpdate(); // 新增：强制更新
            return;
        }

        // 空值防护：parsedText为null时直接返回
        if (parsedText == null)
        {
            Debug.LogWarning($"[{gameObject.name}] ParsedText为null，无法更新可见范围", this);
            return;
        }

        // 获取实际要显示的字符索引
        int displayIndex = parsedText.plainText.Length;
        if (charCount > 0 && parsedText.charMapping.ContainsKey(charCount - 1))
        {
            displayIndex = parsedText.charMapping[charCount - 1] + 1;
        }

        // 截取并显示文本
        if (displayIndex > 0 && displayIndex <= parsedText.plainText.Length)
        {
            string visibleText = parsedText.plainText.Substring(0, displayIndex);
            // 解析并跟踪富文本标签
            visibleText = ParseAndTrackRichTags(visibleText, true);
            // 闭合未完成的标签
            visibleText += CloseOpenRichTags();

            tmpText.text = visibleText;
        }
        tmpText.ForceMeshUpdate();
    }

    // 立即显示全部文本（新增空值防护）
    public void ShowInstant(string fullText)
    {
        if (tmpText == null)
        {
            Debug.LogWarning($"[{gameObject.name}] tmpText为null，无法立即显示文本", this);
            return;
        }

        _openRichTags.Clear();
        tmpText.text = fullText;
        tmpText.ForceMeshUpdate();
    }

    // 清空文本（优化：添加ForceMeshUpdate）
    public void Clear()
    {
        if (tmpText == null)
        {
            Debug.LogWarning($"[{gameObject.name}] tmpText为null，无法清空文本", this);
            return;
        }

        _openRichTags.Clear();
        tmpText.text = "";
        tmpText.ForceMeshUpdate(); // 新增：强制更新网格，避免残留
    }
}