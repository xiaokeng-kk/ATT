using System.Reflection;
using ATT.Core.Interfaces;

namespace ATT.Core;

/// <summary>
/// 组件目录 — 负责组件的注册、发现和按类型查询
/// 参考 OpenTAP 的 PluginManager（组件扫描 + 按基类查找）
/// </summary>
public class ComponentCatalog
{
    private readonly List<Type> _componentTypes = [];

    /// <summary>所有已注册的组件类型</summary>
    public IReadOnlyList<Type> ComponentTypes => _componentTypes.AsReadOnly();

    /// <summary>组件注册变更事件</summary>
    public event Action? CatalogChanged;

    // ==================== 注册 ====================

    /// <summary>
    /// 手动注册一个组件类型
    /// </summary>
    public void Register<T>() where T : IComponent
    {
        Register(typeof(T));
    }

    /// <summary>
    /// 手动注册一个组件类型
    /// </summary>
    public void Register(Type type)
    {
        if (!typeof(IComponent).IsAssignableFrom(type))
            throw new ArgumentException($"类型 {type.Name} 未实现 IComponent");

        if (!_componentTypes.Contains(type))
        {
            _componentTypes.Add(type);
            CatalogChanged?.Invoke();
        }
    }

    /// <summary>
    /// 扫描指定程序集，注册所有实现了 IComponent 的类型
    /// </summary>
    public void ScanAssembly(Assembly assembly)
    {
        var types = assembly.GetExportedTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false }
                        && typeof(IComponent).IsAssignableFrom(t));

        bool changed = false;
        foreach (var type in types)
        {
            if (!_componentTypes.Contains(type))
            {
                _componentTypes.Add(type);
                changed = true;
            }
        }

        if (changed)
            CatalogChanged?.Invoke();
    }

    /// <summary>
    /// 扫描指定目录下所有 DLL，注册组件
    /// 参考 OpenTAP PluginManager — 扫描目录下所有程序集
    /// </summary>
    public void ScanDirectory(string directoryPath)
    {
        if (!Directory.Exists(directoryPath)) return;

        foreach (var dll in Directory.GetFiles(directoryPath, "*.dll"))
        {
            try
            {
                var assembly = Assembly.LoadFrom(dll);
                ScanAssembly(assembly);
            }
            catch
            {
                // 跳过无法加载的程序集
            }
        }
    }

    // ==================== 查询 ====================

    /// <summary>
    /// 获取所有实现指定基类的组件类型
    /// 参考 OpenTAP 的 GetPlugins&lt;T&gt;()
    /// </summary>
    public IEnumerable<Type> GetComponents<T>() where T : IComponent
    {
        return _componentTypes.Where(t => typeof(T).IsAssignableFrom(t));
    }

    /// <summary>
    /// 获取所有实现指定基类的组件类型
    /// </summary>
    public IEnumerable<Type> GetComponents(Type baseType)
    {
        if (!typeof(IComponent).IsAssignableFrom(baseType))
            throw new ArgumentException($"类型 {baseType.Name} 未实现 IComponent");

        return _componentTypes.Where(baseType.IsAssignableFrom);
    }

    /// <summary>
    /// 创建指定类型的实例
    /// </summary>
    public T CreateInstance<T>(Type type) where T : IComponent
    {
        return (T)Activator.CreateInstance(type)!;
    }

    /// <summary>
    /// 清空所有注册
    /// </summary>
    public void Clear()
    {
        _componentTypes.Clear();
        CatalogChanged?.Invoke();
    }
}
