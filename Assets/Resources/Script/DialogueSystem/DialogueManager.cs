using UnityEngine;

/// <summary>
/// 对话流程控制器（只负责对话播放，不关心 CSV 解析细节）。
/// 通过 NodeID 驱动对话节点跳转，实际节点数据由 DialogueConfigLoader 提供。
/// </summary>
public class DialogueManager : MonoBehaviour
{
    [Header("配置来源")]
    [Tooltip("对话配置加载器（负责从 CSV 解析出所有节点），在场景中挂载 DialogueConfigLoader 后拖入此处。")]
    [SerializeField] private DialogueConfigLoader configLoader;

    [Header("播放设置")]
    [Tooltip("是否在 Start 时自动开始播放对话。")]
    [SerializeField] private bool playOnStart = true;

    [Tooltip("初始节点 ID，对应 CSV 中的 NodeID。如果留空，则使用配置中的第一个节点。")]
    [SerializeField] private string startNodeId;

    [Tooltip("保留字段：当使用 Input System 的 Action 调用 GoNextDefault 时，可在 UI 或其他脚本中作为标记使用。")]
    [SerializeField] private string nextActionName = "NextDialogue";

    /// <summary>当前所在对话节点的 NodeID。</summary>
    private string _currentNodeId;

    /// <summary>当前是否处于对话播放状态。</summary>
    private bool _playing;

    private void Awake()
    {
        if (configLoader == null)
        {
            Debug.LogWarning("[DialogueManager] 未设置 DialogueConfigLoader，无法播放对话。", this);
        }
    }

    private void Start()
    {
        if (!playOnStart || configLoader == null || !configLoader.HasAnyNode)
        {
            return;
        }

        _playing = true;

        // 未指定起始节点时，使用配置中记录的第一个节点
        if (string.IsNullOrEmpty(startNodeId))
        {
            startNodeId = configLoader.FirstNodeId;
        }

        GoToNode(startNodeId);
    }

    /// <summary>
    /// 跳转到指定 NodeID 的对话节点，并请求 UI 显示该节点文本。
    /// </summary>
    /// <param name="nodeId">目标节点 ID。</param>
    public void GoToNode(string nodeId)
    {
        if (configLoader == null)
        {
            Debug.LogWarning("[DialogueManager] configLoader 未设置，无法跳转对话节点。", this);
            _playing = false;
            return;
        }

        if (string.IsNullOrEmpty(nodeId))
        {
            _playing = false;
            return;
        }

        if (!configLoader.TryGetNode(nodeId, out var line))
        {
            Debug.LogWarning($"[DialogueManager] 配置中未找到节点 ID = {nodeId}", this);
            _playing = false;
            return;
        }

        _currentNodeId = nodeId;

        // TODO：后续可把 line.typingSpeed 传递给 UI，控制打字机速度
        DialogueUIManager.Instance.ShowLine(line.speaker, line.text);
    }

    /// <summary>
    /// 使用当前节点的 NextNodeID 作为默认跳转规则，切换到下一句对话。
    /// 一般在「下一句」按键或点击对话框时调用。
    /// </summary>
    public void GoNextDefault()
    {
        if (string.IsNullOrEmpty(_currentNodeId))
        {
            _playing = false;
            return;
        }

        if (configLoader == null)
        {
            _playing = false;
            return;
        }

        if (!configLoader.TryGetNode(_currentNodeId, out var current))
        {
            _playing = false;
            return;
        }

        if (string.IsNullOrEmpty(current.nextNodeId))
        {
            _playing = false;
            return;
        }

        GoToNode(current.nextNodeId);
    }
}