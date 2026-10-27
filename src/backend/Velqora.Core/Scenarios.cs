using System.Security.Cryptography;
using System.Text;
namespace Velqora.Core;
public static class Scenarios
{
    public static readonly string[] Names = ["login-abuse", "privilege-misuse", "event-spike", "telemetry-loss", "benign-baseline"];
    public static int Count(string name) => name switch
    {
        "login-abuse" => 13, "privilege-misuse" => 3, "event-spike" => 120,
        "telemetry-loss" => 1, "benign-baseline" => 30, _ => throw new DomainException("Unknown scenario.")
    };
    public static EventInput Event(ScenarioRun run, int index)
    {
        var type = run.Scenario switch
        {
            "login-abuse" => index < 12 ? "auth.failure" : "auth.success",
            "privilege-misuse" => index == 2 ? "privilege.change" : "auth.success",
            "event-spike" => "network.connection",
            "telemetry-loss" => "heartbeat",
            _ => index % 10 == 0 ? "auth.failure" : "auth.success"
        };
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{run.Id}:{index}"));
        return new(new Guid(bytes.AsSpan(0, 16)), 1, run.CreatedAt.AddMilliseconds(index * 100),
            "velqora-simulation", type, run.Device, run.Account, "192.0.2.42",
            new() { ["scenario"] = run.Scenario, ["sequence"] = index.ToString(), ["label"] = run.Scenario == "benign-baseline" ? "benign" : "suspicious" }, true);
    }
}
