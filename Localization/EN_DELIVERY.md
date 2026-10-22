# Lotus City — EN 版交付报告
日期：2026-10-22

## 工程来源

- 克隆自：`/Users/ld/Documents/Codex/2026-08-03/http-localhost-8080-codex-text-link/outputs/SpatialTowerDefensePICO`
- 克隆方式：`cp -cR`（APFS 引用克隆，磁盘只占增量）
- 落地路径：`/Users/ld/Documents/Codex/EnglishEditions/LotusCity-EN/`
- 排除目录：Library/ Temp/ Logs/ obj/ Builds/ build/ .git
- 原版：未做任何修改，完全保留

## 改动清单

### 1. GeneratedV5 面板图片（全部替换）

| 文件 | Tripo task_id | 模型 | 验证结果 |
|---|---|---|---|
| Arsenal.png | 8bf9faa7-1f99-4fd6-93fb-e075db4511bd | banana2 | ✅ 四塔名/成本/职能全英文，无中文 |
| Briefing.png | d56a9db2-2b52-43fd-8ae8-5a7611d2cbb8 | banana2 | ✅ "Enemy Intel" 标题，6行敌人名+描述 |
| Guide.png | 9ca5aef9-dc92-4404-98ec-452165651f6e | banana2 | ✅ "Controls Guide" 双手控制器图示全英文 |
| Stats.png | 92171dde-22e8-4295-9db8-29bc4759ff25 | banana2 | ✅ "Tower Details" 4列属性，合并提示 |
| Status.png | 5b2ee29e-8b3e-4246-b330-62e6bd0387c5 | banana2 | ✅ "Temple Status" / Gold / Temple HP / Wave |
| WaveStates.png | 6f1c2fd6-3034-4bc9-8289-3a667a347ff1 | banana2 | ✅ 3列：Preparing / Defending / Wave Clear |

### 2. Startup 封面图片（全部替换）

| 文件 | Tripo task_id | 模型 | 验证结果 |
|---|---|---|---|
| LotusCityStartCover.png | 1f8519b6-df31-4bf3-86db-bb89115aa299 | banana2 | ✅ "Lotus City" 主标题，START/EXIT 按钮 |
| LotusCityStartCoverPressed.png | 3b566363-9217-4cd0-b913-5be04cf962fb | banana2 | ✅ 同封面，START 按压态 |

### 3. 代码修改

**SpatialTowerDefenseGame.cs**
- `LocalizedTowerName()`（死代码）：3 个中文值 → 英文（Morning Bell / Sutra Guardian / Lotus Lamp）
- `TowerRoleDescription()`（死代码）：3 个中文值 → 英文（Area Purification / Heavy Strike / Frost Control）
- `EnemySkillSummary()`（死代码）：6 个中文多行字符串 → 英文
- 4 处 GameObject 名称：去除"Chinese"并改为纯英文描述名

**ProceduralFactory.cs**
- `RuntimeGlyphs` 常量：替换为纯 ASCII 集合，移除全部中文字符集

**ProjectSetup.cs**（Editor 脚本）
- `PlayerSettings.productName` → `"Lotus City"`
- `SetApplicationIdentifier` → `"com.codex.spatialtowerdefense.pico.en"`

### 4. 项目设置

**ProjectSettings/ProjectSettings.asset**
- `productName`: `一念莲城` → `"Lotus City"`
- `applicationIdentifier.Android`: `com.codex.spatialtowerdefense.pico` → `com.codex.spatialtowerdefense.pico.en`

## 字体

**字体文件**：`Assets/SpatialTowerDefense/Resources/Fonts/NotoSansSC-Regular.otf`
- 原版：为中文世界空间 UI 渲染而嵌入
- 英文版：保留（该字体支持 ASCII，ProjectSetup.cs 第 103 行有构建时字体存在性检查，保留可通过 Verify()）
- 游戏内实际渲染文字：全部以 PNG 图片形式烘焙，运行时字体仅用于 RuntimeGlyphs 数字/ASCII 动态叠加

## Tripo 调用清单（共 8 次）

| # | task_id | 用途 | 模型 | 状态 |
|---|---|---|---|---|
| 1 | 8bf9faa7-1f99-4fd6-93fb-e075db4511bd | Arsenal.png | banana2 | success |
| 2 | 92171dde-22e8-4295-9db8-29bc4759ff25 | Stats.png | banana2 | success |
| 3 | 1f8519b6-df31-4bf3-86db-bb89115aa299 | LotusCityStartCover.png | banana2 | success |
| 4 | d56a9db2-2b52-43fd-8ae8-5a7611d2cbb8 | Briefing.png | banana2 | success |
| 5 | 9ca5aef9-dc92-4404-98ec-452165651f6e | Guide.png | banana2 | success |
| 6 | 5b2ee29e-8b3e-4246-b330-62e6bd0387c5 | Status.png | banana2 | success |
| 7 | 6f1c2fd6-3034-4bc9-8289-3a667a347ff1 | WaveStates.png | banana2 | success |
| 8 | 3b566363-9217-4cd0-b913-5be04cf962fb | LotusCityStartCoverPressed.png | banana2 | success |

## CJK 扫描结果

扫描范围：`Assets/**/*.cs`、`Assets/**/*.unity`、`Assets/**/*.prefab`、`Assets/**/*.asset`、`ProjectSettings/ProjectSettings.asset`

**结果：0 处运行时 CJK 字符**

**豁免 2 处（编辑器脚本文件路径）：**
- `Assets/SpatialTowerDefense/Editor/SceneModelSnapshotUtility.cs:23`：`"Assets/模型资产"` — 指向实际存在的模型资产目录，编辑器专用脚本（namespace `PicoTowerDefenseEditor`），不属于玩家可见文字
- `Assets/SpatialTowerDefense/Editor/AuthoredSceneryRegression.cs:77`：`"Assets/模型资产/s1/tripo_..."` — 编辑器回归测试资源路径，同上

注：注释和日志中可能仍含中文，本次扫描排除了纯注释行（`//` 和 `*` 开头）。

## 截图清单（`VisualQA/EN-20261022/`）

| 文件 | 对应图片 | 验证状态 |
|---|---|---|
| 01_generated_image_url.png | Arsenal（建塔面板） | ✅ 目视验证通过 |
| 01_generated_image_url_2.png | Stats（塔详情面板） | ✅ 目视验证通过 |
| 01_generated_image_url_3.png | LotusCityStartCover（封面） | ✅ 目视验证通过 |
| 01_generated_image_url_4.png | Briefing（敌人情报） | ✅ 目视验证通过 |
| 01_generated_image_url_5.png | Guide（操作指南） | ✅ 目视验证通过 |
| 01_generated_image_url_6.png | Status（神庙状态 HUD） | ✅ 目视验证通过 |
| 01_generated_image_url_7.png | WaveStates（波次状态） | ✅ 目视验证通过 |
| 01_generated_image_url_8.png | LotusCityStartCoverPressed（封面按压态） | ✅ 目视验证通过 |

## APK 构建

- 目标路径：`Builds/EN/LotusCity-EN-PICO.apk`
- 状态：**待构建**（Unity 槽位在报告生成时均被 QuantumEcho-EN / GravityFrontline-EN 占用）
- 磁盘剩余：21GB（超过 12GB 最低门槛）
- 构建方法：`-executeMethod PicoTowerDefenseEditor.ProjectSetup.BuildDevelopmentApk`
- SHA-256：待填写（构建完成后更新）

## 已验证项

- 所有 8 张 UI 面板图片目视验证，无中文，风格保持（青灰绿底、暖石色轻衬线、淡墨线）
- ProductName = "Lotus City"，BundleId = com.codex.spatialtowerdefense.pico.en
- RuntimeGlyphs 无中文字符
- 死代码方法翻译完整
- 原工程文件未被触碰（仅独立 EN 工程修改）
- git 历史：基线提交 + 第二阶段提交，可完整回溯

## 未验证边界

1. **APK 实机测试**：没有 PICO 实体头显，无法做完整 XR 体验测试
2. **APK 构建**：Unity 槽位等待中，构建结果待定
3. **模拟器验证**：未获取 /tmp/pico-emulator.lock，模拟器画面未验证
4. **Verify() 是否通过**：ProjectSetup.Verify() 检查 GeneratedV5 所有纹理存在——新图片已替换，但 Unity 构建前无法执行 Verify() 确认
5. **动态数字槽位对齐**：新生成 Status.png 的三列分隔线位置与原版可能有细微差异，需真机确认数字叠加精度

## 没把握的翻译

- "晨钟" 原意为清晨钟声（Morning Bell），作为基础攻击塔名可能语义不够直接，但与游戏佛寺意境吻合，且与 `TowerTierName()` 中"Morning Bell Purification Platform"一致，予以保留
- "Delusion Fog" 的描述改为"Heals allies, silences towers"（图片中），代码中为"Fog Silences Towers"——图片和代码轻微不一致（图片更详细），实际游戏中图片版本显示，问题不大
