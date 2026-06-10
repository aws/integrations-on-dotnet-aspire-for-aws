// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.

using Amazon.BedrockAgentCore;

var builder = WebApplication.CreateBuilder(args);

// The AWS SDK automatically picks up AWS_ENDPOINT_URL_BEDROCK_AGENTCORE
// set by Aspire's WithReference() — no manual ServiceURL configuration needed.
builder.Services.TryAddAWSService<IAmazonBedrockAgentCore>();

var app = builder.Build();

app.MapPost("/chat", async (ChatRequest request, IAmazonBedrockAgentCore client) =>
{
    var payload = System.Text.Json.JsonSerializer.Serialize(new { prompt = request.Message });
    using var payloadStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(payload));

    var response = await client.InvokeAgentRuntimeAsync(new Amazon.BedrockAgentCore.Model.InvokeAgentRuntimeRequest
    {
        AgentRuntimeArn = "local-agent",
        Payload = payloadStream
    });

    using var reader = new StreamReader(response.Response);
    var body = await reader.ReadToEndAsync();

    return Results.Ok(new { response = body });
});

app.MapGet("/", () => Results.Content("""
    <!DOCTYPE html>
    <html>
    <head><title>AgentCore Chat (WithReference Demo)</title>
    <style>
        * { margin: 0; padding: 0; box-sizing: border-box; }
        body { font-family: system-ui, sans-serif; background: #0f1117; color: #e8eaed; height: 100vh; display: flex; flex-direction: column; }
        header { padding: 16px 24px; border-bottom: 1px solid rgba(255,255,255,0.06); }
        header h1 { font-size: 16px; font-weight: 600; }
        header p { font-size: 12px; color: #6b7280; margin-top: 2px; }
        .messages { flex: 1; overflow-y: auto; padding: 24px; display: flex; flex-direction: column; gap: 12px; }
        .msg { max-width: 720px; padding: 12px 16px; border-radius: 10px; font-size: 14px; line-height: 1.5; white-space: pre-wrap; }
        .msg.user { background: #1d4ed8; color: #fff; align-self: flex-end; }
        .msg.agent { background: #1c2029; border: 1px solid rgba(255,255,255,0.06); align-self: flex-start; }
        .input-area { padding: 16px 24px; border-top: 1px solid rgba(255,255,255,0.06); display: flex; gap: 10px; }
        input { flex: 1; padding: 10px 14px; border-radius: 8px; border: 1px solid rgba(255,255,255,0.1); background: #1a1d27; color: #e8eaed; font-size: 14px; outline: none; }
        button { padding: 10px 20px; border-radius: 8px; border: none; background: #2563eb; color: #fff; font-size: 14px; cursor: pointer; }
        button:disabled { opacity: 0.5; }
    </style>
    </head>
    <body>
        <header>
            <h1>AgentCore ChatUI</h1>
            <p>Invokes agent via AWS SDK &mdash; endpoint injected by WithReference()</p>
        </header>
        <div class="messages" id="msgs"></div>
        <div class="input-area">
            <input id="inp" placeholder="Type a message..." autocomplete="off" />
            <button id="btn" onclick="send()">Send</button>
        </div>
        <script>
            const inp = document.getElementById('inp'), btn = document.getElementById('btn'), msgs = document.getElementById('msgs');
            inp.addEventListener('keydown', e => { if (e.key === 'Enter' && !btn.disabled) send(); });
            async function send() {
                const text = inp.value.trim(); if (!text) return;
                inp.value = ''; add(text, 'user'); btn.disabled = true; btn.textContent = '...';
                try {
                    const r = await fetch('/chat', { method: 'POST', headers: {'Content-Type':'application/json'}, body: JSON.stringify({message:text}) });
                    const d = await r.json(); add(d.response || JSON.stringify(d), 'agent');
                } catch(e) { add('Error: '+e.message, 'agent'); }
                btn.disabled = false; btn.textContent = 'Send'; inp.focus();
            }
            function add(t, c) { const d = document.createElement('div'); d.className='msg '+c; d.textContent=t; msgs.appendChild(d); msgs.scrollTop=msgs.scrollHeight; }
        </script>
    </body>
    </html>
    """, "text/html"));

app.Run();

record ChatRequest(string Message);
