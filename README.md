Unity Dialogue UI System
一个可扩展、可配置的对话框 UI 系统，专注于在 Unity 中实现灵活的对话框布局、角色样式与文本打字机效果。设计初衷是让美术、剧情人员和开发者可以高效协作，快速迭代对话场景，而不需要频繁修改代码。

注：当前版本聚焦于 UI 管线的架构与对接点，Dialogue 相关的具体实现（如 DialogueWindow、SpeakerProfile、DialogueWindowLayout、DialogueStyle 等）可在后续迭代中逐步完善并集成。

目标与特性
可配置的对话框布局

支持多种布局模板（左/右/中/全宽等），可在运行时切换
头像、名字、背景等 UI 元素的可定制化
角色驱动的样式

SpeakerProfile 用于存放角色的显示名称、头像、名称颜色、是否显示名字等信息
全局默认样式与角色覆盖样式的优先级机制
打字机文本效果

通过与 TextSystem 的对接实现逐字显示的文本效果
支持自定义打字设置（速度、节奏等）
动画与过渡

渐隐/渐显等过渡动画，提升对话体验
组件化、可测试、易扩展

清晰的职责划分：DialogueUIManager、DialogueWindow、SpeakerProfile、DialogueWindowLayout、DialogueStyle 等
易于在未来接入剧情系统、事件驱动等逻辑
编辑器友好

可在 Inspector 配置 SpeakerProfile、Layout、Style 等数据
运行时可动态注册/修改角色配置
架构概览
DialogueUIManager
全局对话 UI 的入口与调度者
管理对话窗口实例池、全局默认布局/样式
提供统一 API：ShowDialogue、SwitchSpeaker、HideCurrentDialogue、GetDialogueWindow 等
DialogueWindow
单个对话框实例的 UI 控制
应用布局、应用样式、设置说话角色、设置对话文本、实现显示/隐藏动画
SpeakerProfile
角色配置（speakerId、displayName、avatar、nameColor、showName、preferredLayout、styleOverride 等）
DialogueWindowLayout
对话框布局模板（位置、尺寸、头像镜像等）
DialogueStyle
样式数据包（字体、字号、颜色、对齐、背景等）
TextPlaySettings
文本打字机设置（速度、节奏等）
DialogueController
贴合剧情/业务逻辑的控制器示例（可选在路线上后续接入剧情系统时使用）
快速开始（示意）
将 Dialog/UI 相关脚本添加到你的场景中，确保你有一个 DialogueUIManager 实例，作为全局入口。
配置 SpeakerProfile、DialogueWindowLayout、DialogueStyle 等数据对象（ScriptableObject 或可序列化数据结构）。
通过 DialogueUIManager 提供的 API 展示对话：
ShowDialogue("hero_01", "这是一个测试对话。")
HideCurrentDialogue()
如需扩展，请添加新的布局模板、样式覆盖，或新增角色并通过 RegisterSpeakerProfile 动态注册。
架构细化与接口设计（建议）
DialogueUIManager

GetDialogueWindow(string windowId = "default"): DialogueWindow
ShowDialogue(string speakerId, string dialogueText, string windowId = "default", TextPlaySettings settings = null)
SwitchSpeaker(string speakerId)
HideCurrentDialogue(bool instant = false)
GetSpeakerProfile(string speakerId): SpeakerProfile
RegisterSpeakerProfile(SpeakerProfile profile)
DialogueWindow

ApplyLayout(DialogueWindowLayout layout)
ApplyStyle(DialogueStyle style)
SetSpeaker(SpeakerProfile speaker)
