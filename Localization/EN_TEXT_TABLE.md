# Lotus City — EN Text Translation Table
生成时间：2026-10-22（v3 rework）

## 术语表（全游戏统一）

| 中文术语 | 英文术语 | 说明 |
|---|---|---|
| 一念莲城 | Lotus City | 游戏名 |
| 灵光 | Spirit Light | 游戏内货币（v1/v2 误为 Gold，v3 已修正） |
| 神庙 | Temple | 防守目标 |
| 心灯生命 | Heart Lamp | 神庙生命值（v2 误为 Temple HP，v3 已修正） |
| 波次 | Wave | 关卡进程单位 |
| 晨钟 | Morning Bell | 基础塔（范围净化） |
| 经轮守卫 | Prayer Wheel Guardian | 重型塔（v1/v2 误为 Sutra Guardian，v3 已修正） |
| 莲花灯 | Lotus Lamp | 冰霜减速塔 |
| 游尘 | Drifting Dust | 普通敌人（v1/v2 误为 Restless Dust，v3 已修正） |
| 疾行执念 | Fleeting Obsession | 快速偷灵光敌人（v1/v2 误为 Grasping Burden，v3 已修正） |
| 无明兽 | Ignorance Beast | Boss 敌人 |
| 疑甲 | Doubt Carapace | 护盾敌人 |
| 怒焰 | Wrath Flame | 加速/分裂敌人（v1/v2 误为 Anger Flame，v3 已修正） |
| 妄雾 | Delusion Fog | 治疗/沉默敌人 |
| 防守中 | Defending | 进攻阶段（WaveStates 列1） |
| 守护成功 | Temple Saved | 波次胜利（WaveStates 列2，v2 误为 Temple Secured） |
| 神庙失守 | Temple Lost | 神庙被摧毁（WaveStates 列3） |

---

## 翻译对照表（v3）

| key | 原中文 | 英文（v3） | 来源文件 | 使用场景 | 状态 |
|---|---|---|---|---|---|
| product_name | 一念莲城 | Lotus City | ProjectSettings.asset / ProjectSetup.cs | 应用名、封面标题 | ✅ 已替换 |
| bundle_id | com.codex.spatialtowerdefense.pico | com.codex.spatialtowerdefense.pico.en | ProjectSettings.asset / ProjectSetup.cs | Android 包名 | ✅ 已替换 |
| cover_title | 一念莲城 | Lotus City | LotusCityStartCover.png（图片烘焙） | 封面主标题 | ✅ 已生成 |
| cover_subtitle | （无） | ◇ TOWER DEFENSE ◇ | LotusCityStartCover.png | 封面副标题 | ✅ 已生成 |
| arsenal_tower1_name | 晨钟 | Morning Bell | Arsenal.png（图片烘焙 v3） | 建塔面板第1列塔名 | ✅ v3 已生成 |
| arsenal_tower1_cost | 50 灵光 | 50 Spirit Light | Arsenal.png | 建塔成本 | ✅ v3 已生成 |
| arsenal_tower1_role | 范围净化 | Area Purify | Arsenal.png | 塔职能描述 | ✅ v3 已生成 |
| arsenal_tower2_name | 经轮守卫 | Prayer Wheel Guardian | Arsenal.png | 建塔面板第2列塔名 | ✅ v3 已生成 |
| arsenal_tower2_cost | 120 灵光 | 120 Spirit Light | Arsenal.png | 建塔成本 | ✅ v3 已生成 |
| arsenal_tower2_role | 高伤净化 | Heavy Purify | Arsenal.png | 塔职能描述 | ✅ v3 已生成 |
| arsenal_tower3_name | 莲花灯 | Lotus Lamp | Arsenal.png | 建塔面板第3列塔名 | ✅ v3 已生成 |
| arsenal_tower3_cost | 90 灵光 | 90 Spirit Light | Arsenal.png | 建塔成本 | ✅ v3 已生成 |
| arsenal_tower3_role | 减速控制 | Slowing Control | Arsenal.png | 塔职能描述 | ✅ v3 已生成 |
| arsenal_start_wave | 开始进攻 | Start Wave | Arsenal.png | 第4列按钮 | ✅ v3 已生成 |
| arsenal_start_button | 左Y键 | [Left Y] | Arsenal.png | 开始波次按键提示 | ✅ v3 已生成 |
| briefing_title | 敌情预报 | Enemy Intel | Briefing.png（图片烘焙 v3） | 波次预告面板标题 | ✅ v3 已生成 |
| enemy_drifting_dust | 游尘 | Drifting Dust | Briefing.png | 敌人名 行1 | ✅ v3 已生成 |
| enemy_drifting_dust_desc | 成群突袭 | Swarming raid | Briefing.png | 敌人描述 | ✅ v3 已生成 |
| enemy_fleeting_obsession | 疾行执念 | Fleeting Obsession | Briefing.png | 敌人名 行2 | ✅ v3 已生成 |
| enemy_fleeting_obsession_desc | 快速偷取灵光 | Fast, steals Spirit Light | Briefing.png | 敌人描述 | ✅ v3 已生成 |
| enemy_ignorance_beast | 无明兽 | Ignorance Beast | Briefing.png | 敌人名 行3 | ✅ v3 已生成 |
| enemy_ignorance_beast_desc | 高生命与护甲 | High HP and armor | Briefing.png | 敌人描述 | ✅ v3 已生成 |
| enemy_doubt_carapace | 疑甲 | Doubt Carapace | Briefing.png | 敌人名 行4 | ✅ v3 已生成 |
| enemy_doubt_carapace_desc | 石壳护盾 | Stone-shell shield | Briefing.png | 敌人描述 | ✅ v3 已生成 |
| enemy_wrath_flame | 怒焰 | Wrath Flame | Briefing.png | 敌人名 行5 | ✅ v3 已生成 |
| enemy_wrath_flame_desc | 加速同伴与分裂 | Hastens allies, splits | Briefing.png | 敌人描述 | ✅ v3 已生成 |
| enemy_delusion_fog | 妄雾 | Delusion Fog | Briefing.png | 敌人名 行6 | ✅ v3 已生成 |
| enemy_delusion_fog_desc | 治疗同伴与沉默 | Heals allies, silences towers | Briefing.png | 敌人描述（v1 截断，v3 已修正） | ✅ v3 已生成 |
| guide_title | 双手操作 | Two-Hand Controls | Guide.png（图片烘焙 v3） | 控制器操作面板标题 | ✅ v3 已生成 |
| guide_left_trigger | 左扳机 | Left Trigger | Guide.png | 左手控制说明 行1 | ✅ v3 已生成 |
| guide_left_trigger_act | 选择·放置·拖拽 | Select  ·  Place  ·  Drag | Guide.png | 左扳机对应操作 | ✅ v3 已生成 |
| guide_left_x | 左X键 | Left X | Guide.png | 左手控制说明 行2 | ✅ v3 已生成 |
| guide_left_x_act | 切换防御塔 | Switch Tower | Guide.png | 左X键对应操作 | ✅ v3 已生成 |
| guide_left_y | 左Y键 | Left Y | Guide.png | 左手控制说明 行3 | ✅ v3 已生成 |
| guide_left_y_act | 开始波次 | Start Wave | Guide.png | 左Y键对应操作 | ✅ v3 已生成 |
| guide_right_trigger | 右扳机 | Right Trigger | Guide.png | 右手控制说明 行4 | ✅ v3 已生成 |
| guide_right_trigger_act | 净化敌人 | Purify Enemies | Guide.png | 右扳机对应操作 | ✅ v3 已生成 |
| guide_right_b | 右B键 | Right B | Guide.png | 右手控制说明 行5 | ✅ v3 已生成 |
| guide_right_b_act | 重置视角 | Reset View | Guide.png | 右B键对应操作 | ✅ v3 已生成 |
| stats_title | 防御塔详情 | Tower Details | Stats.png（图片烘焙 v3） | 塔悬停信息面板标题 | ✅ v3 已生成 |
| stats_level | 等级 | Level | Stats.png | 塔属性列1 | ✅ v3 已生成 |
| stats_attack | 攻击 | Attack | Stats.png | 塔属性列2 | ✅ v3 已生成 |
| stats_speed | 攻速 | Speed | Stats.png | 塔属性列3（v2 误为 Rate，v3 已修正） | ✅ v3 已生成 |
| stats_range | 范围 | Range | Stats.png | 塔属性列4 | ✅ v3 已生成 |
| stats_merge_hint | 左手拖动·同级合成 | Left-hand drag  ·  Merge same tier | Stats.png | 升级提示 | ✅ v3 已生成 |
| status_col1 | 灵光余额 | Spirit Light | Status.png（图片烘焙 v3） | HUD 货币栏标签 | ✅ v3 已生成 |
| status_col2 | 心灯生命 | Heart Lamp | Status.png | HUD 生命栏标签（v2 误为 Temple HP，v3 已修正） | ✅ v3 已生成 |
| status_col3 | 当前波次 | Wave | Status.png | HUD 波次栏标签 | ✅ v3 已生成 |
| wave_active | 防守中 | Defending | WaveStates.png 列1（图片烘焙 v3） | 波次进行中 | ✅ v3 已生成 |
| wave_active_sub | 净化敌人 | Purify the enemies | WaveStates.png 列1 | Defending 副文 | ✅ v3 已生成 |
| wave_saved | 守护成功 | Temple Saved | WaveStates.png 列2 | 神庙守住 | ✅ v3 已生成 |
| wave_saved_sub | 心灯长明 | The Heart Lamp burns on | WaveStates.png 列2 | Temple Saved 副文 | ✅ v3 已生成 |
| wave_lost | 神庙失守 | Temple Lost | WaveStates.png 列3 | 神庙被摧毁 | ✅ v3 已生成 |
| wave_lost_sub | 重新挑战 | Try Again | WaveStates.png 列3 | Temple Lost 副文 | ✅ v3 已生成 |

---

## 代码更新（v3）

| 方法/字段 | 旧值 | 新值 | 文件 |
|---|---|---|---|
| GameDefinitions.cs EnemyKind.Runner | Restless Dust | Drifting Dust | GameDefinitions.cs |
| GameDefinitions.cs EnemyKind.Splitter | Grasping Burden | Fleeting Obsession | GameDefinitions.cs |
| GameDefinitions.cs EnemyKind.Support | Anger Flame | Wrath Flame | GameDefinitions.cs |
| GameDefinitions.cs EnemyKind.Tank | Ignorance Beast Boss | Ignorance Beast | GameDefinitions.cs |
| GameDefinitions.cs TowerKind.Cannon | Sutra Guardian Pillar/Wheel Platform/Gate | Prayer Wheel Guardian Pillar/Wheel Platform/Gate | GameDefinitions.cs |
| SpatialTowerDefenseGame.cs LocalizedTowerName(Cannon) | Sutra Guardian | Prayer Wheel Guardian | SpatialTowerDefenseGame.cs |
| SpatialTowerDefenseGame.cs TowerRoleDescription(Cannon) | Heavy Strike | Heavy Purify | SpatialTowerDefenseGame.cs |
| SpatialTowerDefenseGame.cs EnemySkillSummary(Runner) | Grasping Burden / Swift / Steals 10 Gold | Fleeting Obsession / Fast, steals Spirit Light | SpatialTowerDefenseGame.cs |
| SpatialTowerDefenseGame.cs EnemySkillSummary(Tank) | Ignorance Beast Boss | Ignorance Beast | SpatialTowerDefenseGame.cs |
| SpatialTowerDefenseGame.cs EnemySkillSummary(Splitter) | Anger Flame / Hastens Allies | Wrath Flame / Hastens allies, splits | SpatialTowerDefenseGame.cs |
| SpatialTowerDefenseGame.cs EnemySkillSummary(Support) | Fog Silences Towers | Heals allies, silences towers | SpatialTowerDefenseGame.cs |
| SpatialTowerDefenseGame.cs EnemySkillSummary(_) | Restless Dust | Drifting Dust | SpatialTowerDefenseGame.cs |

---

## CJK 扫描豁免项

| 位置 | 内容 | 豁免理由 |
|---|---|---|
| SceneModelSnapshotUtility.cs:23 | `"Assets/模型资产"` | 编辑器脚本文件路径，指向实际存在的 Assets/模型资产/ 目录，不属于玩家可见文字 |
| AuthoredSceneryRegression.cs:77 | `"Assets/模型资产/s1/tripo_..."` | 同上，编辑器回归测试资源路径 |
