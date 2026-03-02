using UnityEngine;

[CreateAssetMenu(menuName = "Dialogue/DialogueWindowLayout", fileName = "NewLayout")]
public class DialogueWindowLayout : ScriptableObject
{
    [Header("布局标识")]
    [Tooltip("布局唯一ID（如：left/right/center/full）")]
    public string layoutId = "center";

    [Header("RectTransform 配置")]
    [Tooltip("锚点最小值（0~1）")]
    public Vector2 anchorMin = new Vector2(0.5f, 0);

    [Tooltip("锚点最大值（0~1）")]
    public Vector2 anchorMax = new Vector2(0.5f, 0);

    [Tooltip("枢轴点（0~1）")]
    public Vector2 pivot = new Vector2(0.5f, 0);

    [Tooltip("锚定位置（像素）")]
    public Vector2 anchoredPosition = new Vector2(0, 100);

    [Tooltip("尺寸增量（像素）")]
    public Vector2 sizeDelta = new Vector2(800, 200);

    [Header("头像配置")]
    [Tooltip("是否镜像头像（如右侧对话框的头像翻转）")]
    public bool mirrorAvatar = false;

    [Tooltip("头像容器尺寸（像素）")]
    public Vector2 avatarSize = new Vector2(80, 80);

    [Tooltip("头像相对于对话框的偏移")]
    public Vector2 avatarOffset = new Vector2(-40, 0);
}