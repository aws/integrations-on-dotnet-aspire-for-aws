using DeploymentTestApp.AppHost;
using System.Reflection;

const string envScenarioEnvironmentVariable = "AWS_ASPIRE_PUBLISH_SCENARIO";

Console.WriteLine("Starting DeploymentTestApp AppHost with arguments: " + string.Join(',', Environment.GetCommandLineArgs()));

var scenario = GetScenarioFromArgs(args);

Console.WriteLine("Executing scenario: " + scenario);

if (string.IsNullOrWhiteSpace(scenario))
    throw new ArgumentException($"Missing required switch {DeploymentTestAppConstants.ScenarioSwitch}");

try
{
    await InvokeScenarioAsync(scenario);
}
finally
{
    // Clear the environment variable after the scenario has run
    Environment.SetEnvironmentVariable(envScenarioEnvironmentVariable, null);
}

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

    string? scenario = null;
    if (Environment.GetEnvironmentVariable(envScenarioEnvironmentVariable) is string envVarValue && !string.IsNullOrWhiteSpace(envVarValue))
    {
        scenario = envVarValue;
    }
    else
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].Equals(DeploymentTestAppConstants.ScenarioSwitch, StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length)
                {
                    scenario = args[i + 1];
                }
                break;
            }
        }

        // Store the current scenario in an environment variable so if we need to get the
        // CDK context using the internal fork mechanism that doesn't pass in the --scenario switch
        // we can still retrieve the scenario.
        Environment.SetEnvironmentVariable("AWS_ASPIRE_PUBLISH_SCENARIO", scenario);
    }

    return scenario;
}

await Scenarios.PublishWebApp2ReferenceOnWebApp1();

