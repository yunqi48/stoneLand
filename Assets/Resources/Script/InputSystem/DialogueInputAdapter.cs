using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 通过 Unity Input System 将输入事件路由到 DialogueManager。
/// - 可绑定到 PlayerInput（Invoke Unity Events 模式）的 Action 上。
/// - 同一个 Action 可以同时绑定键盘空格、鼠标左键等多个输入。
/// </summary>
public class DialogueInputAdapter : MonoBehaviour
{
    [SerializeField] private DialogueManager dialogueManager;

    /// <summary>
    /// 供 Input System 调用的回调方法。
    /// 在 InputActions 里把某个 Action（比如 NextDialogue）设置为调用本方法。
    /// </summary>
    public void OnNext(InputAction.CallbackContext context)
    {
        // 只在 performed 阶段响应（按下 / 点击完成）
        if (!context.performed) return;
        if (dialogueManager == null) return;

        dialogueManager.GoNextDefault();
    }
}

