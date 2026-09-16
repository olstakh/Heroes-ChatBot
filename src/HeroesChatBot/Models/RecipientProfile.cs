namespace HeroesChatBot.Models;

public sealed class RecipientProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserName { get; set; } = string.Empty;
    public int IntervalSeconds { get; set; } = 3600;
    public List<string> Messages { get; set; } = [];
    public int NextMessageIndex { get; set; }
    public DateTimeOffset? LastSentAt { get; set; }

    public string GetNextMessage()
    {
        if (Messages.Count == 0)
        {
            throw new InvalidOperationException("The recipient has no messages.");
        }

        var index = NextMessageIndex % Messages.Count;
        NextMessageIndex = (index + 1) % Messages.Count;
        return Messages[index];
    }

    public override string ToString() => string.IsNullOrWhiteSpace(UserName) ? "(new recipient)" : UserName;
}
