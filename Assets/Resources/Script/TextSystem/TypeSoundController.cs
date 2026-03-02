using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 打字音效控制器
/// 核心功能：
/// 1. 为文本打字机提供音效播放支持（串行/并行播放）
/// 2. 音频源池化管理（避免频繁创建/销毁AudioSource）
/// 3. 音效随机化（音调/音量），避免机械感
/// 4. 字符过滤（忽略空格/标点等无声字符）
/// 5. 文本完成时停止所有音效（适配TextSystem的播放完成逻辑）
/// 已移除：每N个字符播放一次音效的频率控制逻辑
/// </summary>
[RequireComponent(typeof(AudioSource))] // 依赖AudioSource基础组件（池化创建，此处仅标记）
public class TypeSoundController : MonoBehaviour
{
    #region Inspector 配置项（按功能分组）
    [Header("基础配置")]
    [Tooltip("打字音效数组：随机选择其中一个播放，增加音效多样性")]
    [SerializeField] private AudioClip[] typeClips;

    [Tooltip("音调随机变化范围：1±该值，例如0.1表示音调在0.9~1.1之间随机")]
    [SerializeField] private float pitchVariance = 0.1f;

    [Tooltip("音频源池大小：同时可播放的最大音效数量（避免音效重叠卡顿）")]
    [SerializeField] private int poolSize = 5;

    [Header("随机化配置")]
    [Tooltip("音量随机变化范围：在min~maxVolume基础上±该值，增强自然感")]
    [SerializeField] private float volumeVariance = 0.05f;

    [Tooltip("最小音量：随机音量的下限（0~1）")]
    [SerializeField] private float minVolume = 0.8f;

    [Tooltip("最大音量：随机音量的上限（0~1）")]
    [SerializeField] private float maxVolume = 1.0f;

    [Header("字符过滤配置")]
    [Tooltip("忽略的字符列表：这些字符不触发音效（空格、标点等无声字符）")]
    [SerializeField] private string ignoreCharacters = " ,.!?:;，。！？；\t\n\r";

    [Header("串行播放配置")]
    [Tooltip("是否开启串行播放：音效按顺序播放，避免重叠（适合短音效）")]
    [SerializeField] private bool enableSerialPlay = true;

    [Tooltip("串行播放额外延迟：前一个音效播放完毕后，额外等待的时长（秒）")]
    [SerializeField] private float serialPlayDelay = 0f;
    #endregion

    #region 扩展与内部状态
    /// <summary>
    /// 可注入的字符判断逻辑（优先级高于内置过滤规则）
    /// 外部可自定义「哪些字符需要播放音效」的规则
    /// </summary>
    public Func<char, bool> ShouldPlayForChar;

    /// <summary>
    /// 音频源池项（扩展信息）
    /// 存储AudioSource + 使用时间 + 剩余播放时长，便于池化管理
    /// </summary>
    private class PooledAudioSource
    {
        public AudioSource Source;          // 音频源组件
        public float LastUsedTime;          // 最后一次使用时间（用于最久未使用复用策略）
        /// <summary>
        /// 计算属性：音频源剩余播放时长（秒）
        /// 无Clip时返回0，否则返回 Clip总时长 - 当前播放位置
        /// </summary>
        public float TimeRemaining => Source.clip == null ? 0 : Source.clip.length - Source.time;
    }

    /// <summary>
    /// 音频源池：存储所有池化的AudioSource，避免频繁创建/销毁
    /// </summary>
    private List<PooledAudioSource> audioSourcePool;

    // ========== 2. 移除：注释/删除 playCounter（频率计数）==========
    // private int playCounter = 0; // 音效播放计数器（用于频率控制，已移除）

    /// <summary>
    /// 环形索引：优化空闲音频源查找，避免每次从头扫描
    /// </summary>
    private int lastPoolIndex = 0;

    /// <summary>
    /// 全局音量缩放：由外部AudioManager控制，统一调整所有音效音量
    /// 取值范围0~1，最终音量 = 随机音量 × 该值
    /// </summary>
    private float globalVolumeScale = 1f;

    #region 串行播放核心字段
    /// <summary>
    /// 音效播放请求队列：串行播放时存储待执行的播放操作
    /// </summary>
    private Queue<Action> soundPlayQueue;

    /// <summary>
    /// 标记是否正在处理串行播放队列
    /// 避免重复启动协程
    /// </summary>
    private bool isPlayingSerialSound;

    /// <summary>
    /// 串行播放协程引用：用于停止/暂停协程
    /// </summary>
    private Coroutine serialPlayCoroutine;
    #endregion
    #endregion

    #region 生命周期方法
    /// <summary>
    /// 初始化：创建音频源池 + 初始化串行播放队列
    /// </summary>
    private void Awake()
    {
        // 初始化音频源池（优化：设置hideFlags避免编辑器污染）
        audioSourcePool = new List<PooledAudioSource>();
        for (int i = 0; i < poolSize; i++)
        {
            // 创建独立的音效游戏对象
            GameObject audioObj = new GameObject($"TypeSoundSource_{i}");
            // 隐藏对象并禁止保存，避免编辑器场景污染
            audioObj.hideFlags = HideFlags.HideAndDontSave | HideFlags.DontSaveInBuild;
            audioObj.transform.SetParent(transform); // 挂载到当前对象下，便于管理

            // 添加AudioSource组件并配置基础参数
            AudioSource source = audioObj.AddComponent<AudioSource>();
            source.playOnAwake = false;    // 不自动播放
            source.spatialBlend = 0f;      // 2D音效（无3D空间衰减）
            source.volume = maxVolume;     // 初始音量设为最大值

            // 添加到音频源池
            audioSourcePool.Add(new PooledAudioSource
            {
                Source = source,
                LastUsedTime = Time.time
            });
        }

        // 初始化串行播放队列
        soundPlayQueue = new Queue<Action>();
        isPlayingSerialSound = false;
        serialPlayCoroutine = null;
    }

    /// <summary>
    /// 销毁时清理资源：释放音频源池 + 停止串行播放协程
    /// 避免内存泄漏和无效协程执行
    /// </summary>
    private void OnDestroy()
    {
        // 清理池对象，避免内存泄漏
        if (audioSourcePool == null) return;
        foreach (var pooled in audioSourcePool)
        {
            if (pooled.Source != null)
            {
                Destroy(pooled.Source.gameObject); // 销毁音效对象（连带AudioSource）
            }
        }
        audioSourcePool.Clear();

        // 清理串行播放资源
        if (soundPlayQueue != null) soundPlayQueue.Clear(); // 清空播放队列
        if (serialPlayCoroutine != null)
        {
            StopCoroutine(serialPlayCoroutine); // 停止协程
            serialPlayCoroutine = null;
        }
        isPlayingSerialSound = false;
    }

    /// <summary>
    /// 应用退出时清理资源（编辑器/运行时通用）
    /// 避免残留音效对象
    /// </summary>
    private void OnApplicationQuit()
    {
        OnDestroy();
    }

#if UNITY_EDITOR
    /// <summary>
    /// 编辑器模式下禁用时清理资源
    /// 避免停止Play模式后残留音效对象
    /// </summary>
    private void OnDisable()
    {
        if (!UnityEditor.EditorApplication.isPlaying)
        {
            OnDestroy();
        }
    }
#endif
    #endregion

    #region 外部控制接口
    /// <summary>
    /// 外部设置全局音量缩放（对接AudioManager）
    /// </summary>
    /// <param name="scale">音量缩放系数（0~1）</param>
    public void SetGlobalVolumeScale(float scale)
    {
        globalVolumeScale = Mathf.Clamp01(scale); // 限制取值范围，避免负数/超过1
    }

    // ========== 3. 移除：Configure 方法中 playFrequency 相关逻辑（保留方法避免报错）==========
    /// <summary>
    /// 配置音效参数（兼容原有接口，已移除频率控制）
    /// </summary>
    /// <param name="clips">音效数组</param>
    /// <param name="pitchVariance">音调变化范围</param>
    /// <param name="frequency">播放频率（已弃用，参数保留避免报错）</param>
    public void Configure(AudioClip[] clips, float pitchVariance, int frequency)
    {
        // 空值安全处理：过滤空Clip，避免播放空音效
        if (clips != null)
        {
            typeClips = clips.Where(c => c != null).ToArray();
        }
        // 避免音调变化范围为负数
        this.pitchVariance = Mathf.Max(0, pitchVariance);
        // 注释：移除频率设置（已改为每个有效字符播放）
        // playFrequency = Mathf.Max(1, frequency);
    }

    /// <summary>
    /// 核心：按字符判断是否播放音效（对外扩展接口）
    /// </summary>
    /// <param name="visibleIndex">字符可见索引（未使用，保留参数兼容原有逻辑）</param>
    /// <param name="c">当前字符</param>
    /// <returns>是否成功触发音效播放</returns>
    public bool PlayForVisibleChar(int visibleIndex, char c)
    {
        // 第一步：判断是否需要为该字符播放音效（内置/自定义过滤规则）
        if (!ShouldPlayForCharacter(c))
        {
            return false;
        }
        // 第三步：实际播放音效（串行/并行逻辑分支）
        if (enableSerialPlay)
        {
            // 串行播放：将播放操作加入队列，由协程处理
            soundPlayQueue.Enqueue(() => TryPlayTypeSound());
            StartSerialPlayCoroutine(); // 启动队列处理协程（防护重复启动）
            return true; // 串行模式返回true表示加入队列成功
        }
        else
        {
            // 并行播放：直接尝试播放音效
            return TryPlayTypeSound();
        }
    }

    /// <summary>
    /// 简化接口：直接播放音效（兼容原有逻辑，不判断字符）
    /// </summary>
    public void PlayTypeSound()
    {
        if (enableSerialPlay)
        {
            // 串行播放：加入队列
            soundPlayQueue.Enqueue(() => TryPlayTypeSound());
            StartSerialPlayCoroutine();
        }
        else
        {
            // 并行播放：直接播放
            TryPlayTypeSound();
        }
    }

    // ========== 5. 新增：文本打印完成时停止所有音效（对外接口，供TextSystem调用）==========
    /// <summary>
    /// 文本打印完成时调用：停止所有音效播放、清空队列
    /// 核心作用：避免文本完成后仍有残留音效播放
    /// </summary>
    public void StopAllSoundsOnTextComplete()
    {
        // 1. 清空串行播放队列（停止待播放的音效）
        if (soundPlayQueue != null) soundPlayQueue.Clear();

        // 2. 停止串行播放协程
        if (serialPlayCoroutine != null)
        {
            StopCoroutine(serialPlayCoroutine);
            serialPlayCoroutine = null;
        }
        isPlayingSerialSound = false;

        // 3. 停止所有正在播放的音频源
        if (audioSourcePool != null)
        {
            foreach (var pooled in audioSourcePool)
            {
                if (pooled.Source != null && pooled.Source.isPlaying)
                {
                    pooled.Source.Stop();
                }
            }
        }

        // 4. 重置计数器（保留，避免后续逻辑报错）
        ResetCounter();
    }

    /// <summary>
    /// 重置状态（兼容原有接口）
    /// 清空队列 + 停止协程 + 停止所有音频源
    /// </summary>
    public void ResetCounter()
    {
        // ========== 6. 移除：注释 playCounter 重置 ==========
        // playCounter = 0;

        // 重置串行播放队列
        if (soundPlayQueue != null) soundPlayQueue.Clear();
        if (serialPlayCoroutine != null)
        {
            StopCoroutine(serialPlayCoroutine);
            serialPlayCoroutine = null;
        }
        isPlayingSerialSound = false;

        // 停止所有正在播放的音频源
        foreach (var pooled in audioSourcePool)
        {
            if (pooled.Source.isPlaying)
            {
                pooled.Source.Stop();
            }
        }
    }

    /// <summary>
    /// 停止所有待播放音效（对外接口）
    /// 清空队列 + 停止协程 + 停止音频源
    /// </summary>
    public void StopAllPendingSounds()
    {
        if (soundPlayQueue != null) soundPlayQueue.Clear();
        if (serialPlayCoroutine != null)
        {
            StopCoroutine(serialPlayCoroutine);
            serialPlayCoroutine = null;
        }
        isPlayingSerialSound = false;

        // 停止所有音频源
        foreach (var pooled in audioSourcePool)
        {
            if (pooled.Source.isPlaying)
            {
                pooled.Source.Stop();
            }
        }
    }
    #endregion

    #region 内部核心逻辑
    /// <summary>
    /// 启动串行播放协程（防护重复启动）
    /// 仅在「开启串行播放 + 未处理队列 + 协程未启动」时启动
    /// </summary>
    private void StartSerialPlayCoroutine()
    {
        if (enableSerialPlay && !isPlayingSerialSound && serialPlayCoroutine == null)
        {
            serialPlayCoroutine = StartCoroutine(ProcessSerialSoundQueue());
        }
    }

    /// <summary>
    /// 串行播放队列处理协程（核心逻辑）
    /// 按顺序执行队列中的播放请求，避免音效重叠
    /// </summary>
    /// <returns>协程迭代器</returns>
    private IEnumerator ProcessSerialSoundQueue()
    {
        isPlayingSerialSound = true;

        // 循环处理队列中的播放请求，直到队列为空
        while (soundPlayQueue.Count > 0)
        {
            // 取出并执行播放请求
            var playAction = soundPlayQueue.Dequeue();
            bool playSuccess = false;
            if (playAction != null)
            {
                playAction.Invoke(); // 执行播放操作
                // 查找刚播放的音频源，获取播放时长
                var playingSource = audioSourcePool.FirstOrDefault(p => p.Source.isPlaying);
                if (playingSource != null && playingSource.Source.clip != null)
                {
                    playSuccess = true;
                    // 等待音效播放完毕 + 额外延迟
                    yield return new WaitForSeconds(playingSource.Source.clip.length + serialPlayDelay);
                }
            }

            // 若播放失败，短暂延迟后继续处理下一个（避免死循环）
            if (!playSuccess)
            {
                yield return new WaitForSeconds(0.01f);
            }
        }

        // 队列为空，重置状态
        isPlayingSerialSound = false;
        serialPlayCoroutine = null;
    }

    /// <summary>
    /// 尝试播放音效（核心播放逻辑）
    /// 1. 检查音效数组有效性
    /// 2. 查找/复用音频源
    /// 3. 随机化音调/音量
    /// 4. 播放音效
    /// </summary>
    /// <returns>是否播放成功</returns>
    public bool TryPlayTypeSound()
    {
        // 安全检查：无有效音效则返回失败
        if (typeClips == null || typeClips.Length == 0)
        {
            Debug.LogWarning("TypeSoundController: 无可用的打字音效Clip");
            return false;
        }

        // 优化：环形索引查找空闲音频源，避免从头扫描
        PooledAudioSource freeSource = FindFreeAudioSource();
        if (freeSource == null)
        {
            // 优化：复用策略 → 优先复用「即将播放完成」的源，其次「最久未使用」的源
            freeSource = GetBestSourceToReuse();
            if (freeSource == null)
            {
                Debug.LogWarning("TypeSoundController: 音频源池耗尽，无法播放音效");
                return false;
            }
        }

        // 随机选择音效Clip（增加多样性）
        AudioClip clip = typeClips[UnityEngine.Random.Range(0, typeClips.Length)];
        if (clip == null) return false;

        // 优化：音色/音量随机化，避免机械感
        var source = freeSource.Source;
        source.clip = clip;
        // 音调随机化：1±pitchVariance
        source.pitch = UnityEngine.Random.Range(1 - pitchVariance, 1 + pitchVariance);
        // 音量计算：随机音量（min~max） × 全局音量缩放
        float randomVolume = UnityEngine.Random.Range(minVolume, maxVolume);
        source.volume = randomVolume * globalVolumeScale;

        // 更新使用时间（用于复用策略）
        freeSource.LastUsedTime = Time.time;
        source.Play(); // 播放音效

        return true;
    }

    /// <summary>
    /// 内部：判断字符是否需要播放音效（优先级：自定义逻辑 > 内置过滤）
    /// </summary>
    /// <param name="c">当前字符</param>
    /// <returns>是否需要播放音效</returns>
    private bool ShouldPlayForCharacter(char c)
    {
        // 优先级：自定义判断逻辑 > 内置过滤规则
        if (ShouldPlayForChar != null)
        {
            return ShouldPlayForChar.Invoke(c);
        }

        // 内置规则：忽略空格、标点等无声字符
        return !ignoreCharacters.Contains(c) && !char.IsWhiteSpace(c);
    }

    /// <summary>
    /// 优化：环形查找空闲音频源
    /// 从上次查找位置开始扫描，避免每次从头遍历，提升效率
    /// </summary>
    /// <returns>空闲的音频源（无则返回null）</returns>
    private PooledAudioSource FindFreeAudioSource()
    {
        int startIndex = lastPoolIndex; // 记录起始位置，避免无限循环
        do
        {
            var pooled = audioSourcePool[lastPoolIndex];
            if (!pooled.Source.isPlaying)
            {
                lastPoolIndex = (lastPoolIndex + 1) % audioSourcePool.Count; // 更新环形索引
                return pooled;
            }
            lastPoolIndex = (lastPoolIndex + 1) % audioSourcePool.Count;
        } while (lastPoolIndex != startIndex); // 遍历完所有源仍无空闲则返回null

        return null;
    }

    /// <summary>
    /// 优化：选择最优的复用源（音效池耗尽时）
    /// 策略1：优先复用「即将播放完成」的源（剩余时间 < 10% 总时长）
    /// 策略2：其次复用「最久未使用」的源
    /// </summary>
    /// <returns>最优复用的音频源（无则返回null）</returns>
    private PooledAudioSource GetBestSourceToReuse()
    {
        // 策略1：优先选择「即将播放完成」的源（剩余时间 < 总时长的10%）
        var nearlyFinished = audioSourcePool
            .Where(p => p.Source.clip != null && p.TimeRemaining < p.Source.clip.length * 0.1f)
            .OrderBy(p => p.TimeRemaining) // 按剩余时长升序，优先选快结束的
            .FirstOrDefault();

        if (nearlyFinished != null)
        {
            return nearlyFinished;
        }

        // 策略2：其次选择「最久未使用」的源
        return audioSourcePool.OrderBy(p => p.LastUsedTime).FirstOrDefault();
    }
    #endregion
}