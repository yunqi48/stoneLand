using UnityEngine;
using TMPro;

[System.Serializable]
public class DialogueStyle
{
    [Header("文本样式")]
    [Tooltip("对话文本字体")]
    public TMP_FontAsset font;

    [Tooltip("文本字号")]
    public int fontSize = 36;

    [Tooltip("文本颜色")]
    public Color textColor = Color.white;

    [Tooltip("文本样式（粗体/斜体等）")]
    public FontStyles fontStyle = FontStyles.Normal;

    [Tooltip("文本对齐方式")]
    public TextAlignmentOptions alignment = TextAlignmentOptions.Left;

    [Header("容器样式")]
    [Tooltip("对话框内边距（X:左右, Y:上下）")]
    public Vector2 padding = new Vector2(16, 12);

    [Tooltip("对话框背景图")]
    public Sprite backgroundSprite;

    [Tooltip("对话框背景颜色")]
    public Color backgroundColor = new Color(0, 0, 0, 0.8f);

    [Tooltip("对话框边框大小（0为无边框）")]
    public float borderWidth = 2f;

    [Tooltip("对话框边框颜色")]
    public Color borderColor = Color.white;
}