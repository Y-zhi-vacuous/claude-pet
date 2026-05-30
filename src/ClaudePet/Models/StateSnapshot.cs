namespace ClaudePet.Models;

public class StateSnapshot
{
    public string Model { get; set; } = string.Empty;
    public double ContextPercent { get; set; }
    public int InputTokens { get; set; }
    public int MaxTokens { get; set; }
    public string Status { get; set; } = "idle";
    public List<ToolActivity> ActiveTools { get; set; } = new();
    public List<AgentInfo> RunningAgents { get; set; } = new();
    public List<TodoItem> Todos { get; set; } = new();
}

public class ToolActivity
{
    public string ToolName { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public string State { get; set; } = "running";
    public DateTime StartedAt { get; set; }
}

public class AgentInfo
{
    public string AgentName { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
}

public class TodoItem
{
    public string Content { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
}
