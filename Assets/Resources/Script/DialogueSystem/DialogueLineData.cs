using UnityEngine;

/// <summary>
/// 单条对话节点的数据结构，对应 CSV 中的一行。
/// 仅保存数据本身，不包含任何播放逻辑。
/// </summary>
[System.Serializable]
public class DialogueLineData
{
    /// <summary>节点唯一 ID（来自 CSV 的 NodeID 列）。</summary>
    public string nodeId;

    /// <summary>说话者配置（通过 SpeakerName 匹配到的 SpeakerProfile）。</summary>
    public SpeakerProfile speaker;

    /// <summary>这一节点要显示的文本内容（来自 Text 列）。</summary>
    public string text;

    /// <summary>打字机速度（来自 TypingSpeed，后续可用于控制 UI 播放速度）。</summary>
    public float typingSpeed;

    /// <summary>是否在本句结束后自动跳转到下一节点（来自 AutoNext）。</summary>
    public bool autoNext;

    /// <summary>跳转模式（来自 JumpMode，预留扩展用）。</summary>
    public string jumpMode;

    /// <summary>默认下一节点 ID（来自 NextNodeID）。</summary>
    public string nextNodeId;
}

