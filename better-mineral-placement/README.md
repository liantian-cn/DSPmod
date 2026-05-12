# 更好的矿物位置

英文目录名：`better-mineral-placement`

这份文档基于 `Assembly-CSharp` 反编译源码，目标是回答一件事：

游戏里星球矿物资源到底是怎么创建出来的，以及后续做“更好的矿物位置”mod 时，最适合改哪一层。

## 结论先说

《戴森球计划》的矿物位置，不是在 `PlanetFactory` 首次加载时“随机摆出来”的。

真正决定矿物位置的核心阶段，是行星原始数据生成阶段：

1. `PlanetData.RegenerateRawDataImmediately()` 生成地形、植被、矿物。
2. `PlanetAlgorithm.GenerateVeins()` 把矿脉实例写进 `PlanetRawData.veinPool`。
3. `PlanetData.SummarizeVeinGroups()` 再把这些矿脉按 `groupIndex` 汇总成矿组。
4. 等星球工厂加载时，`PlanetFactory` 只是把已经生成好的矿脉数据复制出来，再创建模型、碰撞和采矿显示。

所以如果 mod 的目标是“优化矿物位置”，最值得改的是 `GenerateVeins()` 前后这层，而不是矿机或 UI 层。

## 关键调用链

### 1. 行星生成阶段

`PlanetData.RegenerateRawDataImmediately()` 会重建行星原始数据：

- `GenerateTerrain(...)`
- `CalcWaterPercent()`
- `GenerateVegetables()`
- `GenerateVeins()`
- `SummarizeVeinGroups()`
- `GenBirthPoints()`

源码位置：

- `Assembly-CSharp/PlanetData.cs:1454-1474`

也就是说，矿物生成和地形生成是同一批流程里的。

### 2. 选择哪一个矿物生成算法

每个星球先从主题里拿到 `algoId`：

- `planet.theme` 先被选出
- 然后从 `themeProto.Algos` 里随机取一个 `planet.algoId`

源码位置：

- `Assembly-CSharp/PlanetGen.cs:397-404`

之后 `PlanetModelingManager.Algorithm(planet)` 决定实例化哪个算法类：

- `PlanetAlgorithm0`
- `PlanetAlgorithm1` ... `PlanetAlgorithm14`

源码位置：

- `Assembly-CSharp/PlanetModelingManager.cs:702-724`

其中和矿物生成直接相关的重写类有：

- `PlanetAlgorithm` 基类：默认矿物生成逻辑
- `PlanetAlgorithm7`
- `PlanetAlgorithm11`
- `PlanetAlgorithm12`
- `PlanetAlgorithm13`
- `PlanetAlgorithm0`：空实现，不生成矿

## 矿物数据结构

### `VeinData`

单个矿点实例是 `VeinData`，核心字段：

- `type`：矿物类型
- `groupIndex`：属于哪个矿组
- `amount`：矿量
- `productId`：采出来的物品
- `pos`：世界方向上的位置

源码位置：

- `Assembly-CSharp/VeinData.cs:4-48`

矿物类型枚举：

- `Iron`
- `Copper`
- `Silicium`
- `Titanium`
- `Stone`
- `Coal`
- `Oil`
- `Fireice`
- `Diamond`
- `Fractal`
- `Crysrub`
- `Grat`
- `Bamboo`
- `Mag`

源码位置：

- `Assembly-CSharp/EVeinType.cs:1-18`

### `VeinGroup`

矿组不是独立生成的，而是之后汇总出来的。

`PlanetData.SummarizeVeinGroups()` 会遍历 `data.veinPool`，按照每个 `VeinData.groupIndex` 累加：

- `type`
- `pos`
- `count`
- `amount`

最后把组中心 `Normalize()` 成单位向量方向。

源码位置：

- `Assembly-CSharp/PlanetData.cs:862-903`

这意味着：

- “矿点”是原子单位
- “矿组”只是对矿点的统计和展示分组

如果你想做“更好的矿物位置”，真正要移动的是 `VeinData.pos`，不是只改 `VeinGroup.pos`。

## 默认矿物生成逻辑

默认实现位于：

- `Assembly-CSharp/PlanetAlgorithm.cs:38-431`

这段逻辑可以拆成 8 个阶段。

### 1. 从主题读取基础矿物参数

先从 `ThemeProto` 里拷贝三组参数：

- `ThemeProto.VeinSpot` -> 每种矿的大致矿组数量
- `ThemeProto.VeinCount` -> 每个矿组里大致有多少个矿点
- `ThemeProto.VeinOpacity` -> 每个矿点的矿量系数

注意：

虽然字段名叫 `VeinOpacity`，但在 `GenerateVeins()` 里它实际上被拿来参与 `amount` 计算，不只是“显示浓度”。

源码位置：

- `Assembly-CSharp/PlanetAlgorithm.cs:60-74`

### 2. 星系类型会改稀有矿生成概率

变量 `p` 会根据恒星类型和光谱调整：

- M 型主序星：`p = 2.5`
- G 型主序星：`p = 0.7`
- B 型主序星：`p = 0.4`
- 巨星：`p = 2.5`
- 白矮星：`p = 3.5`
- 中子星：`p = 4.5`
- 黑洞：`p = 5.0`

源码位置：

- `Assembly-CSharp/PlanetAlgorithm.cs:75-180`

这个 `p` 会进入稀有矿概率公式：

- `1f - Mathf.Pow(1f - baseChance, p)`

也就是：

- 越特殊的恒星，稀有矿越容易出现
- 不是简单加法，而是对原概率做幂函数放大

### 3. 特殊恒星直接追加某些稀有矿

除了概率放大，部分恒星还会直接增加特定矿种的矿组数：

- 白矮星额外偏向 `Diamond`、`Fractal`、`Grat`
- 中子星额外偏向 `Mag`
- 黑洞额外偏向 `Mag`

源码位置：

- `Assembly-CSharp/PlanetAlgorithm.cs:108-179`

### 4. 主题稀有矿表参与最终矿种集合

`themeProto.RareVeins` 和 `themeProto.RareSettings` 决定主题允许哪些稀有矿出现。

一旦通过概率判定：

- `array[num2]++` 增加该矿的矿组数
- `array2[num2]` 和 `array3[num2]` 同时被覆盖成对应的稀有矿参数

源码位置：

- `Assembly-CSharp/PlanetAlgorithm.cs:181-206`

### 5. 出生星有一套特殊照顾逻辑

如果当前星球是出生星：

- 先执行 `planet.GenBirthPoints(data, birthSeed)`
- `resourceCoef` 额外乘以 `2f / 3f`
- 强制插入两个预设矿组中心：
  - `birthResourcePoint0` -> 铁矿
  - `birthResourcePoint1` -> 铜矿

源码位置：

- `Assembly-CSharp/PlanetAlgorithm.cs:207-255`
- `Assembly-CSharp/PlanetData.cs:962-1023`

也就是说，新手星开局附近的铁铜，不是普通随机结果，而是专门塞进去的。

`GenBirthPoints()` 的逻辑本身也很重要：

- 它先找一个适合降落和出生的区域 `birthPoint`
- 再在附近偏移出两个资源点 `birthResourcePoint0/1`
- 并检查这些点以及周边小范围都高于地表/海面要求

这保证了开局资源不会刷在坏地形里。

### 6. 先生成“矿组中心”，不是直接撒单个矿点

源码里先生成的是 `veinVectors` 和 `veinVectorTypes`，可以理解成：

- 每个元素先代表一个“矿组中心”
- 每个中心关联一种 `EVeinType`

生成方式：

- 先根据随机方向找候选点
- 非油矿会把候选方向向 `birthPoint` / `veinBiasVector` 偏一点
- 用 `QueryHeight()` 检查地形高度是否合法
- 再检查和已有矿组中心的间距是否足够

关键限制：

- 最多尝试 200 次找合法中心
- 油矿和普通矿的最小间距常量不同
  - 油矿用 `100`
  - 普通矿用 `196`

源码位置：

- `Assembly-CSharp/PlanetAlgorithm.cs:229-315`

这里非常关键：

`planet.veinBiasVector` 会让非油矿整体朝一个偏置方向分布，而不是完全均匀覆盖整颗星球。

### 7. 再从矿组中心扩展出多个矿点

确定一个矿组中心后，代码会在一个 2D 局部平面上扩展 `tmp_vecs`：

- 初始先放 `(0, 0)`
- 再不断向周围长出新点
- 新点之间不能太近
- 最终数量大致由 `VeinCount * Random(20..25)` 决定

特殊情况：

- 油井强制 `num19 = 1`
- 出生星前两个矿组强制 `num19 = 6`

源码位置：

- `Assembly-CSharp/PlanetAlgorithm.cs:319-377`

所以普通矿组是“多矿点簇”，油是“单点簇”。

### 8. 为每个矿点写入最终矿量和位置

每个局部点最后会变成一个 `VeinData`：

- `type`
- `groupIndex = num17 + 1`
- `modelIndex`
- `amount`
- `productId`
- `pos`

矿量核心公式：

- 先取 `num20 * 100000f * num25`
- 然后再乘资源倍率

其中：

- 普通矿受 `DSPGame.GameDesc.resourceMultiplier` 影响
- 油受 `DSPGame.GameDesc.oilAmountMultiplier` 影响
- 无限资源模式下，非油矿直接变成 `1000000000`

源码位置：

- `Assembly-CSharp/PlanetAlgorithm.cs:378-427`

油矿的特殊点：

- 只生成 1 个矿点
- 位置要先经过 `planet.aux.RawSnap(...)`
- 后续显示和产量常常把 `amount * VeinData.oilSpeedMultiplier` 当作每秒产出

源码位置：

- `Assembly-CSharp/PlanetAlgorithm.cs:379-419`
- `Assembly-CSharp/VeinData.cs:48`

最后还有两步很实际：

- `data.EraseVegetableAtPoint(vein.pos)`：清掉该点植被
- 如果该点落在水下，并且星球有海洋，则不加入矿脉池

源码位置：

- `Assembly-CSharp/PlanetAlgorithm.cs:421-427`

## 特殊算法分支

虽然很多星球共用基类逻辑，但至少 7、11、12、13 号算法自己重写了矿物生成。

这些重写版的主体框架和基类很像，但会改“矿组中心的合法条件”。

### `PlanetAlgorithm11`

它会按高度限制基础矿和高级矿的落点：

- 铁/铜不能太高
- 硅/钛反而要求更高地形

关键判断：

- `((int)eVeinType <= 2 && num14 > planet.radius + 0.7f)`
- `((eVeinType == EVeinType.Silicium || eVeinType == EVeinType.Titanium) && num14 <= planet.radius + 0.7f)`

源码位置：

- `Assembly-CSharp/PlanetAlgorithm11.cs:479-481`

这说明该算法对应的星球，是用“海拔分层”来分隔矿种位置的。

### `PlanetAlgorithm12`

它对 `Fireice` 有更高的地形要求：

- `Fireice` 必须高于 `planet.radius + 1.2f`

源码位置：

- `Assembly-CSharp/PlanetAlgorithm12.cs:315-317`

### `PlanetAlgorithm13`

它限制前几种基础矿不能刷到太高的位置：

- `((int)eVeinType <= 4 && num14 > planet.radius + 0.7f)`

源码位置：

- `Assembly-CSharp/PlanetAlgorithm13.cs:560-562`

### `PlanetAlgorithm7`

它是另一套完全重写的矿物生成逻辑，至少可以确认两点：

1. 出生星时不再沿用基类那套固定铁铜资源点插入方式。
2. `Bamboo` 有特殊地形限制，过高位置会被跳过。

关键判断：

- `if (eVeinType == EVeinType.Bamboo && data.QueryHeight(zero) > planet.realRadius - 4f)`

源码位置：

- `Assembly-CSharp/PlanetAlgorithm7.cs:426-463`
- `Assembly-CSharp/PlanetAlgorithm7.cs:491-493`

如果后续要做“所有星球矿物都统一重排”的 mod，不能只 patch 基类 `PlanetAlgorithm.GenerateVeins()`，还得检查这些重写类。

## 矿物是如何进入工厂运行时的

`PlanetFactory` 在初始化时会：

1. 确保 `planetData.RegenerateRawDataImmediately()` 已经跑过
2. 把 `planet.data.veinPool` 复制到 `factory.veinPool`
3. 初始化 hash 和矿组

源码位置：

- `Assembly-CSharp/PlanetFactory.cs:312-329`

之后在工厂加载的第 2 阶段，才为这些矿脉创建：

- 模型
- 动画数据
- 碰撞体
- 采矿覆盖显示

源码位置：

- `Assembly-CSharp/PlanetModelingManager.cs:284-329`

因此：

- `PlanetRawData` / `PlanetData` 阶段决定“矿在哪里、多少、属于哪组”
- `PlanetFactory` 阶段只是把它们变成可见、可交互对象

## 对“更好的矿物位置”mod 的直接启发

### 最推荐的改法

最稳的方案，是在矿物还停留在 `PlanetData.data.veinPool` 时就调整位置。

原因：

- 这时还没创建模型和碰撞
- 改完后只要重新 `SummarizeVeinGroups()`
- 不需要处理 `factory` 里的显示、碰撞、哈希、矿机覆盖更新

### 推荐拦截点

#### 方案 A：直接 patch `GenerateVeins()`

适合：

- 你想重写矿组中心选点规则
- 你想控制矿和出生点/赤道/两极/海岸线的关系

建议入口：

- `PlanetAlgorithm.GenerateVeins()`
- `PlanetAlgorithm7.GenerateVeins()`
- `PlanetAlgorithm11.GenerateVeins()`
- `PlanetAlgorithm12.GenerateVeins()`
- `PlanetAlgorithm13.GenerateVeins()`

#### 方案 B：在 `GenerateVeins()` 之后重排 `veinPool`

适合：

- 你不想重写原版矿种概率
- 只想把已生成矿点重新排布得更合理

建议流程：

1. 等原版 `GenerateVeins()` 跑完
2. 遍历 `planet.data.veinPool`
3. 修改 `VeinData.pos`
4. 必要时重设 `groupIndex`
5. 调用 `planet.SummarizeVeinGroups()`

这是我更推荐的第一版 mod 切入点。

### 不太推荐的改法

直接在 `PlanetFactory` 已加载后再改矿位，会连带处理很多额外问题：

- `factory.veinPool`
- hash bucket
- collider
- GPU 模型位置
- 矿组统计
- 已放置矿机的覆盖关系

源码里确实有重算矿组和刷新显示的方法，但运行时改动成本更高。

## 对这个 mod 的建议实现方向

如果 mod 名字叫“更好的矿物位置”，我建议把目标收窄成下面三种之一：

1. 保持原版矿种与矿量不变，只优化矿组的地形可达性和集中度。
2. 保持原版稀有矿概率不变，但让同类矿更容易形成适合摆矿机的形状。
3. 保持出生星资源保底逻辑不变，只优化非出生星的矿组布局。

原因很直接：

- 原版已经把“矿种出现概率”和“特殊恒星奖励”写得很重
- 如果第一版同时改概率、数量、位置，后面很难判断问题出在分布逻辑还是平衡逻辑

## 最有价值的源码入口总结

- 行星重建入口：`Assembly-CSharp/PlanetData.cs:1454-1474`
- 默认矿物生成：`Assembly-CSharp/PlanetAlgorithm.cs:38-431`
- 出生点与出生资源点：`Assembly-CSharp/PlanetData.cs:944-1023`
- 矿组汇总：`Assembly-CSharp/PlanetData.cs:862-903`
- 主题决定算法：`Assembly-CSharp/PlanetGen.cs:397-404`
- 算法类分派：`Assembly-CSharp/PlanetModelingManager.cs:702-724`
- 工厂复制矿脉数据：`Assembly-CSharp/PlanetFactory.cs:312-329`
- 工厂创建矿物模型与碰撞：`Assembly-CSharp/PlanetModelingManager.cs:284-329`

## 一句话版结论

矿物位置的真正源头是 `PlanetAlgorithm.GenerateVeins()` 写入 `PlanetRawData.veinPool` 的那一刻。

“更好的矿物位置”这个 mod，最佳切入点不是矿机，不是 UI，也不是工厂加载后的显示，而是行星原始矿脉生成或其后立即重排的阶段。
