namespace ATT.Core.Attributes;

/// <summary>
/// 显示特性 — 为组件或属性提供 UI 标签、分组、排序
/// 参考 OpenTAP 的 [Display(Name, Group, Order)] 模式
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Interface, AllowMultiple = false)]
public class DisplayAttribute : Attribute
{
    /// <summary>显示名称</summary>
    public string Name { get; }

    /// <summary>描述</summary>
    public string Description { get; }

    /// <summary>分组路径，如 ["VISA", "Locking"]</summary>
    public string[] Groups { get; }

    /// <summary>在同组中的排序顺序</summary>
    public double Order { get; set; }

    public DisplayAttribute(string name, string description = "")
    {
        Name = name;
        Description = description;
        Groups = [];
    }

    public DisplayAttribute(string name, string description, params string[] groups)
    {
        Name = name;
        Description = description;
        Groups = groups ?? [];
    }
}
