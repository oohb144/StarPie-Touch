namespace StarPie.Plugin;

/// <summary>目标进程的启动权限；不改变宿主自身的权限。</summary>
public enum ProcessLaunchMode
{
    /// <summary>沿用原有启动行为。</summary>
    Default = 0,
    /// <summary>请求管理员启动，取消或失败时不回退。</summary>
    Administrator = 1,
    /// <summary>通过普通用户 Shell 启动，失败时不回退。</summary>
    StandardUser = 2,
}
