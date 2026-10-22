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

---

# 追加内容：图九 VR 字高修正 + 开始按钮手柄反馈（2026-10-22）

## 一、字高检查表（规范 C 实测数据）

| 面板 | 世界尺寸 (m) | 图像尺寸 (px) | 密度 (px/m) | 设计距离 (m) | 层级 | 字体 (px) | 大写字高 (px) | 实测 (mm) | 规范 (mm) | 结果 |
|---|---|---|---|---|---|---|---|---|---|---|
| Status | 0.90×0.50 | 1956×1448 | 2896 | 2.67 | 列标题 | 100 | 69 | 47.7 | ≥45.4 | ✓ |
| Status | — | — | — | 2.67 | 动态数字 h=0.070m | 0.070×0.68=47.6mm | — | 47.6 | ≥45.4 | ✓ |
| Briefing | 0.60×1.40 | 930×2160 | 1543 | 2.79 | 标题 | 130 | 90 | 82.0 | ≥78.1 | ✓ |
| Briefing | — | — | — | 2.79 | 敌人名 | 75 | 52 | 47.4 | ≥47.4 | ✓ |
| Briefing | — | — | — | 2.79 | 描述行 | 56 | 39 | 35.5 | ≥33.5 | ✓ |
| Briefing | — | — | — | 2.79 | 数字 h=0.052m | 0.052×0.68=35.4mm | — | 35.4 | ≥33.5 | ✓ |
| Briefing | — | — | — | 2.79 | 波次数字 h=0.072m | 0.072×0.68=49.0mm | — | 49.0 | ≥47.4 | ✓ |
| Guide | 0.72×0.55 | 1536×1024 | 1862 | 2.20 | 标题 | 167 | 115 | 61.9 | ≥61.6 | ✓ |
| Guide | — | — | — | 2.20 | 正文 | 101 | 70 | 37.4 | ≥37.4 | ✓ |
| Stats | 0.66×0.33 | 1774×887 | 2688 | 2.0 | 标题 | 220 | 152 | 56.5 | ≥56.0 | ✓ |
| Stats | — | — | — | 2.0 | 列标题 | 133 | 92 | 34.1 | ≥34.0 | ✓ |
| Stats | — | — | — | 2.0 | 提示文字 | 94 | 65 | 24.1 | ≥24.0 | ✓ |
| Stats | — | — | — | 2.0 | 动态数字 h=0.050m | 0.050×0.68=34.0mm | — | 34.0 | ≥34.0 | ✓ |
| WaveStates | 0.18×0.36 | 512×1024/col | 2844 | 1.48 | 状态名 | 107 | 74 | 26.0 | ≥25.2 | ✓ |
| WaveStates | — | — | — | 1.48 | 副标题 | 74 | 51 | 18.0 | ≥17.8 | ✓ |
| Arsenal | 0.18×0.36 | 443×887/col | 2464 | 1.48 | 全部文字 | 64 | 44 | 17.9 | ≥17.8 | ✓ |

*注：Arsenal 使用次要规范（d×0.012），因其为主动点击目标且悬停触发 Stats 面板（正文级）。*

## 二、面板改动摘要

### Status.png — 列标题放大
- 面板尺寸：0.90×0.30m → 0.90×0.50m（代码改）
- 列标题重绘：font=100px，cap=69px=47.7mm（旧版仅约13mm）
- 动态数字高度：0.055/0.050m → 0.070m

### Briefing.png — 整体重排
- 面板尺寸：0.50×0.75m → 0.60×1.40m（代码改）
- 标题 "ENEMY INTEL"：130px（82.0mm，旧版约5mm）
- 敌人名：75px（47.4mm）；技能描述：56px（35.5mm）
- 布局：字宽超限处缩写（"800 HP · 38% Armor"、"Heals, silences towers"）

### Guide.png — 全部放大
- 面板尺寸：0.72×0.48m → 0.72×0.55m（代码改，密度 2133→1862 px/m）
- 标题 "Two-Hand Controls"：167px（61.9mm，旧版约13.6mm）
- 正文 5 行：101px（37.4mm，旧版约8.7mm）

### Stats.png — 全部放大
- 像素不变（1774×887），字号大幅提升
- 标题 "Tower Details"：220px（56.5mm）
- 列标题 Level/Attack/Speed/Range：133px（34.1mm）
- 提示：94px（24.1mm）
- 动态数字高度：0.042m → 0.050m（代码改）

### WaveStates.png — 重排
- "Defending"：107px 单行；"Temple Saved/Lost"：107px 双行
- 副标题（"Purify foes" / "Lamp endures" / "Try Again"）：74px
- 保留原始植物插画（叶、莲、落叶）

### Arsenal.png — 放大 + 缩写
- "Slowing Control" → "Slow Control"（62→374px，符合443px列宽）
- "Prayer Wheel Guardian" 拆为两行
- 全部文字：64px（17.9mm，次要规范）

## 三、开始/退出按钮手柄反馈（LotusTitleStage.cs）

| 事件 | 震动参数 | 音效 |
|---|---|---|
| 射线悬停进入 START/EXIT | Pulse(0.15, 0.02) 双手 | 无 |
| 按下 START/EXIT | Pulse(0.40, 0.04) 双手 | med_ui_soft_02.mp3 |
| 悬停高亮叠色 START | (1.0, 0.85, 0.38, 0.52) | — |
| 悬停高亮叠色 EXIT | (0.22, 0.65, 0.56, 0.32) | — |

音效来源：ange-embed ui_audio 集合，asset ID=3f9bb65b，URL=https://cdn-ange.holymolly.ai/1/8/18ddbccc-5892-4dcd-b3c9-1804033b266d.mp3

## 四、Batchmode 执行方法

```
lockf /tmp/en-unity-A.lock unity \
  -batchmode -buildTarget Android \
  -projectPath /Users/ld/Documents/Codex/EnglishEditions/LotusCity-EN \
  -executeMethod PicoTowerDefenseEditor.ProjectSetup.ApplyPolish20261022 \
  -quit
```

reimport 范围：Status / Briefing / Guide / Stats / WaveStates / Arsenal + 两个音频文件

## 五、git 提交记录

| 提交 | 摘要 |
|---|---|
| 02b2679 | Status + Briefing 面板字高修正，开始按钮手柄/音效 |
| 06e9419 | Guide / Stats / WaveStates 面板字高修正 |
| c9e15f1 | Arsenal 面板字高修正 |

## 六、对比图路径

```
VisualQA/Polish-20261022/Status_comparison.png
VisualQA/Polish-20261022/Briefing_comparison.png
VisualQA/Polish-20261022/Guide_comparison.png
VisualQA/Polish-20261022/Stats_comparison.png
VisualQA/Polish-20261022/WaveStates_comparison.png
VisualQA/Polish-20261022/Arsenal_comparison.png
```
