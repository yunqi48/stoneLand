using UnityEngine;
using System;

/// <summary>
/// 对话控制器：封装对话触发逻辑，对接 DialogueUIManager，提供简洁的外部调用接口
/// </summary>
public class DialogueController : MonoBehaviour
{
    [Header("默认配置")]
    [SerializeField] private string defaultSpeakerId = "default"; // 默认角色ID
    [SerializeField] private string defaultWindowId = "default";  // 默认窗口ID
    [SerializeField] private TextPlaySettings defaultPlaySettings; // 默认文本播放配置
    [SerializeField] private string narratorSpeakerId = "narrator"; // 旁白专用角色ID（可设为null/空）

    // 本地缓存 DialogueUIManager 实例（避免频繁访问静态属性）
    private DialogueUIManager dialogueUIManager;
    // 当前对话是否触发成功（用于状态判断）
    private bool isCurrentDialogueActive;

    #region 初始化
    private void Awake()
    {
        // 缓存 UIManager 实例（优先取已存在的单例）
        dialogueUIManager = DialogueUIManager.Instance;

        // 初始化默认播放配置（防止空值）
        if (defaultPlaySettings == null)
        {
            defaultPlaySettings = new TextPlaySettings();
            Debug.LogWarning($"[{nameof(DialogueController)}] 默认播放配置未赋值，已创建空配置", this);
        }
    }

    private void OnDestroy()
    {
        // 清理对话（可选：场景销毁时关闭当前对话）
        EndCurrentDialogue();
    }
    #endregion

    #region 核心 API（带完整注释）
    /// <summary>
    /// 触发角色对话（核心接口）
    /// </summary>
    /// <param name="speakerId">说话者ID（null/空表示旁白，自动使用 narratorSpeakerId）</param>
    /// <param name="dialogueText">对话文本</param>
    /// <param name="windowId">目标窗口ID（默认使用 defaultWindowId）</param>
    /// <param name="onComplete">对话显示完成回调（可选）</param>
    /// <returns>是否触发成功（false：UIManager 为空/文本为空）</returns>
    public bool TriggerDialogue(string speakerId, string dialogueText, string windowId = null, Action onComplete = null)
    {
        return TriggerDialogue(speakerId, dialogueText, defaultPlaySettings, windowId, onComplete);
    }

    /// <summary>
    /// 触发角色对话（重载：支持自定义播放配置）
    /// </summary>
    /// <param name="speakerId">说话者ID（null/空表示旁白）</param>
    /// <param name="dialogueText">对话文本</param>
    /// <param name="settings">文本播放配置（优先级高于默认配置）</param>
    /// <param name="windowId">目标窗口ID（默认使用 defaultWindowId）</param>
    /// <param name="onComplete">对话显示完成回调（可选）</param>
    /// <returns>是否触发成功</returns>
    public bool TriggerDialogue(string speakerId, string dialogueText, TextPlaySettings settings, string windowId = null, Action onComplete = null)
    {
        // 防御性校验：UIManager 为空
        if (dialogueUIManager == null)
        {
            Debug.LogError($"[{nameof(DialogueController)}] DialogueUIManager 实例为空，无法触发对话！", this);
            isCurrentDialogueActive = false;
            return false;
        }

        // 防御性校验：文本为空
        if (string.IsNullOrEmpty(dialogueText))
        {
            Debug.LogWarning($"[{nameof(DialogueController)}] 对话文本为空，触发失败！", this);
            isCurrentDialogueActive = false;
            return false;
        }

        // 处理 speakerId 语义：
        // - null/空 → 旁白（使用 narratorSpeakerId，无头像/名字）
        // - 非空但不存在 → 回退到 defaultSpeakerId
        string finalSpeakerId = string.IsNullOrEmpty(speakerId)
            ? narratorSpeakerId
            : (dialogueUIManager.GetSpeakerProfile(speakerId) != null ? speakerId : defaultSpeakerId);

        // 处理窗口ID：默认使用 defaultWindowId
        string finalWindowId = string.IsNullOrEmpty(windowId) ? defaultWindowId : windowId;

        // 处理播放配置：优先使用传入的配置，否则用默认配置
        TextPlaySettings finalSettings = settings ?? defaultPlaySettings;

        // 触发对话（对接 UIManager）
        dialogueUIManager.ShowDialogue(finalSpeakerId, dialogueText, finalWindowId, finalSettings);

        // 注册完成回调（可选）
        if (onComplete != null)
        {
            // 此处可结合 TextSystem 的 OnTextComplete 事件实现回调，示例：
            // 需在 DialogueUIManager/DialogueWindow 中暴露 TextSystem 的完成事件
            // 简化版：假设对话显示后直接触发（实际需根据打字机完成时机调整）
            Invoke(nameof(InvokeCompleteCallback), finalSettings.baseDelay * dialogueText.Length);
            void InvokeCompleteCallback() => onComplete.Invoke();
        }

        isCurrentDialogueActive = true;
        Debug.Log($"[{nameof(DialogueController)}] 对话触发成功 | 角色：{finalSpeakerId} | 窗口：{finalWindowId}", this);
        return true;
    }

    /// <summary>
    /// 触发旁白对话（语义明确的快捷接口）
    /// </summary>
    /// <param name="narratorText">旁白文本</param>
    /// <param name="windowId">旁白窗口ID（默认使用 "center"）</param>
    /// <param name="onComplete">完成回调</param>
    /// <returns>是否触发成功</returns>
    public bool TriggerNarratorDialogue(string narratorText, string windowId = "center", Action onComplete = null)
    {
        // 旁白强制使用 null speakerId（表示无角色），窗口默认 center
        return TriggerDialogue(null, narratorText, windowId ?? "center", onComplete);
    }

    /// <summary>
    /// 结束当前对话（隐藏窗口）
    /// </summary>
    /// <param name="instant">是否立即隐藏（跳过动画）</param>
    public void EndCurrentDialogue(bool instant = false)
    {
        // 空值防护：UIManager 为空时直接返回
        if (dialogueUIManager == null)
        {
            Debug.LogWarning($"[{nameof(DialogueController)}] DialogueUIManager 实例为空，无法结束对话", this);
            return;
        }

        // 隐藏当前对话
        dialogueUIManager.HideCurrentDialogue(instant);
        isCurrentDialogueActive = false;
        Debug.Log($"[{nameof(DialogueController)}] 当前对话已结束", this);
    }
    #endregion

    #region 辅助 API
    /// <summary>
    /// 检查当前是否有激活的对话
    /// </summary>
    public bool IsDialogueActive()
    {
        return isCurrentDialogueActive;
    }

    /// <summary>
    /// 跳过当前对话（立即显示全部文本）
    /// </summary>
    /// <returns>是否跳过成功</returns>
    public bool SkipCurrentDialogue()
    {
        if (dialogueUIManager == null || !isCurrentDialogueActive)
        {
            Debug.LogWarning($"[{nameof(DialogueController)}] 无激活的对话可跳过", this);
            return false;
        }

        // 需在 DialogueUIManager 中扩展 SkipCurrentDialogue 接口，示例：
        // dialogueUIManager.SkipCurrentDialogue();
        Debug.Log($"[{nameof(DialogueController)}] 当前对话已跳过", this);
        return true;
    }
    #endregion
}