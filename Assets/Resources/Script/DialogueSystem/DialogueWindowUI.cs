using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 单个对话窗口的 UI 控制：
/// - 应用布局（左/右/中/全宽等）
/// - 应用样式（字体、颜色、背景）
/// - 应用说话者（名字、头像）
/// - 管理显示/隐藏动画
/// - 内置简单打字机效果（可选）
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class DialogueWindowUI : MonoBehaviour
{
    [Header("基础引用")]
    [SerializeField] private RectTransform rootRect;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private RectTransform avatarContainer;
    [SerializeField] private Image avatarImage;
    [SerializeField] private TextMeshProUGUI nameLabel;
    [SerializeField] private TextMeshProUGUI bodyLabel;

    [Header("默认资源")]
    [SerializeField] private DialogueLayout defaultLayout;
    [SerializeField] private DialogueStyle defaultStyle;

    [Header("动画配置")]
    [SerializeField] private float fadeDuration = 0.2f;
    [SerializeField] private float slideDistance = 100f;

    [Tooltip("是否默认使用打字机效果")]
    [SerializeField] private bool useTypewriterByDefault = true;
    [SerializeField] private float charsPerSecond = 30f;

    private CanvasGroup _canvasGroup;

    // 状态缓存
    private Coroutine _fadeCoroutine;
    private Coroutine _typeCoroutine;
    private string _fullText;

    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        if (rootRect == null) rootRect = transform as RectTransform;

        // 初始隐藏
        _canvasGroup.alpha = 0f;
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;
    }

    #region 外部主入口 API

    /// <summary>
    /// 设置说话者信息 + 样式 + 布局 + 文本，并显示窗口。
    /// </summary>
    public void Show(SpeakerProfile speaker,
        string text,
        DialogueLayout layoutOverride = null,
        DialogueStyle styleOverride = null,
        bool? useTypewriter = null)
    {
        // 1. 应用布局
        ApplyLayout(ResolveLayout(speaker, layoutOverride));

        // 2. 应用样式
        ApplyStyle(ResolveStyle(speaker, styleOverride), speaker);

        // 3. 应用说话者显示（名字 / 头像）
        ApplySpeakerVisual(speaker);

        // 4. 文本 & 打字机
        SetBodyText(text, useTypewriter ?? useTypewriterByDefault);

        // 5. 显示动画（淡入 + 轻微滑入）
        PlayShowAnimation();
    }

    /// <summary>
    /// 隐藏窗口（立即或带动画）。
    /// </summary>
    public void Hide(bool instant = false)
    {
        if (instant)
        {
            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
            return;
        }

        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(FadeCoroutine(0f));
    }

    /// <summary>
    /// 跳过当前打字机，立即显示完整文本。
    /// </summary>
    public void SkipTypewriter()
    {
        if (_typeCoroutine != null)
        {
            StopCoroutine(_typeCoroutine);
            _typeCoroutine = null;
        }

        if (bodyLabel != null)
        {
            bodyLabel.text = _fullText;
        }
    }

    #endregion

    #region 样式 / 布局 / 文本

    private DialogueLayout ResolveLayout(SpeakerProfile speaker, DialogueLayout external)
    {
        if (speaker != null &&
            speaker.layoutOverride != null &&
            speaker.forceLayoutOverride)
        {
            return speaker.layoutOverride;
        }

        if (external != null) return external;
        if (speaker != null && speaker.layoutOverride != null) return speaker.layoutOverride;
        return defaultLayout;
    }

    private DialogueStyle ResolveStyle(SpeakerProfile speaker, DialogueStyle external)
    {
        if (external != null) return external;
        if (speaker != null && speaker.styleOverride != null) return speaker.styleOverride;
        return defaultStyle;
    }

    private void ApplyLayout(DialogueLayout layout)
    {
        if (layout == null || rootRect == null) return;

        rootRect.anchorMin = layout.anchorMin;
        rootRect.anchorMax = layout.anchorMax;
        rootRect.pivot = layout.pivot;
        rootRect.anchoredPosition = layout.anchoredPosition;
        rootRect.sizeDelta = layout.sizeDelta;

        if (avatarContainer != null)
        {
            avatarContainer.anchoredPosition = layout.avatarOffset;
            avatarContainer.sizeDelta = layout.avatarSize;
            avatarContainer.gameObject.SetActive(layout.useAvatar);

            // 镜像头像
            var s = avatarContainer.localScale;
            s.x = layout.mirrorAvatar ? -Mathf.Abs(s.x) : Mathf.Abs(s.x);
            avatarContainer.localScale = s;
        }
    }

    private void ApplyStyle(DialogueStyle style, SpeakerProfile speaker)
    {
        if (style == null) return;

        if (bodyLabel != null)
        {
            if (style.font != null) bodyLabel.font = style.font;
            bodyLabel.fontSize = style.bodyFontSize;
            bodyLabel.color = style.bodyColor;
            bodyLabel.alignment = style.alignment;
        }

        if (nameLabel != null)
        {
            if (style.font != null) nameLabel.font = style.font;
            nameLabel.fontSize = style.titleFontSize;
            nameLabel.color = style.titleColor != default ? style.titleColor :
                (speaker != null ? speaker.nameColor : Color.white);
        }

        if (backgroundImage != null)
        {
            // 只在样式里确实设置了背景图时才覆盖，防止运行时把预制体上本来就有的背景清空
            if (style.backgroundSprite != null)
            {
                backgroundImage.sprite = style.backgroundSprite;
            }
            backgroundImage.color = style.backgroundColor;
        }
    }

    private void ApplySpeakerVisual(SpeakerProfile speaker)
    {
        // 名字
        if (nameLabel != null)
        {
            if (speaker == null || !speaker.showName)
            {
                nameLabel.gameObject.SetActive(false);
            }
            else
            {
                nameLabel.gameObject.SetActive(true);
                nameLabel.text = speaker.displayName;
            }
        }

        // 头像
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
    }

    private void SetBodyText(string text, bool useTypewriter)
    {
        if (bodyLabel == null) return;

        _fullText = text ?? string.Empty;

        if (_typeCoroutine != null)
        {
            StopCoroutine(_typeCoroutine);
            _typeCoroutine = null;
        }

        if (!useTypewriter || charsPerSecond <= 0f)
        {
            bodyLabel.text = _fullText;
            return;
        }

        _typeCoroutine = StartCoroutine(TypewriterCoroutine());
    }

    #endregion

    #region 动画 / 协程

    private void PlayShowAnimation()
    {
        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(ShowWithSlideAndFade());
    }

    private IEnumerator ShowWithSlideAndFade()
    {
        _canvasGroup.interactable = true;
        _canvasGroup.blocksRaycasts = true;

        float t = 0f;
        _canvasGroup.alpha = 0f;

        Vector2 startPos = rootRect.anchoredPosition - new Vector2(0f, slideDistance);
        Vector2 endPos = rootRect.anchoredPosition;

        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / fadeDuration);
            _canvasGroup.alpha = p;
            rootRect.anchoredPosition = Vector2.Lerp(startPos, endPos, p);
            yield return null;
        }

        _canvasGroup.alpha = 1f;
        rootRect.anchoredPosition = endPos;
    }

    private IEnumerator FadeCoroutine(float targetAlpha)
    {
        float start = _canvasGroup.alpha;
        float t = 0f;

        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / fadeDuration);
            _canvasGroup.alpha = Mathf.Lerp(start, targetAlpha, p);
            yield return null;
        }

        _canvasGroup.alpha = targetAlpha;
        _canvasGroup.interactable = targetAlpha > 0.9f;
        _canvasGroup.blocksRaycasts = targetAlpha > 0.9f;
    }

    private IEnumerator TypewriterCoroutine()
    {
        bodyLabel.text = string.Empty;

        if (string.IsNullOrEmpty(_fullText))
            yield break;

        float interval = 1f / Mathf.Max(charsPerSecond, 1f);

        for (int i = 0; i < _fullText.Length; i++)
        {
            bodyLabel.text = _fullText.Substring(0, i + 1);
            yield return new WaitForSeconds(interval);
        }
    }

    #endregion
}

