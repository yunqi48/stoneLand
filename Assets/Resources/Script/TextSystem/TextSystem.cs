using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 文本渲染器：支持场景+预制体定位DialogueWindow中的DialogueText
/// 适配：UpdateVisibleRange支持ParsedText类型，解决CS1503类型转换错误
/// </summary>
public class TextRenderer : MonoBehaviour
{
    // 目标文本组件（自动查找）
    private TextMeshProUGUI _dialogueTextComponent;

    // 基础定位配置（兼容旧逻辑）
    [Header("基础定位配置")]
    [Tooltip("DialogueText在DialogueWindow中的路径")]
    [SerializeField] private string _dialogueTextPath = "DialogueText";

    // 场景+预制体定位配置
    [Header("场景+预制体定位配置")]
    [Tooltip("目标场景名称（留空=当前激活场景）")]
    [SerializeField] private string _targetSceneName = "";
    [Tooltip("DialogueWindow预制体根物体名称")]
    [SerializeField] private string _dialogueWindowName = "DialogueWindow";

    // 外部访问文本组件的属性（解决CS1061 TmpText错误）
    public TextMeshProUGUI TmpText
    {
        get => _dialogueTextComponent;
        set => _dialogueTextComponent = value;
    }

    // TMP富文本标签闭合映射
    private readonly Dictionary<string, string> _closingTags = new Dictionary<string, string>()
    {
        { "<color", "</color>" },
        { "<size", "</size>" },
        { "<b>", "</b>" },
        { "<i>", "</i>" },
        { "<u>", "</u>" },
        { "<mark", "</mark>" },
        { "<style", "</style>" }
    };

    #region 外部调用方法（兼容所有CS1061/CS1503错误）
    // 基础设置文本（保留原有逻辑）
    public void SetDialogueText(string content)
    {
        if (_dialogueTextComponent == null)
        {
            Debug.LogWarning("DialogueText组件未找到，无法设置文本", this);
            return;
        }
        _dialogueTextComponent.text = AutoCloseUnclosedTags(content);
    }

    // 兼容SetText调用（解决CS1061 SetText错误）
    public void SetText(string content)
    {
        SetDialogueText(content);
    }

    // 获取当前文本
    public string GetDialogueText()
    {
        return _dialogueTextComponent?.text ?? string.Empty;
    }

    // 清空文本（解决CS1061 Clear错误）
    public void Clear()
    {
        if (_dialogueTextComponent == null)
        {
            Debug.LogWarning("DialogueText组件未找到，无法清空文本", this);
            return;
        }
        _dialogueTextComponent.text = string.Empty;
    }

    // 无参版本UpdateVisibleRange（向下兼容）
    public void UpdateVisibleRange()
    {
        if (_dialogueTextComponent == null)
        {
            Debug.LogWarning("DialogueText组件未找到，无法更新可见范围", this);
            return;
        }
        _dialogueTextComponent.gameObject.SetActive(true);
    }

    // 适配ParsedText类型的UpdateVisibleRange（核心修复CS1503）
    // 假设ParsedText有一个Text/Content属性存储字符串内容，可根据实际情况修改
    public void UpdateVisibleRange(int currentCharIndex, ParsedText parsedText)
    {
        if (_dialogueTextComponent == null)
        {
            Debug.LogWarning("DialogueText组件未找到，无法更新可见范围", this);
            return;
        }

        // 从ParsedText中提取字符串内容（关键：替换为你实际的字符串属性名）
        // 常见属性名：Text / Content / Value / RawText 等，根据你的ParsedText类调整
        string textContent = parsedText?.Text ?? string.Empty;

        // 核心逻辑：逐字显示 + 自动闭合标签
        if (!string.IsNullOrEmpty(textContent) && currentCharIndex >= 0)
        {
            int displayLength = Mathf.Min(currentCharIndex + 1, textContent.Length);
            string visibleText = textContent.Substring(0, displayLength);
            string processedText = AutoCloseUnclosedTags(visibleText);
            _dialogueTextComponent.text = processedText;
        }

        _dialogueTextComponent.gameObject.SetActive(true);
    }

    // 兼容string类型的重载（防止其他调用处报错）
    public void UpdateVisibleRange(int currentCharIndex, string parsedText)
    {
        if (_dialogueTextComponent == null)
        {
            Debug.LogWarning("DialogueText组件未找到，无法更新可见范围", this);
            return;
        }

        if (!string.IsNullOrEmpty(parsedText) && currentCharIndex >= 0)
        {
            int displayLength = Mathf.Min(currentCharIndex + 1, parsedText.Length);
            string visibleText = parsedText.Substring(0, displayLength);
            string processedText = AutoCloseUnclosedTags(visibleText);
            _dialogueTextComponent.text = processedText;
        }

        _dialogueTextComponent.gameObject.SetActive(true);
    }

    // ShowInstant方法（解决CS1061 ShowInstant错误）
    public void ShowInstant()
    {
        ShowInstant(string.Empty);
    }

    public void ShowInstant(string content)
    {
        if (_dialogueTextComponent == null)
        {
            Debug.LogWarning("DialogueText组件未找到，无法即时显示文本", this);
            return;
        }
        _dialogueTextComponent.gameObject.SetActive(true);
        if (!string.IsNullOrEmpty(content))
        {
            SetDialogueText(content);
        }
    }
    #endregion

    #region 内部工具方法
    // 自动闭合未关闭的TMP标签
    private string AutoCloseUnclosedTags(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        string processed = input;
        foreach (var tagPair in _closingTags)
        {
            string startTagKey = tagPair.Key;
            string closeTag = tagPair.Value;

            int startCount = CountOccurrences(processed, startTagKey);
            int closeCount = CountOccurrences(processed, closeTag);

            if (startCount > closeCount)
            {
                int missing = startCount - closeCount;
                for (int i = 0; i < missing; i++)
                {
                    processed += closeTag;
                }
            }
        }
        return processed;
    }

    // 统计子串出现次数
    private int CountOccurrences(string source, string substring)
    {
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(substring))
            return 0;

        int count = 0;
        int index = 0;
        while ((index = source.IndexOf(substring, index)) != -1)
        {
            count++;
            index += substring.Length;
        }
        return count;
    }
    #endregion

    #region 组件查找逻辑
    private void Awake()
    {
        GameObject dialogueWindow = FindDialogueWindowInScene();
        if (dialogueWindow != null)
        {
            FindDialogueTextInWindow(dialogueWindow);
        }
        else
        {
            FindDialogueTextComponent();
        }
    }

    private GameObject FindDialogueWindowInScene()
    {
        Scene targetScene = string.IsNullOrEmpty(_targetSceneName)
            ? SceneManager.GetActiveScene()
            : SceneManager.GetSceneByName(_targetSceneName);

        if (!targetScene.isLoaded)
        {
            Debug.LogWarning($"目标场景【{_targetSceneName}】未加载，将使用原有查找逻辑", this);
            return null;
        }

        GameObject[] sceneRoots = targetScene.GetRootGameObjects();
        foreach (GameObject root in sceneRoots)
        {
            if (root.name == _dialogueWindowName)
            {
                return root;
            }
            Transform windowTrans = FindChildRecursive(root.transform, _dialogueWindowName);
            if (windowTrans != null)
            {
                return windowTrans.gameObject;
            }
        }

        Debug.LogWarning($"场景【{targetScene.name}】中未找到【{_dialogueWindowName}】物体，将使用原有查找逻辑", this);
        return null;
    }

    private void FindDialogueTextInWindow(GameObject dialogueWindow)
    {
        Transform textTrans = FindChildRecursive(dialogueWindow.transform, _dialogueTextPath);
        if (textTrans != null)
        {
            _dialogueTextComponent = textTrans.GetComponent<TextMeshProUGUI>();
        }

        if (_dialogueTextComponent == null)
        {
            Debug.LogError($"在【{_dialogueWindowName}】中未找到【{_dialogueTextPath}】上的TextMeshProUGUI组件！", this);
        }
    }

    private void FindDialogueTextComponent()
    {
        Transform targetTrans = transform.Find(_dialogueTextPath);
        if (targetTrans == null)
        {
            targetTrans = FindChildRecursive(transform, _dialogueTextPath);
        }

        if (targetTrans != null)
        {
            _dialogueTextComponent = targetTrans.GetComponent<TextMeshProUGUI>();
        }

        if (_dialogueTextComponent == null)
        {
            Debug.LogError($"TextRenderer未找到【{_dialogueTextPath}】上的TextMeshProUGUI组件！", this);
        }
    }

    private Transform FindChildRecursive(Transform parent, string childName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == childName)
            {
                return child;
            }
            Transform grandChild = FindChildRecursive(child, childName);
            if (grandChild != null)
            {
                return grandChild;
            }
        }
        return null;
    }
    #endregion

    // 编辑器校验
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(_dialogueTextPath))
        {
            _dialogueTextPath = "DialogueText";
            Debug.LogWarning("DialogueText路径不能为空，已重置为默认值", this);
        }

        if (string.IsNullOrEmpty(_dialogueWindowName))
        {
            _dialogueWindowName = "DialogueWindow";
            Debug.LogWarning("DialogueWindow名称不能为空，已重置为默认值", this);
        }
    }

    // 请根据你的实际代码，补充ParsedText类的定义（如果未定义）
    // 示例ParsedText类（你需要替换为项目中实际的定义）
    public class ParsedText
    {
        public string Text { get; set; } // 核心字符串属性，替换为你实际的属性名
        // 其他可能的属性：如标签、样式、解析后的格式等
    }
}