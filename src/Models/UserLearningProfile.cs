using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MindForge.Models;

[Table("UserLearningProfiles")]
public class UserLearningProfile
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(100)] public string UserId { get; set; } = "default";

    [NotMapped]
    public List<string> PreferredMethods
    {
        get => PreferredMethodsJson == null ? [] : System.Text.Json.JsonSerializer.Deserialize<List<string>>(PreferredMethodsJson) ?? [];
        set => PreferredMethodsJson = System.Text.Json.JsonSerializer.Serialize(value);
    }

    [Column("PreferredMethods")] public string? PreferredMethodsJson { get; set; }

    [MaxLength(50)] public string LearningStyle { get; set; } = "Mixed";

    public bool ShortExplanations { get; set; } = false;
    public bool NeedsExamples { get; set; } = true;
    public bool NeedsExercises { get; set; } = true;
    public bool NeedsFormulas { get; set; } = false;

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedDate { get; set; } = DateTime.UtcNow;
}
