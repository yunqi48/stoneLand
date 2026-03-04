using UnityEngine;

/// <summary>
/// 对话 UI 管理器（仅负责 UI，不关心剧情流程）：
/// - 管理一个或多个 DialogueWindowUI 实例
/// - 运行时切换角色 / 布局 / 样式
/// - 对外提供简单 API，方便剧情系统调用
/// </summary>
public class DialogueUIManager : MonoBehaviour
{
    public static DialogueUIManager Instance { get; private set; }

    [Header("预设窗口（可多布局）")]
    [Tooltip("例如：LeftWindow / RightWindow / CenterWindow / FullWidthWindow 等")]
    public DialogueWindowUI[] windows;

    [Header("默认资源")]
    public DialogueWindowUI defaultWindow;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// 使用指定窗口显示一条对话。
    /// 剧情系统只需要传入「说话者 + 文本」，其他布局/样式按规则自动解析。
    /// </summary>
    public void ShowLine(SpeakerProfile speaker, string text, DialogueWindowUI targetWindow = null,
        DialogueLayout layoutOverride = null, DialogueStyle styleOverride = null, bool? useTypewriter = null)
    {
        DialogueWindowUI window = targetWindow ?? defaultWindow;
        if (window == null && windows != null && windows.Length > 0)
        {
            window = windows[0];
        }

        if (window == null)
        {
            Debug.LogWarning("[DialogueUIManager] 没有可用的 DialogueWindowUI。", this);
            return;
        }

        window.Show(speaker, text, layoutOverride, styleOverride, useTypewriter);
    }

    /// <summary>
    /// 简单重载：仅文本（无角色 / 无布局指定），适合作为系统提示 / 旁白。
    /// </summary>
    public void ShowLine(string text)
    {
        ShowLine(null, text);
    }
}

