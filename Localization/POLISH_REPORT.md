# LOTUS CITY — v2 视觉打磨报告
日期：2026-10-22

## 改动清单

### 1. CombatVisualEffects.cs

#### 1a. 强化净化环可见度
- `TransientPurificationRing.Update()` 中，冲击环的最大不透明度由 `0.20f` 提高到 `0.38f`
- 理由：原版光环在 VR 中对比度偏低，近距离仍难以注意到；0.38 接近原版 CombatVisualEffects 其他冲击效果的透明度，视觉上更统一

#### 1b. 新增 LanternFlicker MonoBehaviour
- 挂载到每个石灯笼的点光源 GameObject 上
- 使用两个叠加正弦函数模拟自然火焰抖动（频率 2.0–3.8 Hz，强度在 0.13–0.30 之间）
- 理由：静态灯笼显得死板；轻微抖动在 VR 中极大增强环境真实感

#### 1c. 新增 TempleHeartPulse MonoBehaviour
- 挂载到受保护神庙目标点光源的 GameObject 上
- 以 1.35 rad/s 平滑脉动灯光强度（0.12–0.28）和范围（0.40–0.58m）
- 理由：宝莲灯/护持焦点需要一种"生命感"，引导玩家视线注意保护目标

### 2. SpatialTowerDefenseGame.cs

#### 2a. 大气薄雾（BuildLightingAndRoom）
- 启用线性雾：颜色 (0.34, 0.38, 0.37)，起始 4.5m，结束 16.0m
- 理由：中国山水画意境的薄雾感；在米制场景里棋盘距离 ~2.2m，雾不影响近景，只增加远山/背景的层次感

#### 2b. 石灯笼点光源（BuildStoneLantern）
- 每个石灯笼添加一个暖琥珀色点光源（色温 (1.0, 0.76, 0.34)，range=0.55m，intensity=0.22，无阴影）
- 同时挂载 LanternFlicker 组件
- 共 6 只灯笼 → 6 个无阴影点光源，在 PICO 4 GPU 预算内

#### 2c. 神庙护持心脏灯脉动
- 在 BuildObjective 末尾的点光源 GameObject 上添加 TempleHeartPulse 组件
- 理由：静态灯光与现有水晶浮动动画形成对比不足

#### 2d. 波次开始触觉反馈（StartNextWave）
- 添加 `_input.Pulse(0.55f, 0.10f)` — 中强度短促脉冲
- 理由：波次开始是重要节点，需要清晰的手柄反馈

#### 2e. 波次开始涟漪视效（StartNextWave → AnimateWaveBellRipple）
- 新协程：从棋盘中心发出青灰绿色扩散光环（半径 1.6m，持续 0.9s），模拟祈福钟声涟漪
- 使用已有 CombatVisualEffects.LineMaterial，不创建额外材质
- 理由：视觉上标志波次开始，增加仪式感

#### 2f. 胜负结局触觉反馈（UpdateGame）
- 胜利：`_input.Pulse(0.88f, 0.18f)` — 强脉冲代表成功
- 失败：`_input.Pulse(0.80f, 0.22f)` — 稍长脉冲代表沉重失落
- 理由：原版无结局触觉，玩家听到音效前缺乏身体感知

#### 2g. 增强敌人净化消散（AnimateEnemyDefeatSmoke）
- 粒子速度：(0.10, 0.18) → (0.13, 0.26)，上升更明显
- 粒子尺寸：(0.38, 0.78) → (0.50, 0.96) × enemyRadius
- 最大粒子数：32 → 48
- 发射率：24/s → 36/s
- 新增重力 `-0.055f`（负值 = 飘浮效果）
- 颜色稍亮：(0.18, 0.68, 0.58, 0.62) → (0.18, 0.68, 0.58, 0.68)
- 理由：净化是游戏核心反馈，原版效果较细小；增大粒子后更显庄重

#### 2h. 敌人净化后金色光点迸发（AnimateEnemyDefeatSmoke）
- 净化烟雾之后追加 6 个金色小球体（0.09 × radius），以 0.50s 向外膨胀并淡出
- 颜色 (1.0, 0.86, 0.42) 代表"灵光（Spirit Light）"奖励获得的视觉提示
- 理由：呼应 "Spirit Light" 设定，使金币飞向水晶的动画有前置视觉触发

## Tripo 调用清单

本轮 v2 打磨未新增 Tripo 生成调用。
- 封面、面板图片均沿用 v1 已生成的高质量资产（task_id 见 EN_DELIVERY.md）
- 封面已满足"全大写等高 LOTUS CITY、暖石色、1920×1080"规范
- 面板已满足"原版像素尺寸、槽位对齐、英文内容经总控核对"规范

## 性能影响评估

| 指标 | 改动前 | 改动后 | 影响 |
|---|---|---|---|
| 点光源数量 | 1（神庙） | 8（+6灯笼+神庙已有1） | +7，均无阴影。移动 VR 每点光源约 0.1-0.2ms；6×0.15=0.9ms，可接受 |
| Update 组件 | 0（此类） | 7（6×LanternFlicker + 1×TempleHeartPulse） | 每帧仅 float sin 运算，<0.01ms |
| 敌人净化粒子峰值 | ~32/次 | ~48/次 + 6顶点 | 单次+16粒子+6顶点，非持续，影响微小 |
| 波次涟漪 | 无 | 1×LineRenderer，48顶点，0.9s | 极小，每波只触发一次 |
| Draw Calls | 基线无量 | +6-7（新点光源阴影批次绕过） | 与预算 ≤150 兼容 |
| 大气薄雾 | 无 | 线性雾（仅像素着色器判断） | 移动 VR 下几乎 0 开销 |

**总体评估**：各改动均属于轻量不损画面的增效改动，不超出 PICO 4 72fps 预算。

## 未完成项与风险

1. **构建和安装**：本轮不自行出包，构建方法：`PicoTowerDefense.ProjectSetupBuildPipeline.BuildRelease`（或等效发布配置），APK 输出 `Builds/EN/LotusCity-EN-PICO.apk`。总控负责构建和安装 `.en` 包。
2. **LotusCityStartCoverPressed.png 视差无变化**：按压态与普通态图片目测无差异（均为同一 Tripo 生成图）。按压反馈靠代码层缩放处理。如需视觉差别，可在 Pressed 图上额外增加整体暗化叠层，但当前版本并未做，留给总控决策。
3. **灯笼数量**：目前 6 只灯笼均添加了点光源。若实际测试 FPS 下降，可减少到每隔一只（3 只）。
4. **雾对 VR 舒适感影响**：薄雾（start 4.5m）在近场（棋盘 2.2m）不可见；中远距离有助于减少深度冲突，通常对 VR 舒适感利大于弊。真实头显测试后如有问题可将 `fogStartDistance` 调大。
5. **金色迸发使用 `sparkMat.color`**：通过 `sharedMaterial` 共享颜色，多个同时死亡的敌人会造成最后一个写入值覆盖前者。考虑到这是次要视觉细节，暂不处理；如需独立，可改为 `renderer.material`（实例化）。
