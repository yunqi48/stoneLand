using System.Collections;
using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// 控制事件
/// </summary>
public class ControlEvent
{
    public int index;          // 事件触发的字符索引
    public ControlEventType type; // 事件类型
    public float value;        // 事件参数值（如暂停时间、速度值）
    public string tag;         // 原始标签文本
    public bool isTriggered;   // 标记是否已触发，避免重复执行
}
