using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System;

[RequireComponent(typeof(CanvasGroup))]
/// <summary>
/// 单个对话框实例：负责UI渲染、样式应用、动画过渡
/// 挂载到对话框预制体上
/// </summary>
public class DialogueWindow : MonoBehaviour
{
    [Header("UI 引用")]
    [SerializeField] private RectTransform rootRect;          // 对话框根节点
    [SerializeField] private Image backgroundImage;           // 背景图
    [SerializeField] private Image avatarImage;               // 头像
    [SerializeField] private TextMeshProUGUI nameText;        // 角色名文本
    [SerializeField] private RectTransform avatarRect;        // 头像容器
    [SerializeField] private CanvasGroup canvasGroup;         // 淡入淡出控制
    [SerializeField] private TextRenderer dialogueTextRenderer; // 复用你的文本渲染器
    [SerializeField] private TextSystem dialogueTextSystem;   // TextSystem（手动绑定优先）

    [Header("默认配置")]
    [SerializeField] private DialogueStyle defaultStyle;      // 全局默认样式
    [SerializeField] private float fadeDuration = 0.2f;       // 淡入淡出动画时长

    // 当前激活的角色配置
    private SpeakerProfile currentSpeaker;
    // 当前应用的布局
    private DialogueWindowLayout currentLayout;
    // 是否正在播放动画
    private bool isAnimating;

    // 缓存TextRenderer中的TMP组件（避免重复查找）
    private TextMeshProUGUI dialogueTmpText;

    private void Awake()
    {
        // 自动获取CanvasGroup
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        // 初始化CanvasGroup状态（空值防护）
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
        else
        {
            Debug.LogError($"[{gameObject.name}] CanvasGroup组件未找到！", this);
        }

        // 绑定TextRenderer（若未手动赋值则自动查找）
        if (dialogueTextRenderer == null)
        {
            dialogueTextRenderer = GetComponent<TextRenderer>();
        }

        // 适配TextRenderer：获取tmpText（优先公开属性，降级反射）
        if (dialogueTextRenderer != null)
        {
            // 优先使用公开属性
            dialogueTmpText = dialogueTextRenderer.TmpText;

            // 降级：反射获取私有字段
            if (dialogueTmpText == null)
            {
                var field = typeof(TextRenderer).GetField("tmpText",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    dialogueTmpText = field.GetValue(dialogueTextRenderer) as TextMeshProUGUI;
                }
                else
                {
                    Debug.LogWarning($"[{gameObject.name}] 无法获取TextRenderer的tmpText字段", this);
                }
            }
        }

        // 自动绑定TextSystem（降级方案）
        if (dialogueTextSystem == null && dialogueTextRenderer != null)
        {
            dialogueTextSystem = dialogueTextRenderer.GetComponent<TextSystem>();
        }

        // 关键组件校验
        if (rootRect == null)
            Debug.LogError($"[{gameObject.name}] rootRect未绑定！", this);
        if (dialogueTextRenderer == null)
            Debug.LogWarning($"[{gameObject.name}] dialogueTextRenderer未绑定！", this);
    }

    #region 核心接口
    /// <summary>
    /// 应用布局配置
    /// </summary>
    public void ApplyLayout(DialogueWindowLayout layout)
    {
        if (layout == null || rootRect == null) return;

        currentLayout = layout;
        // 应用RectTransform配置
        rootRect.anchorMin = layout.anchorMin;
        rootRect.anchorMax = layout.anchorMax;
        rootRect.pivot = layout.pivot;
        rootRect.anchoredPosition = layout.anchoredPosition;
        rootRect.sizeDelta = layout.sizeDelta;

        // 应用头像布局（适配Image组件的镜像逻辑）
        if (avatarRect != null)
        {
            avatarRect.sizeDelta = layout.avatarSize;
            avatarRect.anchoredPosition = layout.avatarOffset;

            // 镜像处理：调整localScale + 保持pivot不变（避免布局错位）
            if (layout.mirrorAvatar)
            {
                // 镜像X轴，保持Y/Z轴不变
                avatarRect.localScale = new Vector3(-1, 1, 1);
                // 补偿锚点：确保镜像后位置不变（关键！）
                avatarRect.anchoredPosition = new Vector2(
                    layout.avatarOffset.x * -1,
                    layout.avatarOffset.y
                );
            }
            else
            {
                avatarRect.localScale = new Vector3(1, 1, 1);
                avatarRect.anchoredPosition = layout.avatarOffset;
            }
        }
    }

    /// <summary>
    /// 应用样式配置（解耦currentSpeaker）
    /// </summary>
    public void ApplyStyle(DialogueStyle style, Color? nameColor = null)
    {
        style = style ?? defaultStyle;
        if (style == null) return;

        // 对话文本样式
        if (dialogueTmpText != null)
        {
            if (style.font != null) dialogueTmpText.font = style.font;
            dialogueTmpText.fontSize = style.fontSize;
            dialogueTmpText.color = style.textColor;
            dialogueTmpText.fontStyle = style.fontStyle;
            dialogueTmpText.alignment = style.alignment;
        }

        // 角色名样式
        if (nameText != null)
        {
            nameText.fontSize = style.fontSize - 4;
            nameText.color = nameColor ?? style.textColor;
            nameText.fontStyle = FontStyles.Bold;
        }

        // 背景样式
        if (backgroundImage != null)
        {
            backgroundImage.sprite = style.backgroundSprite;
            backgroundImage.color = style.backgroundColor;
        }
    }

    /// <summary>
    /// 设置当前说话角色
    /// </summary>
    public void SetSpeaker(SpeakerProfile speaker)
    {
        currentSpeaker = speaker;

        // 角色名处理（空值防护）
        if (nameText != null)
        {
            if (speaker == null || !speaker.showName)
            {
                nameText.gameObject.SetActive(false);
            }
            else
            {
                nameText.gameObject.SetActive(true);
                nameText.text = speaker.displayName;
                nameText.color = speaker.nameColor;
            }
        }

        // 头像处理（空值防护）
        if (avatarImage != null)
        {
            if (speaker == null || speaker.avatar == null)
            {
                avatarImage.gameObject.SetActive(false);
            }
            else
            {
                avatarImage.gameObject.SetActive(true);
                avatarImage.sprite = speaker.avatar;
                avatarImage.preserveAspect = true;
            }
        }

        // 应用角色偏好布局（若有）
        if (speaker?.preferredLayout != null && speaker.forcePreferredLayout)
        {
            ApplyLayout(speaker.preferredLayout);
        }

        // 应用角色专属样式（传入角色名颜色）
        ApplyStyle(speaker?.styleOverride, speaker?.nameColor);
    }

    /// <summary>
    /// 设置对话文本（对接TextRenderer和TextSystem）
    /// </summary>
    public void SetDialogueText(string text, TextPlaySettings playSettings = null)
    {
        if (dialogueTextRenderer == null) return;

        // 清空原有文本
        dialogueTextRenderer.Clear();

        // 调用TextSystem播放文本
        if (dialogueTextSystem != null)
        {
            dialogueTextSystem.PlayText(text, playSettings ?? new TextPlaySettings());
        }
        else
        {
            // 降级：无TextSystem时直接显示文本
            dialogueTextRenderer.SetText(text);
        }
    }
    #endregion

    #region 显示/隐藏动画
    /// <summary>
    /// 显示对话框
    /// </summary>
    public void ShowWindow(bool instant = false, Action onComplete = null)
    {
        // Instant模式：强制跳过动画
        if (instant)
        {
            if (isAnimating)
            {
                StopCoroutine(FadeCoroutine(1, null));
                isAnimating = false;
            }
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }
            onComplete?.Invoke();
            return;
        }

        // 非Instant模式：防重入
        if (isAnimating) return;
        StartCoroutine(FadeCoroutine(1, onComplete));
    }

    /// <summary>
    /// 隐藏对话框
    /// </summary>
    public void HideWindow(bool instant = false, Action onComplete = null)
    {
        // Instant模式：强制跳过动画
        if (instant)
        {
            if (isAnimating)
            {
                StopCoroutine(FadeCoroutine(0, null));
                isAnimating = false;
            }
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
            onComplete?.Invoke();
            return;
        }

        // 非Instant模式：防重入
        if (isAnimating) return;
        StartCoroutine(FadeCoroutine(0, onComplete));
    }

    /// <summary>
    /// 淡入淡出协程（支持回调）
    /// </summary>
    private IEnumerator FadeCoroutine(float targetAlpha, Action onComplete = null)
    {
        if (canvasGroup == null) yield break;

        isAnimating = true;
        float startAlpha = canvasGroup.alpha;
        float elapsed = 0;

        // 统一管理交互状态
        if (targetAlpha > 0)
        {
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }
        else
        {
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fadeDuration);
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
        isAnimating = false;
        onComplete?.Invoke();
    }
    #endregion

    /// <summary>
    /// 重置对话框状态（清空所有残留）
    /// </summary>
    public void ResetWindow()
    {
        HideWindow(true);
        currentSpeaker = null;
        currentLayout = null;

        // 清空文本
        dialogueTextRenderer?.Clear();

        // 清空角色名
        if (nameText != null)
        {
            nameText.text = "";
            nameText.gameObject.SetActive(false);
        }

        // 清空头像
        if (avatarImage != null)
        {
            avatarImage.sprite = null;
            avatarImage.gameObject.SetActive(false);
        }
    }
}