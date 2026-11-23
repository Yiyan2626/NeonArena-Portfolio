# NeonArena · 白区断链

Unity 第三人称科幻战斗原型的**公开代码展示版**，用于求职交流与代码审阅。

本项目围绕弹药克制、室内外遭遇、高低路线、喷气背包和过热近战构建玩法。完整开发工程使用 Unity 2022.3.62f1c1（Built-in Render Pipeline）；当前展示快照对应 0.31。

> 这里提供经过筛选的玩法、动作、镜头、UI 代码与测试源码。它不是可直接打开运行的完整 Unity 工程：场景、模型、贴图、音频、预制体及 Animator 资源未随仓库发布。

## 从哪里看

| 关注方向 | 代码入口 | 可审阅内容 |
| --- | --- | --- |
| 射击与弹种 | [Weapon](Code/Runtime/Weapon.cs)、[WeaponLoadout](Code/Runtime/WeaponLoadout.cs)、[Projectile](Code/Runtime/Projectile.cs) | 三选一弹药、射击间隔、穿透、反弹和命中处理 |
| 敌人和护盾 | [EnemyController](Code/Runtime/EnemyController.cs)、[EnemyShield](Code/Runtime/EnemyShield.cs)、[EnemyRanged](Code/Runtime/EnemyRanged.cs) | 敌人状态、射击/蓄力、护盾交互 |
| 角色机动 | [PlayerController](Code/Runtime/PlayerController.cs)、[PlayerDash](Code/Runtime/PlayerDash.cs)、[PlayerFlight](Code/Runtime/PlayerFlight.cs) | 镜头相对移动、跳跃、冲刺和喷气背包 |
| 过热奖励 | [PlayerOverheat](Code/Runtime/PlayerOverheat.cs) | 击杀充能、限时形态、目标与落点检查、斩击时序及复位 |
| 动作与IK | [RetargetedMotion](Code/Runtime/RetargetedMotion.cs)、[CharacterMotion](Code/Runtime/CharacterMotion.cs) | 动作状态、脚部接地、双臂握持、原模型指骨回退 |
| 镜头与视野 | [ArenaCamera](Code/Runtime/ArenaCamera.cs)、[ArenaVision](Code/Runtime/ArenaVision.cs) | 跟随、自由观察、避障、视距/遮挡与上躯干视线 |
| 流程与界面 | [FrontierCampaign](Code/Runtime/FrontierCampaign.cs)、[FrontierHUD](Code/Runtime/FrontierHUD.cs)、[FrontEnd](Code/Runtime/FrontEnd.cs) | 区段推进、终端选择、HUD和主界面 |
| 回归测试 | [Tests](Code/Tests) | 玩法、机动、视野和动作生命周期的测试源码 |

## 玩法概览

- 初始基础枪、一级跳跃、一次冲刺；每个区段后选择弹药与机动升级。
- 激光弹远距单发并破盾；穿透弹处理墙体与纵队；反弹弹在地形上最多反弹三次。
- 二级跳跃解锁喷气背包，冲刺储备最高三次；上层捷径与地面路线提供选择。
- 八次击杀充能进入十秒过热形态，包含双头刃瞬移近战、三种斩击与结束回枪。
- 四个流程模块：城郊广场、办公楼、高架管线与重型守卫战。8–12 分钟是设计目标，不能视作已完成人工流程计时。

## 验证与实际边界

历史完整工程在 2026-10-23 的完整 PlayMode 回归为 208/208；最后的手指骨骼修复之后另外执行 17/17 项动作与过热专项。没有把补丁前全回归说成补丁后重跑，也没有把自动演示当作真人完整通关。

公开快照没有场景与美术依赖，未在这里重新编译或运行上述测试。记录与范围见 [验证摘要](Docs/verification-summary.json)，逐文件来源哈希见 [源码清单](Docs/source-manifest.json)。

已知余量：部分代码仍直接依赖场景实例，数据驱动和模块边界可继续改进；挥刀前摇和远景三招区分仍需真人试玩；未提供商业发行、全流程难度或引擎帧时间基准的验收结论。

## 制作与素材说明

这是包含 AI 协助制作代码、工具和迭代验证的学习项目。完整样机使用过第三方模型、动作、场景和音效；本仓库不把它们表述为个人原创建模，也不包含限制再分发或许可不明确的素材。

完整工程保留为私有开发备份。本仓库采用全新提交历史，不从私有工程分叉，因此不能通过公开历史取回被排除的素材。具体发布范围见 [公开范围说明](Docs/PUBLIC_SCOPE.md)。

此处未添加统一开源许可证。代码公开可读不等于已授予任意复制、再分发或商业使用许可；第三方内容的权利不因本仓库而改变。
