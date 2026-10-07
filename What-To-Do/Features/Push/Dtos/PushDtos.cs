namespace What_To_Do.Features.Push.Dtos;

public class PushStatusResponse
{
    public bool Enabled { get; set; }
    public string? PublicKey { get; set; }
}

public class SubscribeRequest
{
    public string Endpoint { get; set; } = string.Empty;
    public string P256dh { get; set; } = string.Empty;
    public string Auth { get; set; } = string.Empty;
    public int LeadMinutes { get; set; } = 10;
    public string? TimeZone { get; set; }
}

public class UnsubscribeRequest
{
    public string Endpoint { get; set; } = string.Empty;
}

public class TestPushResponse
{
    public int Sent { get; set; }
    public int Failed { get; set; }
    public int Removed { get; set; }
}
