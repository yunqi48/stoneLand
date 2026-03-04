using UnityEngine;
using TMPro;

/// <summary>
/// 文本 / 气泡样式配置：字体、字号、颜色、背景图等。
/// 可为不同角色或不同语气配置多份 ScriptableObject。
/// </summary>
[CreateAssetMenu(menuName = "Dialogue/Style", fileName = "DialogueStyle")]
public class DialogueStyle : ScriptableObject
{
    [Header("文本样式")]
    [Tooltip("使用的 TMP 字体（注意要支持中文）")]
    public TMP_FontAsset font;

    [Tooltip("详细内容文本字号")]
    public int bodyFontSize = 36;

    [Tooltip("详细内容文本颜色")]
    public Color bodyColor = Color.white;

    [Tooltip("标题（角色名）文本字号")]
    public int titleFontSize = 40;

    [Tooltip("标题文本颜色（若为空则用 SpeakerProfile.nameColor）")]
    public Color titleColor = Color.white;

    [Tooltip("正文对齐方式")]
    public TextAlignmentOptions alignment = TextAlignmentOptions.Left;

    [Header("对话框背景")]
    [Tooltip("气泡背景图（可为空，仅用纯色）")]
    public Sprite backgroundSprite;

    [Tooltip("背景颜色")]
    public Color backgroundColor = new Color(0f, 0f, 0f, 0.75f);
}

