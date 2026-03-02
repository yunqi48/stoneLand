using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

/// <summary>
/// 对话UI管理器：单例，管理所有对话框实例、角色配置、窗口池
/// </summary>
public class DialogueUIManager : MonoBehaviour
{
    // 单例实例
    public static DialogueUIManager Instance { get; private set; }

    [Header("预设配置")]
    [SerializeField] private List<DialogueWindow> presetWindows; // 预设对话框
    [SerializeField] private List<SpeakerProfile> speakerProfiles; // 角色配置列表
    [SerializeField] private string defaultWindowKey = "default"; // 默认窗口Key
    [SerializeField] private TextPlaySettings defaultPlaySettings; // 默认文本播放配置

    // 窗口池（Key：窗口ID/名称，Value：对话框实例）
    private Dictionary<string, DialogueWindow> windowPool = new Dictionary<string, DialogueWindow>();
    // 角色缓存（Key：speakerId，Value：角色配置）
    private Dictionary<string, SpeakerProfile> speakerCache = new Dictionary<string, SpeakerProfile>();
    // 当前激活的对话框
    private DialogueWindow activeWindow;

    #region 单例初始化
    private void Awake()
    {
        // 单例逻辑：防止重复实例
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 初始化窗口池（防空、防重复）
        InitWindowPool();

        // 初始化角色缓存（防空）
        InitSpeakerCache();

        // 编辑器校验（仅在编辑器模式下执行）
#if UNITY_EDITOR
        ValidateConfiguration();
#endif
    }

    private void OnDestroy()
    {
        // 清理单例引用，避免残留
        if (Instance == this)
        {
            Instance = null;
        }

        // 清理窗口池（可选：根据需求决定是否销毁窗口）
        ClearWindowPool(false);
    }
    #endregion

    #region 初始化逻辑
    /// <summary>
    /// 初始化窗口池（防空、防重复）
    /// </summary>
    private void InitWindowPool()
    {
        windowPool.Clear();

        if (presetWindows == null || presetWindows.Count == 0)
        {
            Debug.LogWarning($"[{nameof(DialogueUIManager)}] 预设窗口列表为空！", this);
            return;
        }

        foreach (var window in presetWindows)
        {
            // 跳过空窗口
            if (window == null)
            {
                Debug.LogWarning($"[{nameof(DialogueUIManager)}] 预设窗口列表包含空引用！", this);
                continue;
            }

            // 使用窗口的显式ID（优先）或实例ID作为Key，避免命名重复
            string windowKey = GetWindowKey(window);

            // 检查重复Key
            if (windowPool.ContainsKey(windowKey))
            {
                Debug.LogWarning($"[{nameof(DialogueUIManager)}] 重复的窗口Key：{windowKey}，已跳过", this);
                continue;
            }

            // 重置窗口并加入池
            window.ResetWindow();
            windowPool.Add(windowKey, window);

            // 隐藏预设窗口
            window.HideWindow(true);
        }
    }

    /// <summary>
    /// 初始化角色缓存（防空）
    /// </summary>
    private void InitSpeakerCache()
    {
        speakerCache.Clear();

        if (speakerProfiles == null || speakerProfiles.Count == 0)
        {
            Debug.LogWarning($"[{nameof(DialogueUIManager)}] 角色配置列表为空！", this);
            return;
        }

        foreach (var profile in speakerProfiles)
        {
            if (profile == null || string.IsNullOrEmpty(profile.speakerId))
            {
                Debug.LogWarning($"[{nameof(DialogueUIManager)}] 角色配置包含空引用或无效ID！", this);
                continue;
            }

            // 覆盖重复的speakerId（最后一个生效）
            speakerCache[profile.speakerId] = profile;
        }
    }

    /// <summary>
    /// 获取窗口的唯一Key（优先使用显式ID，否则用实例ID）
    /// </summary>
    private string GetWindowKey(DialogueWindow window)
    {
        // 你可以在DialogueWindow中添加public string windowId字段，优先使用
        // if (!string.IsNullOrEmpty(window.windowId)) return window.windowId;

        // 备用方案：使用实例ID，保证唯一性
        return window.gameObject.GetInstanceID().ToString();
    }
    #endregion

    #region 核心API
    /// <summary>
    /// 显示对话（核心接口）
    /// </summary>
    /// <param name="speakerId">说话者ID</param>
    /// <param name="text">对话文本</param>
    /// <param name="windowKey">指定窗口Key（默认使用default）</param>
    /// <param name="playSettings">文本播放配置（默认使用全局配置）</param>
    public void ShowDialogue(string speakerId, string text, string windowKey = null, TextPlaySettings playSettings = null)
    {
        // 1. 获取目标窗口（空值校验）
        DialogueWindow targetWindow = GetDialogueWindow(windowKey ?? defaultWindowKey);
        if (targetWindow == null)
        {
            Debug.LogError($"[{nameof(DialogueUIManager)}] 未找到指定窗口：{windowKey ?? defaultWindowKey}", this);
            return;
        }

        // 2. 获取角色配置
        SpeakerProfile speaker = GetSpeakerProfile(speakerId);

        // 3. 隐藏当前激活的窗口
        if (activeWindow != null && activeWindow != targetWindow)
        {
            activeWindow.HideWindow();
        }

        // 4. 设置窗口内容（关键修改：优先使用传入的配置，否则用默认配置）
        targetWindow.SetSpeaker(speaker);
        TextPlaySettings finalSettings = playSettings ?? defaultPlaySettings;
        targetWindow.SetDialogueText(text, finalSettings);

        // 5. 显示窗口
        targetWindow.ShowWindow();

        // 6. 更新激活窗口
        activeWindow = targetWindow;
    }

    /// <summary>
    /// 获取对话框实例（支持按Key/名称/索引）
    /// </summary>
    /// <param name="key">窗口Key/名称/索引（字符串）</param>
    public DialogueWindow GetDialogueWindow(string key)
    {
        // 空值校验
        if (string.IsNullOrEmpty(key) || windowPool.Count == 0)
        {
            return null;
        }

        // 1. 优先按Key查找
        if (windowPool.TryGetValue(key, out DialogueWindow windowByKey))
        {
            return windowByKey;
        }

        // 2. 按窗口名称模糊查找（兼容旧逻辑）
        foreach (var kvp in windowPool)
        {
            if (kvp.Value.gameObject.name.Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                return kvp.Value;
            }
        }

        // 3. 按索引查找（支持数字字符串）
        if (int.TryParse(key, out int index) && index >= 0 && index < presetWindows.Count)
        {
            return presetWindows[index];
        }

        // 4. 返回第一个窗口（降级方案）
        return windowPool.Count > 0 ? windowPool.Values.GetEnumerator().Current : null;
    }

    /// <summary>
    /// 获取角色配置
    /// </summary>
    public SpeakerProfile GetSpeakerProfile(string speakerId)
    {
        if (string.IsNullOrEmpty(speakerId) || speakerCache.Count == 0)
        {
            return null;
        }

        speakerCache.TryGetValue(speakerId, out SpeakerProfile speaker);
        return speaker;
    }

    /// <summary>
    /// 注册角色配置（防重复）
    /// </summary>
    public void RegisterSpeakerProfile(SpeakerProfile profile)
    {
        // 空值/无效ID校验
        if (profile == null || string.IsNullOrEmpty(profile.speakerId))
        {
            Debug.LogWarning($"[{nameof(DialogueUIManager)}] 无效的角色配置！", this);
            return;
        }

        // 更新缓存
        speakerCache[profile.speakerId] = profile;

        // 防重复添加到列表
        if (speakerProfiles == null)
        {
            speakerProfiles = new List<SpeakerProfile>();
        }

        if (!speakerProfiles.Contains(profile))
        {
            speakerProfiles.Add(profile);
        }
    }

    /// <summary>
    /// 注销角色配置
    /// </summary>
    public void UnregisterSpeakerProfile(string speakerId)
    {
        if (string.IsNullOrEmpty(speakerId))
        {
            return;
        }

        // 从缓存移除
        if (speakerCache.ContainsKey(speakerId))
        {
            // 从列表移除（如果存在）
            var profile = speakerCache[speakerId];
            if (speakerProfiles != null && speakerProfiles.Contains(profile))
            {
                speakerProfiles.Remove(profile);
            }

            speakerCache.Remove(speakerId);
        }
    }
    #endregion

    #region 窗口池管理
    /// <summary>
    /// 注册新的对话框到池（运行时动态添加）
    /// </summary>
    public bool RegisterDialogueWindow(DialogueWindow window, string customKey = null)
    {
        if (window == null)
        {
            Debug.LogWarning($"[{nameof(DialogueUIManager)}] 无法注册空窗口！", this);
            return false;
        }

        string windowKey = customKey ?? GetWindowKey(window);

        if (windowPool.ContainsKey(windowKey))
        {
            Debug.LogWarning($"[{nameof(DialogueUIManager)}] 窗口Key已存在：{windowKey}", this);
            return false;
        }

        windowPool.Add(windowKey, window);
        presetWindows?.Add(window);
        window.ResetWindow();

        return true;
    }

    /// <summary>
    /// 从池注销对话框
    /// </summary>
    public bool UnregisterDialogueWindow(string windowKey, bool destroyWindow = false)
    {
        if (!windowPool.TryGetValue(windowKey, out DialogueWindow window))
        {
            return false;
        }

        // 从列表移除
        if (presetWindows != null && presetWindows.Contains(window))
        {
            presetWindows.Remove(window);
        }

        // 销毁窗口（可选）
        if (destroyWindow)
        {
            Destroy(window.gameObject);
        }
        else
        {
            window.ResetWindow();
        }

        windowPool.Remove(windowKey);

        // 如果是激活窗口，清空
        if (activeWindow == window)
        {
            activeWindow = null;
        }

        return true;
    }

    /// <summary>
    /// 清空窗口池
    /// </summary>
    public void ClearWindowPool(bool destroyWindows = false)
    {
        foreach (var kvp in windowPool)
        {
            if (destroyWindows)
            {
                Destroy(kvp.Value.gameObject);
            }
            else
            {
                kvp.Value.ResetWindow();
            }
        }

        windowPool.Clear();
        presetWindows?.Clear();
        activeWindow = null;
    }
    #endregion

    #region 编辑器校验（可选）
    /// <summary>
    /// 编辑器模式下校验配置错误
    /// </summary>
    [ContextMenu("校验配置")]
    private void ValidateConfiguration()
    {
        // 检查默认窗口是否存在
        if (!string.IsNullOrEmpty(defaultWindowKey) && windowPool.Count > 0)
        {
            var defaultWindow = GetDialogueWindow(defaultWindowKey);
            if (defaultWindow == null)
            {
                Debug.LogWarning($"[{nameof(DialogueUIManager)}] 默认窗口 {defaultWindowKey} 不存在！", this);
            }
        }

        // 检查角色ID重复
        HashSet<string> duplicateSpeakerIds = new HashSet<string>();
        if (speakerProfiles != null)
        {
            foreach (var profile in speakerProfiles)
            {
                if (profile == null || string.IsNullOrEmpty(profile.speakerId))
                {
                    continue;
                }

                if (duplicateSpeakerIds.Contains(profile.speakerId))
                {
                    Debug.LogError($"[{nameof(DialogueUIManager)}] 重复的角色ID：{profile.speakerId}", profile);
                }
                else
                {
                    duplicateSpeakerIds.Add(profile.speakerId);
                }
            }
        }

        Debug.Log($"[{nameof(DialogueUIManager)}] 配置校验完成！", this);
    }
    #endregion

    #region 辅助方法
    /// <summary>
    /// 隐藏当前对话
    /// </summary>
    public void HideCurrentDialogue(bool instant = false)
    {
        if (activeWindow != null)
        {
            activeWindow.HideWindow(instant);
            activeWindow = null;
        }
    }

    /// <summary>
    /// 重置所有对话窗口
    /// </summary>
    public void ResetAllWindows()
    {
        foreach (var window in windowPool.Values)
        {
            window.ResetWindow();
        }

        activeWindow = null;
    }
    #endregion
}