using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

/// <summary>
/// 文本播放状态类
/// 存储文本打字机播放过程中的所有动态状态数据，包括播放进度、配置、暂停/等待状态、事件指针等
/// 核心作用：将播放状态与控制逻辑解耦，便于状态管理和多文本播放隔离
/// </summary>
public class TextPlayState
{
    #region 基础文本数据
    /// <summary>
    /// 原始未解析的文本（包含控制标签，如 [pause]、[wait]）
    /// </summary>
    public string rawText;

    /// <summary>
    /// 解析后的文本对象（已去除控制标签，分离纯文本和控制事件）
    /// 关联 ParsedText 类，包含可见字符长度、控制事件列表等
    /// </summary>
    public ParsedText parsedText;

    /// <summary>
    /// 当前播放到的可见字符索引（从0开始，仅计数纯文本，不包含控制标签）
    /// 例如：索引5表示已播放前5个可见字符
    /// </summary>
    public int currentCharIndex = 0;

    /// <summary>
    /// 上一个字符播放完成后累积的时间（秒）
    /// 用于修复低帧率下打字间隔不准的问题（时间累积逻辑）
    /// </summary>
    public float elapsedSinceLastChar = 0f;

    /// <summary>
    /// 当前文本的播放配置（如基础打字间隔、是否播放音效、是否允许跳过等）
    /// 关联 TextPlaySettings 类，存储静态配置项
    /// </summary>
    public TextPlaySettings settings;
    #endregion

    #region 取消与速度控制
    /// <summary>
    /// 取消令牌源（CancellationTokenSource）
    /// 用于强制终止打字协程，避免协程泄漏或无效执行
    /// </summary>
    public CancellationTokenSource cts;

    /// <summary>
    /// 当前播放速度缩放系数（默认1f=正常速度）
    /// 可通过 [speed] 控制标签动态修改，例如 2f=2倍速，0.5f=0.5倍速
    /// </summary>
    public float currentSpeedScale = 1f;

    /// <summary>
    /// 是否开启快进模式（软跳过）
    /// 快进时打字间隔会大幅缩短（通常为原间隔的1/10），但不终止协程
    /// </summary>
    public bool isFastForward = false;
    #endregion

    #region 事件处理指针
    /// <summary>
    /// 下一个待处理控制事件的索引（事件指针）
    /// 用于高效遍历控制事件列表，避免重复处理同一事件
    /// 例如：索引3表示前3个事件已处理，下次从第4个事件开始检查
    /// </summary>
    public int nextEventIndex = 0;
    #endregion

    #region 暂停/等待状态管理（核心细分）
    /// <summary>
    /// 计时暂停请求计数器（支持嵌套暂停）
    /// 数值>0表示处于计时暂停状态，每次调用 AddPauseRequest() 加1，RemovePauseRequest() 减1
    /// 场景：手动暂停、暂停标签 [pause] 触发的暂停
    /// </summary>
    public int pauseRequestCount = 0;

    /// <summary>
    /// 是否处于「等待用户输入」状态
    /// 由 [wait] 控制标签触发，需用户点击「继续」按钮才能恢复播放
    /// </summary>
    public bool waitingForInput = false;

    /// <summary>
    /// 标记是否处于「暂停标签」的暂停状态（区别于手动暂停）
    /// 用于区分暂停类型，避免手动恢复覆盖标签暂停的逻辑
    /// </summary>
    public bool isPausing;
    #endregion

    #region 状态计算与操作方法
    /// <summary>
    /// 只读计算属性：判断当前是否处于暂停状态（包含计时暂停/等待输入）
    /// 规则：暂停请求计数>0 或 等待输入=true 时，判定为暂停
    /// </summary>
    public bool IsPaused => pauseRequestCount > 0 || waitingForInput;

    /// <summary>
    /// 增加计时暂停请求（支持嵌套暂停）
    /// 例如：连续调用2次 AddPauseRequest()，需调用2次 RemovePauseRequest() 才能恢复
    /// </summary>
    public void AddPauseRequest() => pauseRequestCount++;

    /// <summary>
    /// 减少计时暂停请求（最小值为0，避免负数）
    /// Mathf.Max(0, ...) 确保计数器不会小于0，防止逻辑异常
    /// </summary>
    public void RemovePauseRequest() => pauseRequestCount = Mathf.Max(0, pauseRequestCount - 1);
    #endregion
}