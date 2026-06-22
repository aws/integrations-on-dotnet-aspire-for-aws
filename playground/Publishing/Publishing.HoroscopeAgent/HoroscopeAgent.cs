// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using System.ComponentModel;
using AWS.AgentCore.Hosting;
using Microsoft.Agents.AI;
using Publishing.HoroscopeAgent.Models;

namespace Publishing.HoroscopeAgent;

public class HoroscopeAgent(ChatClientAgent chatAgent, ILogger<HoroscopeAgent> logger)
{
    [AgentCoreHandler]
    public async Task<string> HandleInvocation(
        PromptRequest request,
        AgentCoreRuntimeContext context,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Horoscope invocation — SessionId={SessionId}, RequestId={RequestId}",
            context.SessionId, context.RequestId);

        var session = await chatAgent.CreateSessionAsync(cancellationToken: cancellationToken);

        var response = await chatAgent.RunAsync(
            request.Prompt ?? "Give me a general horoscope for today.",
            session: session,
            cancellationToken: cancellationToken);

        return response.ToString();
    }

    [AgentCorePing]
    public object Ping() => new { status = "Healthy", time_of_last_update = DateTimeOffset.UtcNow.ToUnixTimeSeconds() };

    [Description("Gets today's horoscope for a given zodiac sign. Valid signs are: Aries, Taurus, Gemini, Cancer, Leo, Virgo, Libra, Scorpio, Sagittarius, Capricorn, Aquarius, Pisces.")]
    public static string GetHoroscope(
        [Description("The zodiac sign to get the horoscope for")] string sign,
        [Description("The current date in yyyy-MM-dd format")] string date)
        => $"Horoscope for {sign} on {date}: The stars are aligned in your favor today. Trust your instincts and embrace new opportunities.";

    [Description("Gets the zodiac sign for a given birth date.")]
    public static string GetZodiacSign([Description("The birth date in MM-dd format")] string birthDate)
    {
        if (!DateTime.TryParseExact($"2000-{birthDate}", "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var date))
            return "Invalid date format. Please use MM-dd format.";

        return date switch
        {
            { Month: 3, Day: >= 21 } or { Month: 4, Day: <= 19 } => "Aries",
            { Month: 4, Day: >= 20 } or { Month: 5, Day: <= 20 } => "Taurus",
            { Month: 5, Day: >= 21 } or { Month: 6, Day: <= 20 } => "Gemini",
            { Month: 6, Day: >= 21 } or { Month: 7, Day: <= 22 } => "Cancer",
            { Month: 7, Day: >= 23 } or { Month: 8, Day: <= 22 } => "Leo",
            { Month: 8, Day: >= 23 } or { Month: 9, Day: <= 22 } => "Virgo",
            { Month: 9, Day: >= 23 } or { Month: 10, Day: <= 22 } => "Libra",
            { Month: 10, Day: >= 23 } or { Month: 11, Day: <= 21 } => "Scorpio",
            { Month: 11, Day: >= 22 } or { Month: 12, Day: <= 21 } => "Sagittarius",
            { Month: 12, Day: >= 22 } or { Month: 1, Day: <= 19 } => "Capricorn",
            { Month: 1, Day: >= 20 } or { Month: 2, Day: <= 18 } => "Aquarius",
            _ => "Pisces"
        };
    }

    [Description("Gets the current date in yyyy-MM-dd format.")]
    public static string GetCurrentDate() => DateTime.UtcNow.ToString("yyyy-MM-dd");
}
