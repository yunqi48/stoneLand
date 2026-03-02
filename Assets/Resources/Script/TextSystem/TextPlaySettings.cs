using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TextPlaySettings
{
    public float baseDelay = 0.05f;        // 每个字符的基础延迟（秒）
    public int charBatchSize = 1;          // 每帧批量显示的字符数
    public bool allowSkip = true;          // 是否允许跳过
    public bool richTextEnabled = true;    // 是否启用富文本
    public bool playTypeSound = true;      // 是否播放打字音效
    public bool autoAdvanceOnComplete = true; // 完成后自动推进
    public bool skipWaitOnHardSkip = true; // 硬跳过时是否跳过等待输入

}
