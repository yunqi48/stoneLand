using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 负责从 CSV 配置文件中读取对话数据，并构建 NodeID → DialogueLineData 的索引。
/// 只处理「数据解析」，不参与任何播放逻辑，方便与 DialogueManager 解耦。
/// </summary>
public class DialogueConfigLoader : MonoBehaviour
{
    [Header("CSV 配置")]
    [Tooltip("对话配置 CSV（例如 DialogueConfig.csv），导入方式为 TextAsset。")]
    [SerializeField] private TextAsset dialogueConfigCsv;

    [Header("说话者配置")]
    [Tooltip("场景或资源中所有可能出现在 CSV 里的 SpeakerProfile 列表，用于按 SpeakerName 匹配。")]
    [SerializeField] private List<SpeakerProfile> speakerProfiles = new List<SpeakerProfile>();

    /// <summary>按 NodeID 存储的全部对话节点。</summary>
    private readonly Dictionary<string, DialogueLineData> _nodes = new Dictionary<string, DialogueLineData>();

    /// <summary>用于快速从 SpeakerName 找到 SpeakerProfile 的查找表。</summary>
    private readonly Dictionary<string, SpeakerProfile> _speakerLookup =
        new Dictionary<string, SpeakerProfile>();

    /// <summary>CSV 中出现的第一个有效 NodeID，通常可作为默认起始节点。</summary>
    public string FirstNodeId { get; private set; }

    /// <summary>当前是否已经成功加载并拥有至少一个节点。</summary>
    public bool HasAnyNode => _nodes.Count > 0;

    /// <summary>对外只读访问全部节点（如需枚举所有 NodeID）。</summary>
    public IReadOnlyDictionary<string, DialogueLineData> Nodes => _nodes;

    private void Awake()
    {
        BuildSpeakerLookup();
        LoadFromCsv();
    }

    /// <summary>
    /// 通过 NodeID 取得对应节点数据。
    /// </summary>
    public bool TryGetNode(string nodeId, out DialogueLineData line)
    {
        return _nodes.TryGetValue(nodeId, out line);
    }

    #region 内部实现：加载 CSV 与构建索引

    /// <summary>
    /// 从 CSV 文本中读取所有对话节点，构建 _nodes 字典。
    /// 预期表头：NodeID,SpeakerName,Text,TypingSpeed,ShowImage,AutoNext,JumpMode,NextNodeID
    /// </summary>
    private void LoadFromCsv()
    {
        _nodes.Clear();
        FirstNodeId = null;

        if (dialogueConfigCsv == null)
        {
            Debug.LogWarning("[DialogueConfigLoader] 未指定对话配置 CSV（DialogueConfig.csv），请在 Inspector 中赋值。", this);
            return;
        }

        string[] rows = dialogueConfigCsv.text.Split('\n');
        if (rows.Length <= 1) return; // 没有有效数据

        // 解析表头，建立「列名 → 索引」映射（不区分大小写）
        var header = rows[0].Trim().Split(',');
        var colIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < header.Length; i++)
        {
            var key = header[i].Trim();
            if (!string.IsNullOrEmpty(key) && !colIndex.ContainsKey(key))
                colIndex.Add(key, i);
        }

        int GetCol(string name)
        {
            return colIndex.TryGetValue(name, out var idx) ? idx : -1;
        }

        int idxNodeId     = GetCol("NodeID");
        int idxSpeaker    = GetCol("SpeakerName");
        int idxText       = GetCol("Text");
        int idxTyping     = GetCol("TypingSpeed");
        int idxAutoNext   = GetCol("AutoNext");
        int idxJumpMode   = GetCol("JumpMode");
        int idxNextNodeId = GetCol("NextNodeID");

        // 从第二行开始解析具体数据行
        for (int i = 1; i < rows.Length; i++)
        {
            string row = rows[i].Trim();
            if (string.IsNullOrEmpty(row))
                continue;

            var cols = row.Split(',');
            if (cols.Length == 0) continue;

            string Get(int idx)
            {
                if (idx < 0 || idx >= cols.Length) return string.Empty;
                return cols[idx].Trim();
            }

            string nodeId      = Get(idxNodeId);
            string speakerName = Get(idxSpeaker);
            string text        = Get(idxText);
            string typingStr   = Get(idxTyping);
            string autoNextStr = Get(idxAutoNext);
            string jumpMode    = Get(idxJumpMode);
            string nextNodeId  = Get(idxNextNodeId);

            if (string.IsNullOrEmpty(nodeId))
                continue;

            SpeakerProfile speaker = ResolveSpeaker(speakerName);

            float typingSpeed = 0f;
            if (!string.IsNullOrEmpty(typingStr))
                float.TryParse(typingStr, out typingSpeed);

            bool autoNext = false;
            if (!string.IsNullOrEmpty(autoNextStr))
            {
                autoNext = autoNextStr.Equals("TRUE", StringComparison.OrdinalIgnoreCase) ||
                           autoNextStr.Equals("1");
            }

            var line = new DialogueLineData
            {
                nodeId      = nodeId,
                speaker     = speaker,
                text        = text,
                typingSpeed = typingSpeed,
                autoNext    = autoNext,
                jumpMode    = jumpMode,
                nextNodeId  = nextNodeId
            };

            _nodes[nodeId] = line;

            // 记录出现的第一个有效节点，作为默认起点
            if (FirstNodeId == null)
            {
                FirstNodeId = nodeId;
            }
        }
    }

    /// <summary>
    /// 构建「名字 → SpeakerProfile」映射，支持通过 speakerId 或 displayName 查找。
    /// </summary>
    private void BuildSpeakerLookup()
    {
        _speakerLookup.Clear();
        foreach (var sp in speakerProfiles)
        {
            if (sp == null) continue;

            if (!string.IsNullOrEmpty(sp.speakerId))
            {
                _speakerLookup[sp.speakerId] = sp;
            }

            if (!string.IsNullOrEmpty(sp.displayName))
            {
                _speakerLookup[sp.displayName] = sp;
            }
        }
    }

    /// <summary>
    /// 根据 CSV 中的 SpeakerName 查找对应的 SpeakerProfile。
    /// 优先匹配 speakerId，其次匹配 displayName。
    /// </summary>
    private SpeakerProfile ResolveSpeaker(string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;

        if (_speakerLookup.TryGetValue(name, out var sp))
            return sp;

        Debug.LogWarning($"[DialogueConfigLoader] 在 speakerProfiles 列表中未找到名为 \"{name}\" 的 SpeakerProfile。", this);
        return null;
    }

    #endregion
}

