// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.BedrockAgentCore;

var builder = WebApplication.CreateBuilder(args);

// The AWS SDK automatically picks up AWS_ENDPOINT_URL_BEDROCK_AGENTCORE
// set by Aspire's WithReference() — no manual ServiceURL configuration needed.
builder.Services.TryAddAWSService<IAmazonBedrockAgentCore>();

var app = builder.Build();

// Simple API to invoke the agent via the SDK
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
    <head>
        <title>AgentCore Chat</title>
        <style>
            * { margin: 0; padding: 0; box-sizing: border-box; }
            body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif; background: #0f1117; color: #e8eaed; height: 100vh; display: flex; flex-direction: column; }
            header { padding: 16px 24px; border-bottom: 1px solid rgba(255,255,255,0.06); }
            header h1 { font-size: 16px; font-weight: 600; color: #fafafa; }
            header p { font-size: 12px; color: #6b7280; margin-top: 2px; }
            .messages { flex: 1; overflow-y: auto; padding: 24px; display: flex; flex-direction: column; gap: 16px; }
            .msg { max-width: 720px; padding: 12px 16px; border-radius: 10px; font-size: 14px; line-height: 1.6; white-space: pre-wrap; word-wrap: break-word; }
            .msg.user { background: #1d4ed8; color: #fff; align-self: flex-end; border-bottom-right-radius: 2px; }
            .msg.agent { background: #1c2029; border: 1px solid rgba(255,255,255,0.06); align-self: flex-start; border-bottom-left-radius: 2px; }
            .msg.error { background: #7f1d1d; border: 1px solid #991b1b; }
            .input-area { padding: 16px 24px; border-top: 1px solid rgba(255,255,255,0.06); display: flex; gap: 10px; }
            input { flex: 1; padding: 10px 14px; border-radius: 8px; border: 1px solid rgba(255,255,255,0.1); background: #1a1d27; color: #e8eaed; font-size: 14px; outline: none; }
            input:focus { border-color: #3b82f6; }
            button { padding: 10px 20px; border-radius: 8px; border: none; background: #2563eb; color: #fff; font-size: 14px; font-weight: 500; cursor: pointer; }
            button:hover { background: #1d4ed8; }
            button:disabled { opacity: 0.5; cursor: not-allowed; }
            .spinner { display: inline-block; width: 12px; height: 12px; border: 2px solid rgba(255,255,255,0.3); border-top-color: #fff; border-radius: 50%; animation: spin 0.6s linear infinite; margin-right: 8px; }
            @keyframes spin { to { transform: rotate(360deg); } }
        </style>
    </head>
    <body>
        <header>
            <h1>AgentCore Chat</h1>
            <p>Connected to agent via AWS SDK (WithReference)</p>
        </header>
        <div class="messages" id="messages"></div>
        <div class="input-area">
            <input id="msg" placeholder="Type a message..." autocomplete="off" />
            <button id="btn" onclick="send()">Send</button>
        </div>
        <script>
            const input = document.getElementById('msg');
            const btn = document.getElementById('btn');
            const messages = document.getElementById('messages');

            input.addEventListener('keydown', e => { if (e.key === 'Enter' && !btn.disabled) send(); });

            async function send() {
                const text = input.value.trim();
                if (!text) return;
                input.value = '';
                addMessage(text, 'user');
                btn.disabled = true;
                btn.innerHTML = '<span class="spinner"></span>Thinking...';
                try {
                    const res = await fetch('/chat', {
                        method: 'POST',
                        headers: {'Content-Type': 'application/json'},
                        body: JSON.stringify({message: text})
                    });
                    const data = await res.json();
                    addMessage(data.response || JSON.stringify(data, null, 2), 'agent');
                } catch (e) {
                    addMessage('Error: ' + e.message, 'error');
                }
                btn.disabled = false;
                btn.textContent = 'Send';
                input.focus();
            }

            function addMessage(text, type) {
                const div = document.createElement('div');
                div.className = 'msg ' + type;
                div.textContent = text;
                messages.appendChild(div);
                messages.scrollTop = messages.scrollHeight;
            }
        </script>
    </body>
    </html>
    """, "text/html"));

app.Run();

record ChatRequest(string Message);
