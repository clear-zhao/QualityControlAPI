using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace QualityControlAPI.Models
{
    // ... User, Tool, Specs 保持不变，重点改 Order ...

    [Table("Users")]
    public class User
    {
        [Key][Column("Id")] public int Id { get; set; }
        [Column("Name")] public required string Name { get; set; }
        [Column("EmployeeId")] public required string EmployeeId { get; set; }
        [Column("Password")] public required string Password { get; set; }
        [Column("Role")] public int Role { get; set; }
    }

    [Table("CrimpingTools")]
    public class CrimpingTool
    {
        [Key][Column("Id")] public int Id { get; set; }
        [Column("Model")] public required string Model { get; set; }
        [Column("Type")] public required string Type { get; set; }
    }

    [Table("TerminalSpecs")]
    public class TerminalSpec
    {
        [Key][Column("Id")] public int Id { get; set; }
        [Column("MaterialCode")] public required string MaterialCode { get; set; }
        [Column("Name")] public required string Name { get; set; }
        [Column("Description")] public string? Description { get; set; }
        [Column("Method")] public int Method { get; set; }
    }

    [Table("WireSpecs")]
    public class WireSpec
    {
        [Key][Column("Id")] public required string Id { get; set; }
        [Column("DisplayName")] public required string DisplayName { get; set; }
        [Column("SectionArea")] public decimal SectionArea { get; set; }
    }

    [Table("PullForceStandards")]
    public class PullForceStandard
    {
        [Key][Column("Id")] public int Id { get; set; }
        [Column("Method")] public int Method { get; set; }
        [Column("SectionArea")] public decimal SectionArea { get; set; }
        [Column("StandardValue")] public int StandardValue { get; set; }
    }

    // --- 重点修改：完全匹配前端 JSON 结构 ---
    [Table("ProductionOrders")]
    public class ProductionOrder
    {
        [Key]
        [Column("Id")]
        public required string Id { get; set; }

        [Column("ProductionOrderNo")]
        public required string ProductionOrderNo { get; set; }

        [Column("ProductName")] public string? ProductName { get; set; }
        [Column("ProductModel")] public string? ProductModel { get; set; }
        [Column("ToolNo")] public string? ToolNo { get; set; }
        [Column("TerminalSpecId")] public string? TerminalSpecId { get; set; }
        [Column("WireSpecId")] public string? WireSpecId { get; set; }
        [Column("StandardPullForce")] public decimal? StandardPullForce { get; set; }
        [Column("CreatorName")] public string? CreatorName { get; set; }

        // --- 新增字段 ---
        [Column("IsClosed")]
        public bool IsClosed { get; set; } = false;

        [Column("CreatedAt")] public DateTime CreatedAt { get; set; } = DateTime.Now;

        public List<InspectionRecord> Records { get; set; } = new();
    }

    [Table("InspectionRecords")]
    public class InspectionRecord
    {
        [Key]
        [Column("Id")]
        public required string Id { get; set; } // 前端生成的字符串 ID

        [Column("OrderId")] public string? OrderId { get; set; }
        [JsonIgnore][ForeignKey("OrderId")] public ProductionOrder? Order { get; set; }

        [Column("Type")] public string? Type { get; set; }
        [Column("SubmitterName")] public string? SubmitterName { get; set; }
        [Column("SubmittedAt")] public DateTime? SubmittedAt { get; set; }

        [Column("Status")] public int Status { get; set; } // 0/1/2

        [Column("AuditorName")] public string? AuditorName { get; set; }
        [Column("AuditedAt")] public DateTime? AuditedAt { get; set; }
        [Column("AuditNote")] public string? AuditNote { get; set; }

        public List<TerminalSample> Samples { get; set; } = new();
    }

    [Table("TerminalSamples")]
    public class TerminalSample
    {
        [Key][Column("Id")] public int Id { get; set; }

        [Column("InspectionRecordId")] public string? InspectionRecordId { get; set; }
        [JsonIgnore][ForeignKey("InspectionRecordId")] public InspectionRecord? InspectionRecord { get; set; }

        [Column("SampleIndex")] public int SampleIndex { get; set; } // 1,2,3
        [Column("MeasuredForce")] public decimal? MeasuredForce { get; set; }
        [Column("IsPassed")] public bool? IsPassed { get; set; }
    }

    public class RecordAuditDto
    {
        public int Status { get; set; } // 1: 合格, 2: 不合格
        public string AuditorName { get; set; }
        public string? AuditNote { get; set; }
        public List<SampleUpdateDto>? Samples { get; set; }
    }

    public class SampleUpdateDto
    {
        public int SampleIndex { get; set; }
        public decimal MeasuredForce { get; set; }
        public bool IsPassed { get; set; }
    }
}