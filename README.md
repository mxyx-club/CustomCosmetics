# CustomCosmetics

一个独立的帽子下载插件，从[超多职业](https://github.com/TheOtherRolesAU/TheOtherRoles)独立并优化而来。

### 使用方法：

安装插件后启动一次游戏生成配置文件

可以在 **/BepinEx/config** 中找到 **com.mxyx.cosmetics.cfg**

打开并编辑配置文件：

```

## Settings file was created by plugin CosmeticsManager v1.0.0
## Plugin GUID: com.mxyx.cosmetics

[Custom]

# 是否只读取本地帽子文件，此项为true时不会进行任何下载行为，仅读取配置文件
Local Hats = false

[CustomHats]

# 此项是帽子下载源，可自行更换，默认是超多职业本家帽子源
Repository Source = https://raw.githubusercontent.com/TheOtherRolesAU/TheOtherHats/master

```

每次修改都需要重启游戏。

兼容版本：详看[Releases](https://github.com/mxyx-club/CustomCosmetics/releases)
