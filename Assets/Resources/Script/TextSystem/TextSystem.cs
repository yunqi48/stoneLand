using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 文本打字机系统核心控制器
/// 负责文本的逐字播放、事件处理、音效同步、暂停/跳过/等待输入等核心逻辑
/// </summary>
[RequireComponent(typeof(TextRenderer), typeof(TagParser))] // 自动依赖组件，避免缺失
public class TextSystem : MonoBehaviour
{
    #region 组件配置（Inspector 可配置）
    [SerializeField]
    [Tooltip("文本渲染器：负责文本的可视化显示")]
    private TextRenderer textRenderer;

    [SerializeField]
    [Tooltip("标签解析器：负责解析文本中的控制标签")]
    private TagParser tagParser;

    [SerializeField]
    [Tooltip("打字音效控制器：负责同步播放打字音效")]
    private TypeSoundController soundController;

    [SerializeField]
    [Tooltip("默认播放设置：文本播放的基础配置")]
    private TextPlaySettings defaultSettings;
    #endregion

    #region 事件回调（供外部订阅）
    /// <summary>
    /// 文本播放完成时触发的事件
    /// </summary>
    public UnityEvent OnTextComplete;

    /// <summary>
    /// 文本播放到「等待输入」标签时触发的事件
    /// </summary>
    public UnityEvent OnWaitForInput;

    /// <summary>
    /// 从「等待输入」状态恢复播放时触发的事件
    /// </summary>
    public UnityEvent OnResumeFromWait;
    #endregion

    #region 私有状态管理
    /// <summary>
    /// 当前文本播放的状态对象（存储播放进度、配置、事件等）
    /// </summary>
    private TextPlayState currentPlayState;
    /// <summary>
    /// 打字协程引用（用于暂停/停止协程）
    /// </summary>
    private Coroutine typingCoroutine;
    /// <summary>
    /// 逐帧动态变化计数
    /// </summary>
    private float currentDelay;
    #endregion

    /// <summary>
    /// 只读属性：是否正在播放文本（未暂停、未等待输入）
    /// </summary>
    public bool IsPlaying => typingCoroutine != null && currentPlayState != null && !currentPlayState.IsPaused;
    /// <summary>
    /// 只读属性：是否处于「等待输入」状态
    /// </summary>
    public bool IsWaitingForInput => currentPlayState != null && currentPlayState.waitingForInput;

    #region 生命周期方法
    /// <summary>
    /// 初始化：自动获取依赖组件 + 初始化默认播放设置
    /// </summary>
    private void Awake()
    {
        // 自动获取组件
        if (textRenderer == null) textRenderer = GetComponent<TextRenderer>();
        if (tagParser == null) tagParser = GetComponent<TagParser>();
        if (soundController == null) soundController = GetComponent<TypeSoundController>();

        defaultSettings = new TextPlaySettings
        {
            baseDelay = 0.05f,       // 基础打字间隔（可自定义）
            playTypeSound = true,     // 是否播放打字音效
            autoAdvanceOnComplete = true, // 文本完成后是否自动触发完成事件
            allowSkip = true,         // 是否允许跳过
            skipWaitOnHardSkip = true // 硬跳过时是否跳过等待
        };
    }

    /// <summary>
    /// 销毁时清理资源：释放 CancellationTokenSource，避免内存泄漏
    /// </summary>
    private void OnDestroy()
    {
        // 清理CTS，避免资源泄漏
        if (currentPlayState?.cts != null)
        {
            currentPlayState.cts.Cancel();
            currentPlayState.cts.Dispose();
            currentPlayState.cts = null;
        }
    }
    #endregion

    #region
    // 播放文本（核心接口）
    public void PlayText(string rawText)
    {
        PlayText(rawText, defaultSettings);
    }

    /// <summary>
    /// 播放文本（核心接口：自定义播放设置）
    /// </summary>
    /// <param name="rawText">原始文本（可包含控制标签）</param>
    /// <param name="settings">自定义播放设置</param>
    public void PlayText(string rawText, TextPlaySettings settings)
    {
        // 停止当前正在播放的文本（避免多文本同时播放）
        AbortCurrent();

        // 初始化播放状态对象
        currentPlayState = new TextPlayState
        {
            rawText = rawText,                          // 原始文本
            parsedText = tagParser.Parse(rawText),      // 解析后的文本（含控制事件）
            settings = settings,                        // 播放设置
            cts = new CancellationTokenSource(),        // 取消令牌（用于终止协程）
            currentSpeedScale = 1f,                     // 初始播放速度缩放（1倍速）
            isFastForward = false,                      // 初始未快进
            nextEventIndex = 0,                         // 下一个待处理事件的索引
            pauseRequestCount = 0,                      // 暂停请求计数器（支持嵌套暂停）
            waitingForInput = false                     // 初始未等待输入
        };

        // 空值防护：文本渲染器为空时终止播放
        if (textRenderer == null)
        {
            Debug.LogError("TextSystem 缺少 TextRenderer 组件，终止文本播放！");
            currentPlayState.cts.Cancel();
            currentPlayState.cts.Dispose();
            currentPlayState = null;
            return;
        }

        // 启动打字协程（核心播放逻辑）
        typingCoroutine = StartCoroutine(RunTypingCoroutine(currentPlayState));
    }
    #endregion

    #region 核心协程：逐字播放文本
    /// <summary>
    /// 打字核心协程：处理逐字播放、暂停、变速、等待输入、事件触发等逻辑
    /// 优化点：修复时间累积误差、事件指针遍历（避免重复处理）、空值防护
    /// </summary>
    /// <param name="state">当前播放状态</param>
    /// <returns>协程迭代器</returns>
    private IEnumerator RunTypingCoroutine(TextPlayState state)
    {
        // 初始化：清空文本渲染器 + 重置音效计数器
        textRenderer.Clear();
        soundController?.ResetCounter(); // 空值防护：避免音效控制器为空时报错

        // 获取解析后文本的可见字符总数（排除控制标签）
        int totalVisibleChars = state.parsedText.VisibleLength;
        // 当前打字间隔（动态调整：基础间隔 / 速度缩放）

        // 主循环：逐字播放文本（直到播放完成或被取消）
        while (state.currentCharIndex < totalVisibleChars && !state.cts.Token.IsCancellationRequested)
        {
            // 1. 处理暂停状态（主动暂停 + 暂停标签触发的暂停）
            while (state.IsPaused || state.isPausing)
            {
                yield return null; // 暂停时帧等待
                if (state.cts.Token.IsCancellationRequested) break; // 被取消则退出循环
            }

            // 2. 处理等待输入状态（WaitForInput 标签触发）
            while (state.waitingForInput)
            {
                yield return null; // 等待输入时帧等待
                if (state.cts.Token.IsCancellationRequested) break; // 被取消则退出循环
            }

            // 计算当前实际打字间隔（快进时速度×10）
            currentDelay = state.settings.baseDelay / (state.isFastForward ? 10f : state.currentSpeedScale);

            // 时间累积逻辑：修复低帧率下打字间隔不准的问题
            state.elapsedSinceLastChar += Time.deltaTime;
            while (state.elapsedSinceLastChar >= currentDelay && state.currentCharIndex < totalVisibleChars
                   && !state.IsPaused && !state.isPausing && !state.waitingForInput)
            {
                // 播放下一个字符
                state.currentCharIndex++;
                // 更新文本渲染器显示范围
                textRenderer.UpdateVisibleRange(state.currentCharIndex, state.parsedText);
                // 处理当前字符对应的控制事件
                ProcessEventsForCurrentChar(state);

                // 播放打字音效（空值防护 + 配置开关）
                if (state.settings.playTypeSound && soundController != null)
                {
                    soundController.PlayTypeSound();
                }

                // 扣除已消耗的时间（剩余时间累积到下一帧）
                state.elapsedSinceLastChar -= currentDelay;
            }

            yield return null; // 帧等待：避免协程占用过多性能
        }

        // 文本播放完成逻辑（未被取消时执行）
        if (!state.cts.Token.IsCancellationRequested)
        {
            // 显示全部文本（避免字符遗漏）
            textRenderer.ShowInstant(state.parsedText.plainText);
            // 停止所有打字音效（文本完成后不再播放）
            soundController?.StopAllSoundsOnTextComplete();

            // 自动触发完成事件（根据配置）
            if (state.settings.autoAdvanceOnComplete)
            {
                OnTextComplete?.Invoke();
            }
        }

        // 清理播放状态 + 重置协程引用
        CleanupPlayState(state);
        typingCoroutine = null;
        currentPlayState = null;
    }
#endregion

    // 处理当前字符的事件（指针遍历，高效且不重复）
    private void ProcessEventsForCurrentChar(TextPlayState state)
    {
        if (state.parsedText.events == null || state.nextEventIndex >= state.parsedText.events.Count)
            return;

        // 处理所有索引等于当前字符的未触发事件
        while (state.nextEventIndex < state.parsedText.events.Count)
        {
            var evt = state.parsedText.events[state.nextEventIndex];

            // 事件索引超过当前字符，停止处理
            if (evt.index > state.currentCharIndex)
                break;

            // 跳过已触发的事件
            if (evt.isTriggered)
            {
                state.nextEventIndex++;
                continue;
            }

            // 处理事件
            switch (evt.type)
            {
                case ControlEventType.Pause:
                    StartCoroutine(HandlePauseEvent(state, evt.value));
                    break;
                case ControlEventType.SpeedChange:
                    state.currentSpeedScale = evt.value;
                    break;
                case ControlEventType.WaitForInput:
                    state.waitingForInput = true;
                    OnWaitForInput?.Invoke();
                    break;
            }

            // 标记为已触发并移动指针
            evt.isTriggered = true;
            state.nextEventIndex++;
        }
    }

    /// <summary>
    /// 处理暂停事件（使用计数器解决嵌套暂停竞态问题）
    /// </summary>
    /// <param name="state">当前播放状态</param>
    /// <param name="pauseTime">暂停时长（秒）</param>
    /// <returns>协程迭代器</returns>
    private IEnumerator HandlePauseEvent(TextPlayState state, float pauseTime)
    {
        // 空值/无效时长防护
        if (state == null || pauseTime <= 0) yield break;

        // 标记暂停状态 + 增加暂停请求计数（支持嵌套暂停）
        state.isPausing = true;
        state.AddPauseRequest();

        // 实际等待指定时长（帧更新，支持中途取消）
        float elapsed = 0;
        while (elapsed < pauseTime && !state.cts.Token.IsCancellationRequested)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        // 恢复播放：减少暂停请求计数 + 重置暂停状态
        state.RemovePauseRequest();
        state.isPausing = false;
    }

    // 跳过当前文本（区分硬跳过/软跳过）
    /// <summary>
    /// 跳过当前文本
    /// </summary>
    /// <param name="isHardSkip">是否硬跳过（跳过所有等待，直接完成）</param>
    public void SkipCurrent(bool isHardSkip = true)
    {
        if (currentPlayState == null || !currentPlayState.settings.allowSkip)
            return;

        soundController?.StopAllPendingSounds();

        if (isHardSkip)
        {
            // 硬跳过：直接显示全部，触发完成事件，忽略等待
            textRenderer.ShowInstant(currentPlayState.parsedText.plainText);

            if (currentPlayState.waitingForInput && currentPlayState.settings.skipWaitOnHardSkip)
            {
                currentPlayState.waitingForInput = false;
                OnResumeFromWait?.Invoke();
            }

            if (typingCoroutine != null)
            {
                StopCoroutine(typingCoroutine);
                typingCoroutine = null;
            }

            OnTextComplete?.Invoke();
            CleanupPlayState(currentPlayState);
            currentPlayState = null;
        }
        else
        {
            // 软跳过：加速播放，保留等待输入逻辑
            currentPlayState.isFastForward = true;
        }
    }

    /// <summary>
    /// 暂停文本播放（仅暂停计时，不影响等待输入状态）
    /// </summary>
    public void Pause()
    {
        if (currentPlayState != null && !currentPlayState.waitingForInput)
        {
            currentPlayState.AddPauseRequest();
        }
    }

    /// <summary>
    /// 恢复文本播放（恢复计时暂停 + 恢复等待输入状态）
    /// </summary>
    public void Resume()
    {
        if (currentPlayState == null) return;

        // 恢复计时暂停
        currentPlayState.pauseRequestCount = 0;

        // 恢复输入等待
        if (currentPlayState.waitingForInput)
        {
            currentPlayState.waitingForInput = false;
            OnResumeFromWait?.Invoke();
        }
    }

    /// <summary>
    /// 强制终止当前文本播放（清空状态 + 停止协程）
    /// </summary>
    public void AbortCurrent()
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }

        if (currentPlayState != null)
        {
            CleanupPlayState(currentPlayState);
            currentPlayState = null;
        }
        soundController?.StopAllPendingSounds();

        textRenderer.Clear();
    }

    /// <summary>
    /// 设置快进模式（加速播放文本）
    /// </summary>
    /// <param name="enabled">是否开启快进</param>
    public void SetFastForward(bool enabled)
    {
        if (currentPlayState != null)
        {
            currentPlayState.isFastForward = enabled;
        }
    }

    #region 辅助方法
    /// <summary>
    /// 清理播放状态：释放 CancellationTokenSource 资源
    /// </summary>
    /// <param name="state">待清理的播放状态</param>
    private void CleanupPlayState(TextPlayState state)
    {
        if (state.cts != null)
        {
            state.cts.Cancel();
            state.cts.Dispose();
            state.cts = null;
        }
    }
    #endregion

    #region 辅助接口
    /// <summary>
    /// 恢复等待输入状态（专门用于处理WaitForInput事件）
    /// </summary>
    public void ResumeFromWait()
    {
        if (currentPlayState != null && currentPlayState.waitingForInput)
        {
            currentPlayState.waitingForInput = false;
            OnResumeFromWait?.Invoke();
        }
    }
    #endregion
}