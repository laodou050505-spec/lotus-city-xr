# Lotus City — EN Text Translation Table
生成时间：2026-10-22

## 术语表（全游戏统一）

| 中文术语 | 英文术语 | 说明 |
|---|---|---|
| 一念莲城 | Lotus City | 游戏名 |
| 灵光 | Gold | 游戏内货币 |
| 神庙 | Temple | 防守目标 |
| 生命 | HP | 神庙生命值 |
| 波次 | Wave | 关卡进程单位 |
| 晨钟 | Morning Bell | 基础塔（快速音波） |
| 经轮守卫 | Sutra Guardian | 重型塔（范围）|
| 莲花灯 / 供灯塔 | Lotus Lamp | 冰霜塔 |
| 游尘 | Restless Dust | 普通敌人 |
| 疾行执念 | Grasping Burden | 快速偷钱敌人 |
| 无明兽 | Ignorance Beast | Boss 敌人 |
| 疑甲 | Doubt Carapace | 护盾敌人 |
| 怒焰 | Anger Flame | 加速/分裂敌人 |
| 妄雾 | Delusion Fog | 治疗/沉默敌人 |
| 准备/防守 | Defending | 进攻阶段（WaveStates 列1） |
| 胜利完成 | Temple Secured | 波次胜利（WaveStates 列2） |
| 失败 | Temple Lost | 神庙被摧毁（WaveStates 列3） |
| 范围伤害 | Powerful Purification | 经轮守卫攻击类型（v2 更新） |
| 减速控制 | Frost Control | 莲花灯攻击类型 |
| 快速音波飞矢 | Area Purification | 晨钟攻击类型 |

---

## 翻译对照表

| key | 原中文 | 英文 | 来源文件:行 | 使用场景 | 状态 |
|---|---|---|---|---|---|
| product_name | 一念莲城 | Lotus City | ProjectSettings.asset:16 / ProjectSetup.cs:852 | 应用名、封面标题 | ✅ 已替换 |
| bundle_id | com.codex.spatialtowerdefense.pico | com.codex.spatialtowerdefense.pico.en | ProjectSettings.asset:173 / ProjectSetup.cs:856 | Android 包名 | ✅ 已替换 |
| cover_title | 一念莲城 | Lotus City | LotusCityStartCover.png (图片烘焙) | 封面主标题 | ✅ v2 已生成 |
| cover_subtitle | （无对应中文副标题） | ◇ TOWER DEFENSE ◇ | LotusCityStartCover.png | 封面副标题（v1 误写"◇ CITY ◇"，v2 已修正） | ✅ v2 已修正 |
| cover_start | 开始 | START | LotusCityStartCover.png | 封面开始按钮 | ✅ 原图已有英文 |
| cover_exit | 退出 | EXIT | LotusCityStartCover.png | 封面退出按钮 | ✅ 原图已有英文 |
| arsenal_tower1_name | 晨钟 | Morning Bell | Arsenal.png (图片烘焙) | 建塔面板第1列塔名 | ✅ 新图已生成 |
| arsenal_tower1_cost | 50 灵光 | 50 Gold | Arsenal.png | 建塔成本 | ✅ 新图已生成 |
| arsenal_tower1_role | 区域净化 | Area Purification | Arsenal.png | 塔职能描述 | ✅ 新图已生成 |
| arsenal_tower2_name | 经轮守卫 | Sutra Guardian | Arsenal.png | 建塔面板第2列塔名 | ✅ 新图已生成 |
| arsenal_tower2_cost | 120 灵光 | 120 Gold | Arsenal.png | 建塔成本 | ✅ 新图已生成 |
| arsenal_tower2_role | 重击 | Powerful Purification | Arsenal.png | 塔职能描述（v2 更新，v1 误为 Heavy Strike） | ✅ v2 已修正 |
| arsenal_tower3_name | 莲花灯 | Lotus Lamp | Arsenal.png | 建塔面板第3列塔名 | ✅ 新图已生成 |
| arsenal_tower3_cost | 90 灵光 | 90 Gold | Arsenal.png | 建塔成本 | ✅ 新图已生成 |
| arsenal_tower3_role | 冰霜控制 | Frost Control | Arsenal.png | 塔职能描述 | ✅ 新图已生成 |
| arsenal_start_wave | 开始进攻 | Start Wave | Arsenal.png | 第4列按钮 | ✅ 新图已生成 |
| arsenal_start_button | 左手 Y 键 | Left Y | Arsenal.png | 开始波次提示 | ✅ 新图已生成 |
| briefing_title | 敌人情报 | Enemy Intel | Briefing.png (图片烘焙) | 波次预告面板标题 | ✅ 新图已生成 |
| enemy_restless_dust | 游尘 | Restless Dust | Briefing.png | 敌人名 行1 | ✅ 新图已生成 |
| enemy_restless_dust_desc | 成群突袭 | Swarming rush | Briefing.png | 敌人描述 | ✅ 新图已生成 |
| enemy_grasping_burden | 疾行执念 | Grasping Burden | Briefing.png | 敌人名 行2 | ✅ 新图已生成 |
| enemy_grasping_burden_desc | 快速，偷取灵光 | Swift, steals Gold | Briefing.png | 敌人描述 | ✅ 新图已生成 |
| enemy_ignorance_beast | 无明兽 | Ignorance Beast | Briefing.png | 敌人名 行3 | ✅ 新图已生成 |
| enemy_ignorance_beast_desc | 高生命高护甲，首领 | High HP and Armor, Boss | Briefing.png | 敌人描述 | ✅ 新图已生成 |
| enemy_doubt_carapace | 疑甲 | Doubt Carapace | Briefing.png | 敌人名 行4 | ✅ 新图已生成 |
| enemy_doubt_carapace_desc | 石壳护盾 | Stone shell shield | Briefing.png | 敌人描述 | ✅ 新图已生成 |
| enemy_anger_flame | 怒焰 | Anger Flame | Briefing.png | 敌人名 行5 | ✅ 新图已生成 |
| enemy_anger_flame_desc | 加速同伴，分裂 | Hastens allies, splits | Briefing.png | 敌人描述 | ✅ 新图已生成 |
| enemy_delusion_fog | 妄雾 | Delusion Fog | Briefing.png | 敌人名 行6 | ✅ 新图已生成 |
| enemy_delusion_fog_desc | 治愈同伴，沉默防御塔 | Silences towers | Briefing.png | 敌人描述（v2 更新，v1 误为"Heals allies, silences towers"） | ✅ v2 已修正 |
| guide_title | 操作指南 | Controls Guide | Guide.png (图片烘焙) | 双手操作提示面板 | ✅ 新图已生成 |
| guide_left_grip | 左手握把：放置塔 | Grip: Place Tower | Guide.png | 左手控制器说明 | ✅ 新图已生成 |
| guide_left_trigger | 左手扳机：选择 | Trigger: Select | Guide.png | 左手控制器说明 | ✅ 新图已生成 |
| guide_right_grip | 右手握把：合并塔 | Grip: Merge Tower | Guide.png | 右手控制器说明 | ✅ 新图已生成 |
| guide_right_trigger | 右手扳机：确认 | Trigger: Confirm | Guide.png | 右手控制器说明 | ✅ 新图已生成 |
| guide_note | 放置时保持双手稳定 | Hold both hands steady during placement | Guide.png | 操作注意事项 | ✅ 新图已生成 |
| stats_title | 塔属性 | Tower Details | Stats.png (图片烘焙) | 塔悬停信息面板标题 | ✅ 新图已生成 |
| stats_level | 等级 | Level | Stats.png | 塔属性列1 | ✅ 新图已生成 |
| stats_attack | 攻击力 | Attack | Stats.png | 塔属性列2 | ✅ 新图已生成 |
| stats_rate | 攻速 | Rate | Stats.png | 塔属性列3 | ✅ 新图已生成 |
| stats_range | 范围 | Range | Stats.png | 塔属性列4 | ✅ 新图已生成 |
| stats_merge_hint | 拖拽合并·同级 | Drag to Merge · Same Tier | Stats.png | 升级提示 | ✅ 新图已生成 |
| status_col1 | 灵光 | Gold Balance | Status.png (图片烘焙) | HUD 货币栏标签（v2 更新，无独立标题行） | ✅ v2 已生成 |
| status_col2 | 神庙生命 | Temple HP | Status.png | HUD 生命栏标签 | ✅ v2 已生成 |
| status_col3 | 波次 | Current Wave | Status.png | HUD 波次栏标签（v2 更新，v1 为"Wave"） | ✅ v2 已生成 |
| wave_active | 准备/防守 | Defending | WaveStates.png 列1 (图片烘焙) | 进攻波次进行中（副文"Purify the Enemies"） | ✅ v2 已生成 |
| wave_secured | 胜利/完成 | Temple Secured | WaveStates.png 列2 | 神庙守住（副文"The Light Endures"） | ✅ v2 已生成 |
| wave_lost | 失败 | Temple Lost | WaveStates.png 列3 | 神庙被摧毁（副文"Try Again"） | ✅ v2 已生成 |

---

## 死代码翻译（已改入代码，防未来启用）

| 方法 | 中文值 | 英文值 | 文件:行 |
|---|---|---|---|
| LocalizedTowerName(Cannon) | 经轮守卫 | Sutra Guardian | SpatialTowerDefenseGame.cs:1963 |
| LocalizedTowerName(Frost) | 莲花灯 | Lotus Lamp | SpatialTowerDefenseGame.cs:1964 |
| LocalizedTowerName(_) | 晨钟 | Morning Bell | SpatialTowerDefenseGame.cs:1965 |
| TowerRoleDescription(Cannon) | 范围伤害 | Heavy Strike | SpatialTowerDefenseGame.cs:1970 |
| TowerRoleDescription(Frost) | 减速控制 | Frost Control | SpatialTowerDefenseGame.cs:1971 |
| TowerRoleDescription(_) | 快速音波飞矢 | Area Purification | SpatialTowerDefenseGame.cs:1972 |
| EnemySkillSummary(Runner) | 疾行执念 ×N\n快速 / 偷取 10 灵光 | Grasping Burden ×N\nSwift / Steals 10 Gold | SpatialTowerDefenseGame.cs:3469 |
| EnemySkillSummary(Tank) | 无明兽首领 ×N\n800 生命 / 38% 护甲\n神庙伤害 4 | Ignorance Beast Boss ×N\n800 HP / 38% Armor\nTemple Damage 4 | SpatialTowerDefenseGame.cs:3470 |
| EnemySkillSummary(Shield) | 疑甲 ×N\n石壳 / 110 护盾 | Doubt Carapace ×N\nStone Shell / 110 Shield | SpatialTowerDefenseGame.cs:3471 |
| EnemySkillSummary(Splitter) | 怒焰 ×N\n加速同伴 / 分裂 | Anger Flame ×N\nHastens Allies / Splits | SpatialTowerDefenseGame.cs:3472 |
| EnemySkillSummary(Support) | 妄雾 ×N\n雾气沉默防御塔 | Delusion Fog ×N\nFog Silences Towers | SpatialTowerDefenseGame.cs:3473 |
| EnemySkillSummary(_) | 游尘 ×N\n成群突袭 | Restless Dust ×N\nSwarming Rush | SpatialTowerDefenseGame.cs:3474 |

---

## CJK 扫描豁免项

| 位置 | 内容 | 豁免理由 |
|---|---|---|
| SceneModelSnapshotUtility.cs:23 | `"Assets/模型资产"` | 编辑器脚本文件路径，指向实际存在的 Assets/模型资产/ 目录，不属于玩家可见文字 |
| AuthoredSceneryRegression.cs:77 | `"Assets/模型资产/s1/tripo_..."` | 同上，编辑器回归测试资源路径 |
