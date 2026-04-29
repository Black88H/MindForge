using System;
namespace MindForge.Models;
public class Achievement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string IconKey { get; set; } = string.Empty;
    public string Requirement { get; set; } = string.Empty;
    public int XPReward { get; set; } = 0;
}
