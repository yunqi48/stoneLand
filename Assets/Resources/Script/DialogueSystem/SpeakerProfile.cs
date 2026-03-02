using UnityEngine;

using TMPro;

[CreateAssetMenu(menuName = "Dialogue/SpeakerProfile", fileName = "NewSpeakerProfile")]
public class SpeakerProfile : ScriptableObject
{
    [Header("基础信息")]
    [Tooltip("角色唯一标识（与剧情脚本中的speakerId对应）")]
    public string speakerId = "default_speaker";

    [Tooltip("角色显示名（可覆盖为多语言Key）")]
    public string displayName = "默认角色";

    [Tooltip("角色头像Sprite")]
    public Sprite avatar;

    [Header("样式配置")]
    [Tooltip("角色名显示颜色")]
    public Color nameColor = Color.white;

    [Tooltip("是否显示角色名")]
    public bool showName = true;

    [Tooltip("角色名显示优先级（多人同框时数值高的优先显示）")]
    public int namePriority = 0;

    [Header("布局与样式覆盖")]
    [Tooltip("角色偏好的对话框布局（为空则使用全局默认）")]
    public DialogueWindowLayout preferredLayout;

    [Tooltip("角色专属样式（为空则使用全局默认样式）")]
    public DialogueStyle styleOverride;

    [Tooltip("是否强制使用偏好布局（false则允许管理器自动调整）")]
    public bool forcePreferredLayout = false;
}