# QA Ticket: TICKET-050

**Title:** Outbound Webhook Security Vulnerability: HTTP 301/302 Redirect Bypass & DNS Rebinding Allow Internal Network SSRF  
**Severity:** 🔴 P1 (High - Server-Side Request Forgery & Internal Infrastructure Probing)  
**QA Focus Area:** Application Security, Penetration Testing & OWASP API Top 10 (SSRF)  
**Found By:** `qa-security-pentest`  
**Status:** Open  
**Project Mode:** Greenfield (Benchmark Educational Standard)  

---

## 1. Description & Architectural Context

In TICKET-035, outbound creator webhook registration implemented basic SSRF defense in [`RegisterWebhookSubscriptionCommandValidator.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/CampaignUpdates/CrowdFunding.Modules.CampaignUpdates.Application/Features/WebhookSubscriptions/Commands/RegisterWebhookSubscription/RegisterWebhookSubscriptionCommandValidator.cs) by rejecting target URLs pointing to private IP addresses (e.g. `127.0.0.1`, `10.0.0.0/8`, `192.168.0.0/16`).

However, the outbound dispatching implementation in [`WebhookDispatcherBackgroundService.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/CampaignUpdates/CrowdFunding.Modules.CampaignUpdates.Infrastructure/Services/WebhookDispatcherBackgroundService.cs#L69-L86) introduces two critical security vulnerabilities:

```csharp
using var httpClient = httpClientFactory.CreateClient(nameof(WebhookDispatcherBackgroundService));
httpClient.Timeout = TimeSpan.FromSeconds(5);

using var request = new HttpRequestMessage(HttpMethod.Post, subscription.TargetUrl)
{
    Content = new StringContent(task.PayloadJson, Encoding.UTF8, "application/json"),
};
// ...
using var response = await httpClient.SendAsync(request, cancellationToken);
```

### Vulnerability 1: HTTP 301/302 Auto-Redirect SSRF Bypass
By default in .NET, `HttpClient` has `AllowAutoRedirect = true`.
If a malicious creator registers a seemingly valid public HTTPS URL (`https://attacker.com/webhook`):
1. The registration-time validator checks DNS: `attacker.com` resolves to a public IP `203.0.113.5` (validation passes).
2. When the background service dispatches the webhook POST, `attacker.com` responds with:
   ```http
   HTTP/1.1 302 Found
   Location: http://169.254.169.254/latest/meta-data/iam/security-credentials/
   ```
3. `HttpClient` **automatically follows the redirect** to the AWS/GCP instance metadata endpoint or internal Kubernetes services (`http://kubernetes.default.svc`)!
4. The background service forwards internal data and logs error details, enabling the attacker to probe and exfiltrate internal cloud metadata.

### Vulnerability 2: DNS Rebinding Time-of-Check to Time-of-Use
The IP address is only checked at registration time. A malicious actor can register `https://webhook.attacker.com` with a short DNS TTL (1 second). After registration passes, they update DNS to point to `127.0.0.1` or internal PostgreSQL (`10.0.0.5:5432`), completely bypassing the registration filter at dispatch time.

---

## 2. Blast Radius & OWASP API Top 10 Impact

- **OWASP API Top 10 (API7:2023 - Server-Side Request Forgery):** Outbound webhook dispatchers are the primary vector for SSRF in modern SaaS platforms.
- **Cloud Credential Exfiltration:** If running in AWS, Azure, or GCP, redirecting to `169.254.169.254` can expose instance IAM role credentials, resulting in total cloud account compromise.

---

## 3. Educational Rationale: Teaching Principals & Architects

### The Pedagogical Objective
Teach architects why **SSRF Defense Cannot Rely on Registration-Time URL Validation Alone**. In any architecture that issues HTTP requests to user-supplied endpoints:
1. HTTP redirects must be disabled (`AllowAutoRedirect = false`),
2. Socket connections must resolve IP addresses at connection time, and
3. Destination IPs must be verified against private CIDR ranges immediately before establishing the TCP handshake.

### Monolith First, Microservices Ready
Whether running in a monolith or a dedicated outbound integration worker, securing outbound HTTP traffic against SSRF is a non-negotiable enterprise security standard.

---

## 4. Affected Files & Modules

- [`src/Modules/CampaignUpdates/CrowdFunding.Modules.CampaignUpdates.Infrastructure/Services/WebhookDispatcherBackgroundService.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/CampaignUpdates/CrowdFunding.Modules.CampaignUpdates.Infrastructure/Services/WebhookDispatcherBackgroundService.cs)
- [`src/Modules/CampaignUpdates/CrowdFunding.Modules.CampaignUpdates.Infrastructure/DependencyInjection/CampaignUpdatesInfrastructureDependencyInjection.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/CampaignUpdates/CrowdFunding.Modules.CampaignUpdates.Infrastructure/DependencyInjection/CampaignUpdatesInfrastructureDependencyInjection.cs)

---

## 5. Greenfield Remediation Guidance

1. In `CampaignUpdatesInfrastructureDependencyInjection.cs`, configure the named `HttpClient` with a custom `SocketsHttpHandler`:
   ```csharp
   services.AddHttpClient(nameof(WebhookDispatcherBackgroundService))
       .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
       {
           // Critical: NEVER follow redirects to prevent SSRF bypass
           AllowAutoRedirect = false,
           ConnectCallback = async (context, ct) =>
           {
               var entry = await Dns.GetHostEntryAsync(context.DnsEndPoint.Host, ct);
               var ip = entry.AddressList.FirstOrDefault();
               if (ip is null || IsPrivateOrLoopback(ip))
               {
                   throw new SecurityException($"Connection to private/restricted IP '{ip}' is blocked.");
               }

               var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
               await socket.ConnectAsync(new IPEndPoint(ip, context.DnsEndPoint.Port), ct);
               return new NetworkStream(socket, ownsSocket: true);
           }
       });
   ```
2. Ensure any HTTP 3xx redirect response from a webhook target is treated as a failed delivery without following the Location header.

---

## 6. Verification & Acceptance Criteria

1. **Auto-Redirect Disabled:** When a webhook target returns a 301 or 302 redirect, `HttpClient` does not follow it and marks the delivery attempt failed.
2. **DNS Rebinding Blocked:** At TCP connection time, if the resolved IP belongs to loopback, private RFC 1918, or cloud metadata ranges, the socket connection is aborted immediately.
