# 《一念莲城》素材来源与授权审计

## 当前打包素材

当前版本使用项目内模型和程序化场景，同时加入三项许可清晰的外部音频：

- `Assets/Resources/PolygonNatureBiomes/`：用户提供的 `POLYGON Meadow Forest - Nature Biomes -1.8.0.unitypackage` 中筛选的内置渲染管线地面山丘、断崖、草地、材质、纹理和着色器，用于三阶浮岛地编。该包按 Asset Store/Synty Studios 资产处理，仅用于用户授权的项目，不随本项目单独再分发。

- `Assets/Resources/Audio/LotusCityMusic.wav`：由 `Dryad's Dream` FLAC 解码为 WAV，Unity 构建时再按目标平台压缩，作为循环背景音乐。
- `Assets/Resources/Audio/Victory.wav`：胜利提示音。
- `Assets/Resources/Audio/TowerPlace.wav`：低沉的实体落台声，用于建筑放置反馈。

以下内容由项目运行时代码生成，不依赖第三方媒体文件：

- 三层寺院（山门、回廊、主殿、佛龛）、防御建筑、抽象心魔、桌面、格子、长明心灯、控制器占位模型和合并特效。
- 程序化天空盒、方向光、补光和点光源。
- 建塔、合并、开波、三类塔武器、玩家武器、胜负提示音与循环背景音乐回退。

官方 PICO 控制器模型和控制器 Profile 来自已安装的 PICO Unity SDK `6.0.0`，随 SDK 使用，不是单独下载的外部媒体素材。

外部音频的署名和原始下载地址见下方记录。

## ange-embed 检索记录

按项目要求，任何美术和音频都优先检索全局 `ange-embed` 资源库。

- 2026-08-04 按 v2 流程检查了 `h5_respack_v1` 场景道具、`v2_music_realistic` 音乐、`v2_sfx_character` 攻击音效、`v2_sfx_foley` 环境音效和 `ui_audio` 反馈音效。
- 场景候选带有 `tripo3d` 来源标签，但没有明确作者、原始来源和许可证；音乐与音效候选提供文件 URL 和描述，但缺少完整可再分发授权字段。
- 来源字段不完整的资源仍未导入；当前使用的三项外部音频来自可核验的 OpenGameArt 条目，并保留原作者与 CC-BY 4.0 许可。

## 已导入外部音频

| 项目内文件 | 素材 | 作者 | 许可证 | 原始条目 | 下载地址 |
| --- | --- | --- | --- | --- | --- |
| `Resources/Audio/LotusCityMusic.wav` | Dryad's Dream | Tsorthan Grove | CC-BY 4.0 | https://opengameart.org/content/dryads-dream | https://cdn-hm.holymolly.ai/music/3cb01fa7-f42f-4df4-a1c8-3d0e828f805c.flac |
| `Resources/Audio/Victory.wav` | Won! | spuispuin | CC-BY 4.0 | https://opengameart.org/content/won-orchestral-winning-jingle | https://cdn-hm.holymolly.ai/music/b75094b1-3315-4e65-b328-b05e1e4cbd45.wav |
| `Resources/Audio/TowerPlace.wav` | Cup On Table Sfx Sound Effect | Nicole Marie T | CC-BY 4.0 | https://opengameart.org/content/cup-on-table-sfx-sound-effect | https://cdn-hm.holymolly.ai/music/be911958-3700-42cd-9392-0d8686c96ea5.wav |

背景音乐仅做格式转换（FLAC -> WAV），没有改变原作内容。项目发布时应一并保留本文件和 CC-BY 4.0 署名。

## 预留替换路径

授权清晰的音频可以放入 `Assets/SpatialTowerDefense/Resources/Audio/`，使用以下资源名：

- `TowerPlace`
- `TowerUpgrade`
- `WaveStart`
- `ArrowShot`
- `CannonShot`
- `FrostShot`
- `BattleMusic`

导入后必须在本文件追加素材名称、作者、来源 URL、许可证、下载日期和项目内路径。
