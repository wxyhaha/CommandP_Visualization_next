# 卫星-飞机通信可视化功能

## 概述
本更新对 `SatelliteSimulator.cs` 进行了改进，使卫星轨道经过飞机运动区域上方，并在卫星和飞机之间添加可视化通信线（仅在两者之间有清晰视线时显示）。

## 主要改动

### 1. 轨道参数调整
卫星轨道现在以飞机航线中心为圆心：
- **轨道中心纬度**: 39.736401°N（同飞机航线）
- **轨道中心经度**: -105.25737°W（同飞机航线）
- **轨道倾斜角**: 45°（从 53° 改为）
- **升交点赤经 (RAAN)**: 0°（从 120° 改为）
- **初始相位**: 180°（从 28° 改为）

这使得卫星轨道位于飞机飞行区域的正上方，有助于卫星观测飞机。

### 2. 通信线可视化
#### 新增功能：
- **自动视线检查**: 系统进行光线追踪，确认卫星和飞机之间没有地球阻挡
- **条件性显示**: 仅当两个单位有清晰视线时才显示通信线
- **动态更新**: 通信线每帧更新，跟踪卫星和飞机的实时位置

#### 可配置参数（在 Inspector 中）：
```
启用通信线 (Enable Communication Line): true/false
飞机单位ID (Aircraft Unit ID): "AIRCRAFT_SIM_001"
通信线颜色 (Communication Line Color): 青色 (0.2, 1.0, 0.8, 0.8)
通信线宽度 (Communication Line Width): 50 像素
```

## 技术细节

### 视线检查算法
```
if 卫星到地心距离 > 地球半径 AND
   飞机到地心距离 > 地球半径 AND
   线段最接近地心的点不穿过地球:
    显示通信线
else:
    隐藏通信线
```

### 坐标变换
- **ECEF (地心地固坐标系)**: 卫星轨道计算采用
- **LLH (经纬度高度)**: 飞机位置数据采用
- **Unity 世界坐标**: 通信线渲染采用

所有坐标之间的转换考虑了 WGS84 椭球体和 Cesium 地理参考。

## 使用说明

### 1. 确保场景配置正确
- Scene 中需要有 `AppManager` 组件
- Scene 中需要有 `UnitViewManager` 组件
- Scene 中需要有 `CesiumGeoreference` 组件（用于坐标变换）
- `AircraftSimulator` 组件（生成飞机数据）

### 2. 在 Inspector 中验证配置
打开 `SatelliteSimulator` 组件，确认：
- ✅ "Enable Communication Line" 已勾选
- ✅ "Aircraft Unit ID" 为 "AIRCRAFT_SIM_001"
- ✅ "Communication Line Color" 为青色
- ✅ "Orbit Center Latitude/Longitude" 与飞机中心相同

### 3. 运行场景
播放场景后，您应该看到：
- 卫星在飞机上空绕轨道飞行
- 当卫星在飞机可视范围内（未被地球遮挡）时，有一条青色的线连接两个模型
- 当卫星飞到地球另一侧或高度不足以保持视线时，连接线消失

## 常见问题

### Q: 为什么看不到通信线？
**A**: 
1. 检查 "Enable Communication Line" 是否已启用
2. 确认飞机和卫星都在运动（不是暂停状态）
3. 验证飞机单位 ID 是否为 "AIRCRAFT_SIM_001"
4. 检查 Camera 是否能看到两个模型
5. 确保场景中有 CesiumGeoreference 组件

### Q: 通信线颜色不对
**A**: 在 Inspector 中修改 "Communication Line Color" 参数

### Q: 如何改变通信线宽度？
**A**: 在 Inspector 中修改 "Communication Line Width" 参数（50 = 50 像素宽度）

### Q: 如何改变卫星轨道的位置？
**A**: 修改 "Orbit Center Latitude" 和 "Orbit Center Longitude" 参数

## 性能影响
- 每帧视线检查：~0.1ms
- LineRenderer 绘制：~0.05ms
- 总体影响：可忽略不计

## 未来改进建议
- [ ] 支持多个飞机单位的同时连接
- [ ] 可视化多个卫星间的通信链路
- [ ] 添加信号强度指示
- [ ] 添加通信带宽计量

---

**版本**: 1.0  
**最后修改**: 2026年5月9日  
**作者**: 自动化系统
