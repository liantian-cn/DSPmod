# 更好的矿物位置

英文目录名：`VeinPlacement`

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

### 6.1 原版如何控制矿组之间的距离

如果只看“不同矿簇彼此别靠太近”这一层，原版用的是矿组中心之间的最小距离判定：

```csharp
float num15 = ((eVeinType == EVeinType.Oil) ? 100f : 196f);
if ((veinVectors[num16] - zero).sqrMagnitude < num * num * num15)
```

源码位置：

- `Assembly-CSharp/PlanetAlgorithm.cs:288-293`

含义是：

- 普通矿组中心的距离平方阈值是 `196`
- 油矿组中心的距离平方阈值是 `100`
- 最终还会乘 `num * num`
- 而 `num = 2.1f / planet.radius`

所以这个距离不是固定世界长度，而是会随星球半径缩放。

对常见半径 `200` 的普通行星来说：

- `num = 2.1 / 200 = 0.0105`
- 普通矿组中心最小距离约等于 `sqrt(196) * 0.0105 = 0.147`
- 油矿组中心最小距离约等于 `sqrt(100) * 0.0105 = 0.105`

如果以后要让“不同矿簇之间更近”，最直接改的就是这里：

- 把普通矿的 `196` 改小
- 把油矿的 `100` 改小

`PlanetAlgorithm11/12/13` 这几个特殊算法也沿用了同样的距离常量：

- `Assembly-CSharp/PlanetAlgorithm11.cs:484-489`
- `Assembly-CSharp/PlanetAlgorithm12.cs:320-325`
- `Assembly-CSharp/PlanetAlgorithm13.cs:565-570`

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

### 7.1 原版如何控制同一矿组内部的距离

矿组内部不是完全随机撒点，而是通过两个常量控制“能长多散”。

#### 组内矿点最小距离

新点加入前会先检查和已有点的距离：

```csharp
if ((tmp_vecs[num24] - vector4).sqrMagnitude < 0.85f)
```

源码位置：

- `Assembly-CSharp/PlanetAlgorithm.cs:359-367`

这表示：

- 同一矿组内部，两个局部点不能太近
- 阈值是距离平方 `< 0.85`

换算到半径 `200` 的普通行星：

- 组内最小局部距离约等于 `sqrt(0.85) * (2.1 / 200) = 0.00968`

如果以后要让“同一簇里的矿更挤”，最直接就是把 `0.85` 改小。

#### 矿组扩展半径上限

原版还限制了从组中心向外扩展的最大范围：

```csharp
if (tmp_vecs[num22].sqrMagnitude > 36f)
{
    continue;
}
```

源码位置：

- `Assembly-CSharp/PlanetAlgorithm.cs:350-353`

这表示：

- 局部点超过一定半径后，不再继续从该点往外长
- 这个半径平方阈值是 `36`

换算到半径 `200` 的普通行星：

- 最大局部扩展半径约等于 `sqrt(36) * (2.1 / 200) = 0.063`

如果以后要让整个矿簇更紧凑，可以把 `36` 也改小。

所以“原版让矿更靠近”的三个核心距离参数，其实就是：

- `196`：普通矿组中心之间的最小距离平方阈值
- `100`：油矿组中心之间的最小距离平方阈值
- `0.85`：同一矿组内部矿点之间的最小距离平方阈值
- `36`：同一矿组内部扩展半径平方上限

对于你的 mod 来说，这一节很重要，因为你当前路线不是改这些原版距离常量，而是让原版先照常生成，再整体搬到目标纬度带。

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

你的目标现在可以明确收敛成一句话：

不改原版矿物生成数量、不改原版矿种分布概率，只在原版 `GenerateVeins()` 完成之后，把已经生成好的矿点移动到一个指定纬度范围。

这意味着，mod 最好不要接管原版生成流程本身，而是做一个“生成后重排位置”的后处理层。

### 最推荐的改法

最稳的方案，是在矿物还停留在 `PlanetData.data.veinPool` 时就调整位置，但前提是：

- 原版 `GenerateVeins()` 已经完整执行完
- 你不再调用任何原版随机生成逻辑
- 你只改 `VeinData.pos`
- 你尽量不改 `VeinData.amount`
- 你尽量不改矿点总数

原因：

- 这时原版已经把“矿种、矿量、矿点数量、groupIndex”都算好了
- 这时还没创建模型和碰撞
- 改完后只要重新 `SummarizeVeinGroups()`
- 不需要处理 `factory` 里的显示、碰撞、哈希、矿机覆盖更新

### 推荐拦截点

最符合你当前思路的，不是“重写生成”，而是“生成后搬运”。

建议入口：

- `PlanetAlgorithm.GenerateVeins()` 的 postfix
- `PlanetAlgorithm7.GenerateVeins()` 的 postfix
- `PlanetAlgorithm11.GenerateVeins()` 的 postfix
- `PlanetAlgorithm12.GenerateVeins()` 的 postfix
- `PlanetAlgorithm13.GenerateVeins()` 的 postfix

原因很直接：

- 这些方法是原版矿物真正落入 `PlanetRawData.veinPool` 的位置
- postfix 能保证原版随机生成先完整跑完
- 你后面做的是确定性的坐标重映射，而不是再参与原版随机抽样

### 你这个 mod 的建议流程

建议流程应该改成下面这样：

1. 等原版 `GenerateVeins()` 跑完
2. 遍历 `planet.data.veinPool`
3. 保持每个 `VeinData.type`、`amount`、`productId` 不变
4. 按你的目标纬度范围，重新计算 `VeinData.pos`
5. 尽量保留原有 `groupIndex`
6. 调用 `planet.SummarizeVeinGroups()`

这里最重要的是第 3 条：

你这个 mod 的本质，不应该是“重新生成矿”，而应该是“搬运已经生成好的矿”。

### 纬度搬运时建议保留的东西

为了保证“原版数量不变”，后处理时最好保留这些字段：

- `VeinData.type`
- `VeinData.amount`
- `VeinData.productId`
- `VeinData.modelIndex`
- `VeinData.groupIndex`
- `data.veinCursor`

真正应该动的，主要只有：

- `VeinData.pos`

如果你后面想让一个矿组整体搬到同一纬度带，最理想的做法是：

- 先按 `groupIndex` 分组
- 先决定每个矿组中心的新纬度
- 再把组内矿点相对中心的小范围形状一起平移/旋转过去

这样会比“逐点单独投影到纬度带”更接近原版矿组形状。

### 不太推荐的改法

直接在 `PlanetFactory` 已加载后再改矿位，会连带处理很多额外问题：

- `factory.veinPool`
- hash bucket
- collider
- GPU 模型位置
- 矿组统计
- 已放置矿机的覆盖关系

源码里确实有重算矿组和刷新显示的方法，但运行时改动成本更高，也更容易漏同步。

## 原版随机种子机制

你现在最关心的问题，其实是：

原版随机数是怎么来的，以及怎样在 mod 里不破坏这个机制。

答案是：原版是分层种子、局部重新初始化随机数状态的。

### 1. 银河级种子

银河生成从 `gameDesc.galaxySeed` 开始：

- `UniverseGen.CreateGalaxy(GameDesc gameDesc)`
- `DotNet35Random dotNet35Random = new DotNet35Random(galaxySeed)`

然后它不断 `Next()`，给每颗星分配自己的 seed。

源码位置：

- `Assembly-CSharp/UniverseGen.cs:30-43`
- `Assembly-CSharp/UniverseGen.cs:64-99`

### 2. 行星级种子

创建星球时，`PlanetGen.CreatePlanet(...)` 同时接收两个种子：

- `info_seed`
- `gen_seed`

然后写入：

- `planetData.infoSeed = info_seed`
- `planetData.seed = gen_seed`

源码位置：

- `Assembly-CSharp/PlanetGen.cs:23-31`

这两个种子用途不同：

- `info_seed` 更偏向轨道、名字、主题选择、样式等“行星信息”
- `gen_seed` 更偏向地形、植被、矿物等“行星内容生成”

### 3. 主题和算法选择也有自己的随机消费顺序

`PlanetGen.CreatePlanet()` 里先基于 `info_seed` 连续调用很多次 `NextDouble()` 和一次 `Next()`：

- 轨道
- 自转
- 潮汐锁定
- 主题选择
- `algoId`
- `mod_x / mod_y`
- `style`

源码位置：

- `Assembly-CSharp/PlanetGen.cs:61-78`
- `Assembly-CSharp/PlanetGen.cs:397-410`

这说明：

原版并不是“全局一个随机数对象到处传”，而是每个阶段各自拿 seed 重新开局，然后按固定顺序消费。

### 4. 矿物生成时，会重新从 `planet.seed` 开始

默认 `GenerateVeins()` 开头就是：

```csharp
DotNet35Random dotNet35Random = new DotNet35Random(planet.seed);
dotNet35Random.Next();
dotNet35Random.Next();
dotNet35Random.Next();
dotNet35Random.Next();
int birthSeed = dotNet35Random.Next();
DotNet35Random dotNet35Random2 = new DotNet35Random(dotNet35Random.Next());
```

源码位置：

- `Assembly-CSharp/PlanetAlgorithm.cs:47-53`

这个结构非常重要，说明：

- 每次矿物生成都会从同一个 `planet.seed` 重新开始
- 而且会先固定跳过几次 `Next()`
- 后面再拆出 `birthSeed` 和真正用于矿物分布的 `dotNet35Random2`

所以同一颗星球、同一算法、同一输入条件下，矿物结果是 deterministic 的。

### 5. `DotNet35Random` 本身是确定性的

源码里的 `DotNet35Random` 是一个固定实现的伪随机数类，只要种子和调用顺序相同，输出就相同。

源码位置：

- `Assembly-CSharp/DotNet35Random.cs:25-117`

所以真正需要保护的不是“随机对象实例”本身，而是：

- 初始 seed 不变
- 原版调用顺序不变
- 原版调用次数不变

## 如何不破坏这个随机机制

如果你的 mod 采用“原版生成后再移动位置”的方案，那么最安全的原则是：

### 原则 1：不要替换原版 `GenerateVeins()` 的随机流程

不要 prefix 里直接跳过原版，再自己重新生成。

因为一旦你接管了：

- 你就必须完整复制不同算法类的随机消费顺序
- 还必须保证未来任何版本差异都和原版一致

这没有必要。

### 原则 2：让原版先完整生成，再做后处理

也就是：

- 原版照常初始化 `DotNet35Random`
- 原版照常调用 `Next()/NextDouble()`
- 原版照常决定矿种、矿量、矿点数、groupIndex
- mod 在 postfix 里只读取结果并重排位置

这样你没有参与原版随机数消费，自然也就不会破坏原版 seed 机制。

### 原则 3：后处理最好做成确定性映射

你后面“搬到特定纬度范围”的算法，最好也不要使用无约束的新随机数。

更稳的做法是：

- 仅基于现有 `VeinData.pos`
- 仅基于 `planet.seed`
- 仅基于 `groupIndex`
- 仅基于目标纬度参数

做一个纯函数式映射。

例如：

- 先把当前点转成球坐标
- 保留经度
- 把纬度压缩或投影到目标区间
- 再按 `QueryHeight()` 或 `RawSnap()` 贴回地表

这样即使你自己做二次排布，它仍然是 deterministic 的。

### 原则 4：不要改变原版已经算好的矿点数量

对你这个 mod 来说，最安全的是：

- 不增删 `veinPool` 项
- 不改 `veinCursor`
- 不改 `amount`
- 不改 `type`

只搬位置。

这样“同一 seed 生成多少矿”这个原版结果被完整保留。

### 原则 5：油矿要单独小心

油矿在原版里有额外特殊处理：

- 单点矿组
- 位置经过 `planet.aux.RawSnap(...)`
- 显示和产量语义和普通矿不完全一样

所以如果你的纬度搬运包含油矿，最好在新位置重新做一次适合油矿的贴点处理，而不是完全沿用普通矿方式。

## 现在这个 mod 的更准确设计目标

基于你现在的要求，这个 mod 最准确的描述应该是：

1. 保留原版种子驱动下生成出的矿种、矿量、矿点数和矿组数。
2. 不接管原版随机生成逻辑，不修改原版随机数消费顺序。
3. 在 `GenerateVeins()` 结束后，对 `planet.data.veinPool` 做一次确定性的纬度重排。
4. 把矿点移动到指定纬度范围，同时重新汇总矿组。

这比“重写矿物生成”要小得多，也更稳。

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

矿物位置的真正源头仍然是 `PlanetAlgorithm.GenerateVeins()`，但你这个 mod 最适合做的不是重写它，而是在它执行完之后，保持原版 seed 结果不变，只把已生成矿点确定性地搬到目标纬度范围。
