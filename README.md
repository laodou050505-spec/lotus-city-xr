# 《一念莲城》PICO 空间塔防

这是把 `http://localhost:8080` 的 three.js 塔防迁移到官方 Unity 6 的 PICO 空间游戏项目。它不是网页或平面展示：玩家扮演守灯人，在约 2.2 x 1.76 米的三层寺院沙盘中守护长明心灯。寺院、塔、心魔、状态牌和操作台都存在于世界空间，PICO 玩家可以真实绕桌行走，从任意方向观察和操作；没有头显时，也能在 Mac 上用鼠标和键盘模拟绕桌视角。

## 当前完成

- 官方 Unity `6000.5.5f1` 独立项目，不使用 Tuanjie。
- Android ARM64、IL2CPP、OpenGL ES 3、Activity 与 PICO Multiview 配置。
- 官方 PICO Unity SDK `6.0.0`、`PXR_Loader`、PICO 控制器输入、6DoF 追踪与触觉反馈。
- 三层寺院地编已使用用户模型库中的真实 FBX 重建：下层水岸、石桥、山门和古柏，中层石砌台地、放生池与回廊，上层主殿、石窟、主佛龛和山壁；高位佛龛和佛像只作为环境主体，不可攻击或部署。
- 沙盘是一座可环绕观察的三层不规则风化岩岛。三个战斗平台随山势起伏、通过台阶和桥相连，顶面仍保留稳定的摆放与拖动空间，不再出现早期粗制桌体和占位小模型。
- 棋盘只在作者保留的 21 个路线格和 45 个武器格渲染真实三维石砖：敌人路线专用乳白色方砖 `S-02` 模块，武器部署位专用低饱和苔绿色 `S-17` 模块。两种棋盘模型不会被放入寺院装饰布局，远离路线的逻辑格也不渲染石砖。
- 主殿、回廊、石窟、山门、桥、古柏、祈愿幡和石灯笼等非玩法布景集中由 `EditableSceneModelLayout` 管理。可在 Inspector 中独立调整位置、旋转、大小和显隐，不改变路线、塔位或玩法数据。
- 当前手工摆放的 39 个模型已保存到场景层级的 `Saved Scene Model Placements`（包含岩石、台阶、放生池等作者摆放内容）；打开摆放工具只会定位和预览，不会自动重排、统一缩放或补回已删除的格子。大佛、右侧拱桥和主殿仍由已保存布局/序列化条目提供。
- 10 x 8 网格、每格约 0.22 米、桌高 0.92 米的空间沙盘，四周留有可绕行距离。
- 200 初始灵力、20 心灯稳定度、8 波组合心魔。
- 6 类抽象心魔：妄念尘、贪相、无明兽、疑障、嗔火、痴雾；击败后褪去黑雾，化为金色微光回到心灯。
- 6 类心魔均已替换为用户模型，并在保持根节点严格沿路线运行的同时加入起伏、步态、侧摆、悬浮和转弯平滑，让出场与行进不再是机械平移。
- 3 类防御塔：晨钟净化塔偏范围与减速，经幢护法塔偏高伤害与范围净化，莲灯回向塔偏持续控制；伤害、射速、范围、弹速和净化效果随等级提升。
- 当前三类可操作防御塔的一级、二级、三级外观均已换成用户提供的晨钟塔、莲灯塔和经幢塔模型，升级通过结构层次而非单纯放大来表现。
- 手动拖动合成：把一座塔的模型拖到另一座塔模型附近即可自动吸附；同类型、同等级时松手合成，两个一级变二级，两个二级变三级，三级封顶。不会自行合成，无效拖动返回原格。
- 卡牌中的一级塔可直接拖到场景内同类型一级塔上升级；已经部署的塔也可随时拖到空闲绿色格位重新摆放。
- 右手守灯人法器：右手控制器瞄准心魔，右扳机释放净化光束；模拟扳机与数字扳机都支持防抖，短暂的射线抖动不会中断连射。左手专门负责卡牌、摆放、搬家和手动合成。
- 攻击拖尾、三类塔的净化圆环和右手法器命中特效已接入；放置、胜利音效和循环 BGM 使用许可清晰的音频文件，其余音效在素材缺失时使用程序化回退。
- 天空、主方向光、冷色补光和灯火点光已调整为柔和光比，以乳白石材、低饱和青绿、深木色和少量暖金为主，避免石材过曝。
- Android/PICO 启动时启用透明相机、PICO Video See Through 与 MR safeguard，使整座岩岛沙盘悬浮在真实房间中；桌面构建保留独立天空背景和鼠标绕桌测试。

## 无头显电脑测试

运行 `Builds/Desktop/SpatialTowerDefenseTest.app`，或在 Unity 打开主场景后点击 Play。

| 操作 | 电脑输入 |
| --- | --- |
| 指向格子、塔或空间按钮 | 移动鼠标 |
| 建塔、点击按钮 | 鼠标左键短按 |
| 合并塔 | 按住一座塔，拖到同类型同等级塔上后松开 |
| 绕桌环视 | 在空白处左键拖动，或使用右键/中键拖动 |
| 拉近或拉远 | 鼠标滚轮，或 `W` / `S` |
| 沿桌子转圈 | `A` / `D` 或左右方向键 |
| 降低或抬高观察角度 | `Q` / `E` |
| 选择箭塔、炮塔、冰塔 | `1` / `2` / `3` |
| 开始下一波 | `Space` |
| 玩家武器瞄准与射击 | 鼠标指向敌人，按住 `F` 或左 `Shift` |
| 恢复默认观察角度 | `R` |

拖动塔或卡牌时，不需要让射线精确命中目标碰撞体；模型中心进入目标附近后会自动识别并吸附。一级卡牌拖到场景中的同类一级塔附近，松手后会直接生成二级塔；目标塔下方绿色表示可升级，红色表示类型、等级或上限不符合。无效拖动不会改变塔和格子占用状态。

## PICO 双手柄分工

项目使用 PICO SDK 6.0.0 的官方左右手柄 Profile 与手柄模型，不是通用占位手柄。左手负责建造，右手负责守灯人攻击；交互成功、合成和命中分别触发对应手柄的触觉反馈。

| 操作 | PICO 输入 |
| --- | --- |
| 卡牌选择、指向、拖塔、搬家、合成 | 左手 6DoF 射线 + 左扳机 |
| 循环选择箭塔/炮塔/冰塔 | 左手 `A` |
| 开始下一波 | 左手 `X` |
| 玩家武器瞄准 | 右手 6DoF 射线（带橙色攻击线） |
| 玩家武器攻击 | 右扳机 |
| 把沙盘重新放到面前 | 右手 `B`，或同时握紧左右握把 |
| 操作反馈 | 左手交互/右手攻击各自触觉脉冲 |

启动后，沙盘会放在玩家前方约 1.1 米并保持房间坐标稳定。玩家可真实绕岩岛观察；重新定位只在主动按下重置操作时发生。PICO Android 版默认按 MR 透视体验配置，场景作为立体沙盘存在于房间空间中，而不是贴在一个平面屏幕上。

## 在 Unity Hub 打开

1. 在 Unity Hub 中添加本目录。
2. 使用 Unity `6000.5.5f1` 打开。
3. 打开 `Assets/SpatialTowerDefense/Scenes/SpatialDefense.unity`。
4. 点击 Play 进行电脑测试。

Unity 菜单提供以下命令：

- `Tools > Spatial Tower Defense > Verify Project`：检查玩法规则和 PICO/Android 配置。
- `Tools > Spatial Tower Defense > Render Preview`：生成项目预览图。
- `Tools > Spatial Tower Defense > Build macOS Desktop Test`：生成无头显测试版。
- `Tools > Spatial Tower Defense > Build PICO Development APK`：生成 PICO Android 开发包。
- `Tools > Spatial Tower Defense > Open Scene Model Placement`：打开主场景并选中可编辑的场景模型布局。

在 `EditableSceneModelLayout` Inspector 中，使用 `Add Scene Model` / 每行的 `Delete` 增减布景模型；展开条目后编辑 `Resource Path`、`Position`、`Rotation` 和 `Uniform Scale`，用 `Show` 临时隐藏模型，最后点击 `Rebuild Preview`。这里只管理装饰模型，不会改变敌人路线或武器格；棋盘专用的 `S-02` 和 `S-17` 会被自动拒绝，防止石砖出现在其他地方。

## 构建输出

- macOS：`Builds/Desktop/SpatialTowerDefenseTest.app`
- PICO APK：`Builds/PICO/SpatialTowerDefensePICO.apk`
- 预览：`SpatialPrototypePreview.png`

2026-08-05 最终无头显验证结果：

- Unity `Verify Project` 完成，日志输出 `[Yi Nian Lotus City] VERIFY PASSED`，脚本与 PICO/Android 配置检查通过。
- `SpatialPrototypePreview.png` 已重新渲染，用于检查场景非空、三层构图和模型朝向。
- macOS 测试版构建成功，约 366 MB；已从打包后的播放器进行约 10 秒无图形启动检查，未发现项目脚本异常。Unity AppUI 缺少默认设置的提示为非阻塞警告。
- PICO APK 构建成功，大小 `92,486,371` bytes，SHA-256 为 `7e326ff33cd6af56672ef19599c3ef674173672b2364898b33a9fa57f9846b3a`（校验值也写入 `Builds/PICO/SHA256.txt`）。

没有头显时，macOS 版可以验证完整玩法、绕桌视角、鼠标拖动合成、塔移动、敌人自然动画、守灯人法器和空间布局。历史 PICO Emulator 运行记录已验证 APK 安装、PICO VR Activity 和首帧沉浸画面，但 SDK 6.0.0 在该模拟器中约 14 秒后会因 `NativeLogLevelChange` 原生异常退出，因此不作为完整玩法通过依据。本轮最终 APK 已完成编译与配置验证，尚未经过真实 PICO 头显、真实双手柄追踪、透视画面、触觉或舒适度测试。

当前棋盘共有 66 块三维砖（21 块白色 `S-02` 路线砖、45 块绿色 `S-17` 武器砖），是首次真机性能测试的重点风险。真机验收时应首先记录最拥挤波次帧率，再根据结果进行低模替换、静态合批或网格合并，不能仅凭电脑帧率判断 PICO 性能。

## 以后新建 PICO Unity 项目

官方 SDK 固定在 `/Users/ld/Library/PICO/UnitySDK/6.0.0/PICO-Unity-SDK`。已安装两个全局命令：

```bash
pico-unity-install /完整路径/你的Unity项目
pico-unity-new 项目名 /项目父目录
```

和 Codex 对话时，最短可以说：

> 这是一个 PICO Unity 项目。

推荐完整说法：

> 用官方 Unity 制作一个 PICO XR 项目，目标 Android/PICO，目前没有头显，需要电脑和 PICO Emulator 测试。

这会明确要求官方 Unity、PICO XR SDK、Android/PICO 配置及无头显测试路径。

## 目录与素材

- `Assets/SpatialTowerDefense/Runtime`：规则、空间输入、MR 呈现、可编辑场景布局、战斗对象与视觉特效。
- `Assets/SpatialTowerDefense/Editor/ProjectSetup.cs`：场景生成、PICO 配置、验证、预览和构建。
- `Assets/SpatialTowerDefense/Scenes/SpatialDefense.unity`：主场景。
- `Assets/XR`：PICO XR Loader 和 PXR 设置。
- `Docs/PRODUCTION_ROADMAP.md`：制作与验收流程。
- `Docs/ASSET_ATTRIBUTION.md`：素材来源和授权审计。

外部美术和音频必须先通过 `ange-embed` 资源库检查来源与许可证。当前场景、塔和敌人使用用户提供的 FBX 模型；三项外部音频具有可核验的作者、来源和 CC-BY 4.0 许可，详见 `Docs/ASSET_ATTRIBUTION.md`。检索到但授权字段不完整的候选资源没有打包进项目。
