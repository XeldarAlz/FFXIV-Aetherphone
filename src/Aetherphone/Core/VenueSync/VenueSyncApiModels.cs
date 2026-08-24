using System.Text.Json.Serialization;

namespace Aetherphone.Core.VenueSync;

internal sealed class VenueSyncShift
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("scheduledStart")] public string ScheduledStart { get; set; } = "";
    [JsonPropertyName("scheduledEnd")] public string ScheduledEnd { get; set; } = "";
    [JsonPropertyName("actualStart")] public string? ActualStart { get; set; }
    [JsonPropertyName("actualEnd")] public string? ActualEnd { get; set; }
    [JsonPropertyName("status")] public string Status { get; set; } = "";
    [JsonPropertyName("notes")] public string? Notes { get; set; }
}

internal sealed class VenueSyncOpenShift
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("scheduledStart")] public string ScheduledStart { get; set; } = "";
    [JsonPropertyName("scheduledEnd")] public string ScheduledEnd { get; set; } = "";
    [JsonPropertyName("roleName")] public string? RoleName { get; set; }
}

internal sealed class VenueSyncShiftsResponse
{
    [JsonPropertyName("shifts")] public List<VenueSyncShift> Shifts { get; set; } = new();
    [JsonPropertyName("openShifts")] public List<VenueSyncOpenShift> OpenShifts { get; set; } = new();
}

internal sealed class VenueSyncClockResult
{
    [JsonPropertyName("success")] public bool Success { get; set; }
    [JsonPropertyName("error")] public string? Error { get; set; }
    [JsonPropertyName("status")] public string? Status { get; set; }
    [JsonPropertyName("hoursWorked")] public double? HoursWorked { get; set; }
}

internal sealed class VenueSyncShiftIdRequest
{
    [JsonPropertyName("shiftId")] public string ShiftId { get; set; } = "";
}

internal sealed class VenueSyncService
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("price")] public string Price { get; set; } = "";
    [JsonPropertyName("category")] public string? Category { get; set; }
}

internal sealed class VenueSyncServicesResponse
{
    [JsonPropertyName("services")] public List<VenueSyncService> Services { get; set; } = new();
    [JsonPropertyName("userRole")] public string? UserRole { get; set; }
}

internal sealed class VenueSyncTransactionRequest
{
    [JsonPropertyName("venueId")] public string VenueId { get; set; } = "";

    [JsonPropertyName("serviceId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ServiceId { get; set; }

    [JsonPropertyName("amount")] public decimal Amount { get; set; }

    [JsonPropertyName("type")] public string Type { get; set; } = "SALE";

    [JsonPropertyName("customerName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CustomerName { get; set; }

    [JsonPropertyName("notes")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Notes { get; set; }
}

internal sealed class VenueSyncTransactionResult
{
    [JsonPropertyName("success")] public bool Success { get; set; }
    [JsonPropertyName("error")] public string? Error { get; set; }
}

internal sealed class VenueSyncVenue
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("slug")] public string Slug { get; set; } = "";
    [JsonPropertyName("role")] public string Role { get; set; } = "";
}

internal sealed class VenueSyncVenuesResponse
{
    [JsonPropertyName("venues")] public List<VenueSyncVenue> Venues { get; set; } = new();
}

internal sealed class VenueSyncLinkCharacterRequest
{
    [JsonPropertyName("characterName")] public string CharacterName { get; set; } = "";
    [JsonPropertyName("world")] public string World { get; set; } = "";
}

internal sealed class VenueSyncCharacter
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("characterName")] public string CharacterName { get; set; } = "";
    [JsonPropertyName("world")] public string World { get; set; } = "";
    [JsonPropertyName("isPrimary")] public bool IsPrimary { get; set; }
}

internal sealed class VenueSyncLinkCharacterResponse
{
    [JsonPropertyName("character")] public VenueSyncCharacter? Character { get; set; }
    [JsonPropertyName("error")] public string? Error { get; set; }
}

internal sealed class VenueSyncPatronVisitRequest
{
    [JsonPropertyName("venueId")] public string VenueId { get; set; } = "";
    [JsonPropertyName("characterName")] public string CharacterName { get; set; } = "";
    [JsonPropertyName("world")] public string World { get; set; } = "";
    [JsonPropertyName("action")] public string Action { get; set; } = "";
    [JsonPropertyName("timestamp")] public string Timestamp { get; set; } = "";
}

internal sealed class VenueSyncActiveEventResponse
{
    [JsonPropertyName("active")] public bool Active { get; set; }
    [JsonPropertyName("eventId")] public string? EventId { get; set; }
}

internal sealed class VenueSyncPatronVisitResult
{
    [JsonPropertyName("success")] public bool Success { get; set; }
    [JsonPropertyName("error")] public string? Error { get; set; }
}
