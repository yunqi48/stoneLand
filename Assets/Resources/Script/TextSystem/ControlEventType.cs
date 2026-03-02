using System.Collections;
using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// 控制事件的状态
/// </summary>
public enum ControlEventType
{
    Pause,          // 暂停
    SpeedChange,    // 速度改变
    WaitForInput,   // 等待输入
    None            // 无事件
}
