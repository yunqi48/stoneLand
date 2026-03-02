using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class TagParser : MonoBehaviour
{
    // 解析原始文本，返回处理后的文本和控制事件
    public ParsedText Parse(string rawText)
    {
        ParsedText parsedText = new ParsedText
        {
            plainText = "",
            events = new List<ControlEvent>(),
            charMapping = new Dictionary<int, int>()
        };

        if (string.IsNullOrEmpty(rawText))
        {
            parsedText.CalculateVisibleLength();
            parsedText.PrepareEvents();
            return parsedText;
        }

        StringBuilder plainTextBuilder = new StringBuilder();
        int visibleCharIndex = 0;
        int currentPosition = 0;

        while (currentPosition < rawText.Length)
        {
            // 检查是否是自定义控制标签起始
            if (rawText[currentPosition] == '{' && currentPosition + 1 < rawText.Length)
            {
                int endIndex = rawText.IndexOf('}', currentPosition);
                if (endIndex != -1)
                {
                    // 提取标签内容
                    string tagContent = rawText.Substring(currentPosition + 1, endIndex - currentPosition - 1);
                    ProcessControlTag(tagContent, visibleCharIndex, parsedText.events);

                    // 跳过整个标签
                    currentPosition = endIndex + 1;
                    continue;
                }
            }

            // 处理富文本标签（TMP标签）
            if (rawText[currentPosition] == '<')
            {
                int endTagIndex = rawText.IndexOf('>', currentPosition);
                if (endTagIndex != -1)
                {
                    // 保留富文本标签，但不计入可见字符
                    string richTag = rawText.Substring(currentPosition, endTagIndex - currentPosition + 1);
                    plainTextBuilder.Append(richTag);
                    currentPosition = endTagIndex + 1;
                    continue;
                }
            }

            // 普通字符
            plainTextBuilder.Append(rawText[currentPosition]);
            parsedText.charMapping[visibleCharIndex] = plainTextBuilder.Length - 1;
            visibleCharIndex++;
            currentPosition++;
        }

        parsedText.plainText = plainTextBuilder.ToString();
        // 计算可见长度并排序事件
        parsedText.CalculateVisibleLength();
        parsedText.PrepareEvents();

        return parsedText;
    }

    // 处理控制标签
    private void ProcessControlTag(string tagContent, int charIndex, List<ControlEvent> events)
    {
        tagContent = tagContent.Trim().ToLower();

        if (tagContent.StartsWith("pause="))
        {
            if (float.TryParse(tagContent.Substring(6), out float pauseTime))
            {
                events.Add(new ControlEvent
                {
                    index = charIndex,
                    type = ControlEventType.Pause,
                    value = pauseTime,
                    tag = $"{{{tagContent}}}",
                    isTriggered = false
                });
            }
        }
        else if (tagContent.StartsWith("speed="))
        {
            if (float.TryParse(tagContent.Substring(6), out float speedValue))
            {
                events.Add(new ControlEvent
                {
                    index = charIndex,
                    type = ControlEventType.SpeedChange,
                    value = speedValue,
                    tag = $"{{{tagContent}}}",
                    isTriggered = false
                });
            }
        }
        else if (tagContent == "wait")
        {
            events.Add(new ControlEvent
            {
                index = charIndex,
                type = ControlEventType.WaitForInput,
                value = 0,
                tag = "{wait}",
                isTriggered = false
            });
        }
    }
}