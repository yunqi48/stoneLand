using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ParsedText
{
    public string plainText;               // 去除控制标签后的纯文本
    public List<ControlEvent> events;      // 控制事件列表（已按index排序）
    public Dictionary<int, int> charMapping; // 可见字符索引映射（处理富文本标签）
    public int VisibleLength { get; private set; } // 可见字符总数（替代Count）

    // 初始化可见长度
    public void CalculateVisibleLength()
    {
        if (charMapping == null || charMapping.Count == 0)
        {
            VisibleLength = 0;
            return;
        }
        // 取最大key+1作为可见长度（处理不连续key的情况）
        int maxKey = charMapping.Keys.Max();
        VisibleLength = maxKey + 1;
    }

    // 排序事件并重置触发状态
    public void PrepareEvents()
    {
        if (events == null) return;
        // 按索引排序事件
        events = events.OrderBy(e => e.index).ToList();
        // 重置触发状态
        foreach (var evt in events)
        {
            evt.isTriggered = false;
        }
    }
}