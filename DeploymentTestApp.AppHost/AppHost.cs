using DeploymentTestApp.AppHost;
using System.Reflection;

var scenario = GetScenarioFromArgs(args);

if (string.IsNullOrWhiteSpace(scenario))
    throw new ArgumentException($"Missing required switch {DeploymentTestAppConstants.ScenarioSwitch}");

await InvokeScenarioAsync(scenario);

static async Task InvokeScenarioAsync(string scenario)
{
    var scenariosType = typeof(Scenarios);

    // Find a public static, parameterless method with the given name
    var method = scenariosType.GetMethod(
        scenario,
        BindingFlags.Public | BindingFlags.Static,
        binder: null,
        types: Type.EmptyTypes,
        modifiers: null);

    if (method is null)
        throw new ArgumentException($"Unknown scenario {scenario}");

    // Invoke the method
    var result = method.Invoke(null, null);

    // Support async scenarios
    if (result is Task task)
    {
        await task;
    }
    else if (method.ReturnType != typeof(void))
    {
        throw new InvalidOperationException(
            $"Scenario '{scenario}' must return void or Task.");
    }
}


static string? GetScenarioFromArgs(string[] args)
{
    for (int i = 0; i < args.Length; i++)
    {
        if (args[i].Equals(DeploymentTestAppConstants.ScenarioSwitch, StringComparison.OrdinalIgnoreCase))
        {
            if (i + 1 < args.Length)
            {
                return args[i + 1];
            }
            return null;
        }
    }
    return null;
}

await Scenarios.PublishWebApp2ReferenceOnWebApp1();

