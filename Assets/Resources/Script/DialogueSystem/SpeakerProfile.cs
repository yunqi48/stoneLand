using UnityEngine;
using TMPro;

/// <summary>
/// 说话者配置（名字 / 头像 / 颜色 / 样式覆盖 / 布局偏好）
/// 仅存放静态数据，方便在 Inspector 中创建多份资产并复用。
/// </summary>
[CreateAssetMenu(menuName = "Dialogue/Speaker Profile", fileName = "SpeakerProfile")]
public class SpeakerProfile : ScriptableObject
{
    [Header("基础信息")]
    [Tooltip("角色唯一ID，供剧情系统或脚本查找用")]
    public string speakerId = "default";

    [Tooltip("展示在对话框上的角色名")]
    public string displayName = "角色名";

    [Tooltip("角色名颜色")]
    public Color nameColor = Color.white;

    [Tooltip("是否显示角色名（旁白可以关掉）")]
    public bool showName = true;

    [Header("头像")]
    [Tooltip("角色头像（可为空表示无头像）")]
    public Sprite avatar;

    [Tooltip("头像在有/无头像布局之间的优先级（数值越大越靠前）")]
    public int avatarPriority = 0;

    [Header("样式 / 布局覆盖")]
    [Tooltip("可选：该角色专用的对话文本样式（字体、字号、气泡背景等）")]
    public DialogueStyle styleOverride;

    [Tooltip("可选：该角色偏好的布局（左 / 右 / 中 / 全宽等）")]
    public DialogueLayout layoutOverride;

    [Tooltip("是否强制使用角色自带布局，而不是 UIManager 传入的布局")]
    public bool forceLayoutOverride = false;
}

