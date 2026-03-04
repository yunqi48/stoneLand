using UnityEngine;

/// <summary>
/// 布局配置：控制对话框整体在屏幕中的位置、大小、头像位置等。
/// 可以为“左 / 右 / 中 / 全宽 / 无头像”等创建多份 ScriptableObject。
/// </summary>
[CreateAssetMenu(menuName = "Dialogue/Layout", fileName = "DialogueLayout")]
public class DialogueLayout : ScriptableObject
{
    public enum LayoutType
    {
        Left,
        Right,
        Center,
        FullWidth,
        Custom
    }

    [Header("布局类型（仅作标记，方便在 Inspector 中区分）")]
    public LayoutType layoutType = LayoutType.Left;

    [Header("对话框 RectTransform 布局")]
    public Vector2 anchorMin = new Vector2(0.5f, 0f);
    public Vector2 anchorMax = new Vector2(0.5f, 0f);
    public Vector2 pivot = new Vector2(0.5f, 0f);

    [Tooltip("锚点相对 Canvas 的偏移位置")]
    public Vector2 anchoredPosition = new Vector2(0f, 100f);

    [Tooltip("对话框尺寸")]
    public Vector2 sizeDelta = new Vector2(1600f, 400f);

    [Header("头像区域")]
    [Tooltip("是否启用头像区域（为 false 时，可以在 UI 上隐藏头像容器）")]
    public bool useAvatar = true;

    [Tooltip("头像区域相对对话框的偏移")]
    public Vector2 avatarOffset = new Vector2(-650f, 0f);

    [Tooltip("头像区域尺寸")]
    public Vector2 avatarSize = new Vector2(256f, 256f);

    [Tooltip("是否镜像头像（用于左右布局时镜像 X 轴）")]
    public bool mirrorAvatar = false;
}

